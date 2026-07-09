using System.IO;
using System.Diagnostics;
using System.Text.Json;
using PulseMeter.Platform.Codex;

namespace PulseMeter.Slices.UsageCollection.Business;

public sealed class CodexUsageService : IUsageService, IAsyncDisposable
{
    private static readonly TimeSpan LiveRequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ResetCreditMergeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ThreadObservationTimeout = TimeSpan.FromSeconds(2);

    private readonly IMockUsageService _mockUsageService;
    private readonly ICodexResetCreditService _resetCreditService;
    private readonly IProjectUsageService _projectUsageService;
    private readonly IUsageAttributionService _usageAttributionService;
    private readonly IAppServerProcessFactory _processFactory;
    private readonly IJsonRpcClientFactory _jsonRpcClientFactory;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IAppServerProcess? _process;
    private IJsonRpcClient? _client;
    private UsageSnapshot? _lastGoodLiveSnapshot;
    private ThreadUsageSnapshot? _recentThread;
    private bool _disposed;

    public CodexUsageService(
        IMockUsageService mockUsageService,
        ICodexResetCreditService resetCreditService,
        IProjectUsageService projectUsageService,
        IUsageAttributionService usageAttributionService,
        IAppServerProcessFactory processFactory,
        IJsonRpcClientFactory jsonRpcClientFactory)
    {
        _mockUsageService = mockUsageService;
        _resetCreditService = resetCreditService;
        _projectUsageService = projectUsageService;
        _usageAttributionService = usageAttributionService;
        _processFactory = processFactory;
        _jsonRpcClientFactory = jsonRpcClientFactory;
    }

    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public bool UseMockMode { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (UseMockMode)
        {
            var mock = await _mockUsageService.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            SnapshotUpdated?.Invoke(this, mock);
            return mock;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetLiveSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ShouldFallbackFromRefreshException(ex, cancellationToken))
        {
            var fallback = await BuildFallbackSnapshotAsync(ex, cancellationToken).ConfigureAwait(false);
            SnapshotUpdated?.Invoke(this, fallback);
            return fallback;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        _process?.Dispose();
        _connectLock.Dispose();
        _refreshLock.Dispose();
    }

    public static bool ShouldFallbackFromRefreshException(Exception exception, CancellationToken callerCancellationToken)
    {
        return exception is not OperationCanceledException || !callerCancellationToken.IsCancellationRequested;
    }

    private async Task<UsageSnapshot> GetLiveSnapshotAsync(CancellationToken cancellationToken)
    {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LiveRequestTimeout);

        var now = DateTimeOffset.UtcNow;
        var rateLimits = await client.SendRequestAsync("account/rateLimits/read", null, timeout.Token).ConfigureAwait(false);
        Debug.WriteLine("[pulsemeter] account/rateLimits/read raw result: " + rateLimits.GetRawText());
        var snapshot = CodexUsageParser.ParseRateLimits(rateLimits, now, "AppServer");
        snapshot = await TryMergeResetCreditExpiryAsync(snapshot, cancellationToken).ConfigureAwait(false);

        try
        {
            var usage = await client.SendRequestAsync("account/usage/read", null, timeout.Token).ConfigureAwait(false);
            snapshot = CodexUsageParser.MergeUsageSummary(snapshot, usage);
            snapshot = await TryMergeProjectUsageAsync(snapshot, now, cancellationToken).ConfigureAwait(false);
            snapshot = await TryMergeUsageAttributionAsync(snapshot, now, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonRpcException or JsonException or InvalidOperationException)
        {
            snapshot = CodexUsageParser.WithStatus(snapshot, SyncStatus.Live, "AppServer", "Rate limits are live; account usage summary was unavailable.");
        }

        if (_recentThread is not null)
        {
            snapshot = CodexUsageParser.WithThreadUsage(snapshot, _recentThread);
        }
        else
        {
            using var threadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            threadTimeout.CancelAfter(ThreadObservationTimeout);
            await TryObserveLoadedThreadsAsync(client, threadTimeout.Token).ConfigureAwait(false);
            if (_recentThread is not null)
            {
                snapshot = CodexUsageParser.WithThreadUsage(snapshot, _recentThread);
            }
        }

        _lastGoodLiveSnapshot = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    private async Task<UsageSnapshot> TryMergeResetCreditExpiryAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ResetCreditMergeTimeout);

            var resetCredits = await _resetCreditService.TryFetchAsync(timeout.Token).ConfigureAwait(false);
            return resetCredits is null
                ? snapshot
                : CodexUsageParser.WithResetCredits(snapshot, resetCredits);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return snapshot;
        }
    }

    private async Task<UsageSnapshot> TryMergeProjectUsageAsync(
        UsageSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _projectUsageService.GetProjectUsageAsync(snapshot.DailyBuckets, now, cancellationToken)
                .ConfigureAwait(false);
            return rows.Count == 0 ? snapshot : CodexUsageParser.WithProjectUsage(snapshot, rows);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return snapshot;
        }
    }

    private async Task<UsageSnapshot> TryMergeUsageAttributionAsync(
        UsageSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var attribution = await _usageAttributionService.GetUsageAttributionAsync(snapshot.DailyBuckets, now, cancellationToken)
                .ConfigureAwait(false);
            return attribution.HasAttribution ? CodexUsageParser.WithUsageAttribution(snapshot, attribution) : snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return snapshot;
        }
    }

    private async Task<IJsonRpcClient> EnsureClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null && _process is not null && !_process.HasExited)
        {
            return _client;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null && _process is not null && !_process.HasExited)
            {
                return _client;
            }

            if (_client is not null)
            {
                await _client.DisposeAsync().ConfigureAwait(false);
            }

            _process?.Dispose();
            _process = _processFactory.Start();
            _client = _jsonRpcClientFactory.Create(_process.Output, _process.Input);
            _client.NotificationReceived += OnNotificationReceived;
            _client.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            var initialize = _client.SendRequestAsync("initialize", new
            {
                clientInfo = new
                {
                    name = "pulsemeter",
                    title = "PulseMeter",
                    version = "0.1.0"
                },
                capabilities = new
                {
                    experimentalApi = false,
                    requestAttestation = false
                }
            }, timeout.Token);

            await _client.SendNotificationAsync("initialized", new { }, timeout.Token).ConfigureAwait(false);
            await initialize.ConfigureAwait(false);
            return _client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task TryObserveLoadedThreadsAsync(IJsonRpcClient client, CancellationToken cancellationToken)
    {
        try
        {
            var loaded = await client.SendRequestAsync("thread/loaded/list", null, cancellationToken).ConfigureAwait(false);
            var data = loaded.ValueKind == JsonValueKind.Array
                ? loaded
                : loaded.ValueKind == JsonValueKind.Object
                    && loaded.TryGetProperty("data", out var dataProperty)
                    && dataProperty.ValueKind == JsonValueKind.Array
                        ? dataProperty
                        : default;

            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var firstItem = data[0];
                var first = firstItem.ValueKind == JsonValueKind.String
                    ? firstItem.GetString()
                    : firstItem.ValueKind == JsonValueKind.Object && firstItem.TryGetProperty("id", out var idProperty)
                        ? idProperty.GetString()
                        : null;

                if (!string.IsNullOrWhiteSpace(first))
                {
                    _recentThread = new ThreadUsageSnapshot
                    {
                        ThreadId = first,
                        LastUpdatedUtc = DateTimeOffset.UtcNow,
                        IsExactCurrentDesktopThread = false
                    };
                }
            }
        }
        catch (Exception ex) when (ex is JsonRpcException or JsonException or InvalidOperationException or OperationCanceledException)
        {
        }
    }

    private Task<UsageSnapshot> BuildFallbackSnapshotAsync(Exception ex, CancellationToken cancellationToken)
    {
        var message = ShortError(ex);

        if (_lastGoodLiveSnapshot is not null)
        {
            return Task.FromResult(CodexUsageParser.WithStatus(
                _lastGoodLiveSnapshot,
                SyncStatus.Stale,
                "AppServer",
                "Live refresh failed; showing last good data. " + message));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Unavailable,
            Source = "AppServer",
            StatusMessage = "The monitored app is not running or the local app-server is unavailable. Start it, then sync again. " + message
        });
    }

    private void OnNotificationReceived(object? sender, JsonRpcNotificationEventArgs args)
    {
        if (args.Method is "thread/tokenUsage/updated" or "thread/status/changed")
        {
            var parsedThread = CodexUsageParser.ParseThreadUsage(args.Parameters, DateTimeOffset.UtcNow);
            if (parsedThread is not null)
            {
                _recentThread = parsedThread;

                if (_lastGoodLiveSnapshot is not null)
                {
                    var updated = CodexUsageParser.WithThreadUsage(_lastGoodLiveSnapshot, parsedThread);
                    _lastGoodLiveSnapshot = updated;
                    SnapshotUpdated?.Invoke(this, updated);
                }
            }
        }

        if (args.Method is "account/rateLimits/updated" or "account/updated")
        {
            _ = RefreshFromNotificationAsync();
        }
    }

    private async Task RefreshFromNotificationAsync()
    {
        try
        {
            await GetSnapshotAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string ShortError(Exception ex)
    {
        return ex switch
        {
            JsonRpcException rpc when rpc.Code is not null => $"({rpc.Code}) {rpc.Message}",
            _ => ex.Message
        };
    }
}
