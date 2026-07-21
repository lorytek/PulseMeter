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
    private static readonly string ClientVersion =
        typeof(CodexUsageService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly IMockUsageService _mockUsageService;
    private readonly ICodexResetCreditService _resetCreditService;
    private readonly SharedRolloutAnalyticsSource _rolloutAnalyticsSource;
    private readonly IProjectUsageService _projectUsageService;
    private readonly IUsageAttributionService _usageAttributionService;
    private readonly IAppServerProcessFactory _processFactory;
    private readonly IJsonRpcClientFactory _jsonRpcClientFactory;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _snapshotGate = new();
    private readonly object _analyticsRefreshGate = new();
    private readonly object _notificationRefreshGate = new();
    private readonly object _disposeGate = new();
    private readonly CancellationTokenSource _analyticsRefreshCancellation = new();
    private readonly CancellationTokenSource _notificationRefreshCancellation = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private IAppServerProcess? _process;
    private IJsonRpcClient? _client;
    private UsageSnapshot? _lastGoodLiveSnapshot;
    private ThreadUsageSnapshot? _recentThread;
    private Task? _analyticsRefreshPump;
    private Task? _notificationRefreshPump;
    private AnalyticsRefreshRequest? _pendingAnalyticsRefresh;
    private bool _notificationRefreshRequested;
    private long _liveSnapshotGeneration;
    private int _activeOperationCount;
    private TaskCompletionSource? _operationsDrained;
    private Task? _disposeTask;
    private bool _disposed;

    public CodexUsageService(
        IMockUsageService mockUsageService,
        ICodexResetCreditService resetCreditService,
        SharedRolloutAnalyticsSource rolloutAnalyticsSource,
        IProjectUsageService projectUsageService,
        IUsageAttributionService usageAttributionService,
        IAppServerProcessFactory processFactory,
        IJsonRpcClientFactory jsonRpcClientFactory)
    {
        _mockUsageService = mockUsageService;
        _resetCreditService = resetCreditService;
        _rolloutAnalyticsSource = rolloutAnalyticsSource;
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
        using var operation = EnterOperation();
        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await _refreshLock.WaitAsync(refreshCancellation.Token).ConfigureAwait(false);
        try
        {
            if (UseMockMode)
            {
                var mock = await _mockUsageService.GetSnapshotAsync(refreshCancellation.Token).ConfigureAwait(false);
                SnapshotUpdated?.Invoke(this, mock);
                return mock;
            }

            return await GetLiveSnapshotAsync(refreshCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (!UseMockMode && ShouldFallbackFromRefreshException(ex, cancellationToken))
        {
            await ResetConnectionAsync().ConfigureAwait(false);
            var fallback = await BuildFallbackSnapshotAsync(ex, refreshCancellation.Token).ConfigureAwait(false);
            SnapshotUpdated?.Invoke(this, fallback);
            return fallback;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        Task? operationsDrained = null;
        TaskCompletionSource? disposalCompletion = null;
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                operationsDrained = _activeOperationCount == 0
                    ? Task.CompletedTask
                    : (_operationsDrained ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                disposalCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = disposalCompletion.Task;
            }

            disposeTask = _disposeTask;
        }

        if (disposalCompletion is not null)
        {
            _ = CompleteDisposalAsync(operationsDrained!, disposalCompletion);
        }

        return new ValueTask(disposeTask);
    }

    private async Task CompleteDisposalAsync(
        Task operationsDrained,
        TaskCompletionSource disposalCompletion)
    {
        try
        {
            await DisposeCoreAsync(operationsDrained).ConfigureAwait(false);
            disposalCompletion.TrySetResult();
        }
        catch (Exception ex)
        {
            disposalCompletion.TrySetException(ex);
        }
    }

    private async Task DisposeCoreAsync(Task operationsDrained)
    {
        Task? analyticsRefreshPump;
        Task? notificationRefreshPump;
        lock (_notificationRefreshGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            _notificationRefreshRequested = false;
            _notificationRefreshCancellation.Cancel();
            notificationRefreshPump = _notificationRefreshPump;
        }

        lock (_analyticsRefreshGate)
        {
            _pendingAnalyticsRefresh = null;
            _analyticsRefreshCancellation.Cancel();
            analyticsRefreshPump = _analyticsRefreshPump;
        }

        try
        {
            await Task.WhenAll(
                    notificationRefreshPump ?? Task.CompletedTask,
                    analyticsRefreshPump ?? Task.CompletedTask)
                .ConfigureAwait(false);
            await operationsDrained.ConfigureAwait(false);
            await DisposeConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            _analyticsRefreshCancellation.Dispose();
            _notificationRefreshCancellation.Dispose();
            _lifetimeCancellation.Dispose();
            _connectLock.Dispose();
            _refreshLock.Dispose();
        }
    }

    private OperationLease EnterOperation()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is not null)
            {
                throw new ObjectDisposedException(nameof(CodexUsageService));
            }

            _activeOperationCount++;
            return new OperationLease(this);
        }
    }

    private void ExitOperation()
    {
        lock (_disposeGate)
        {
            _activeOperationCount--;
            if (_activeOperationCount == 0)
            {
                _operationsDrained?.TrySetResult();
            }
        }
    }

    public static bool ShouldFallbackFromRefreshException(Exception exception, CancellationToken callerCancellationToken)
    {
        return exception is not OperationCanceledException || !callerCancellationToken.IsCancellationRequested;
    }

    private async Task<UsageSnapshot> GetLiveSnapshotAsync(CancellationToken cancellationToken)
    {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await ReadRateLimitsAsync(client, cancellationToken).ConfigureAwait(false);
        snapshot = await TryMergeResetCreditExpiryAsync(snapshot, cancellationToken).ConfigureAwait(false);

        var lastGoodLiveSnapshot = GetLastGoodLiveSnapshot();
        if (lastGoodLiveSnapshot is not null
            && RateLimitSnapshotGuard.IsSuspiciousRegression(lastGoodLiveSnapshot, snapshot))
        {
            client = await EnsureClientAsync(cancellationToken, forceReconnect: true).ConfigureAwait(false);
            var confirmation = await ReadRateLimitsAsync(client, cancellationToken).ConfigureAwait(false);
            if (!RateLimitSnapshotGuard.IsConfirmedSnapshotChange(lastGoodLiveSnapshot, snapshot, confirmation))
            {
                var stale = CodexUsageParser.WithStatus(
                    lastGoodLiveSnapshot,
                    SyncStatus.Stale,
                    "AppServer",
                    "Rate-limit readings disagreed; showing the last confirmed values.");
                SnapshotUpdated?.Invoke(this, stale);
                return stale;
            }

            snapshot = PreserveResetCreditMetadata(confirmation, snapshot);
        }

        var now = snapshot.LastUpdatedUtc ?? DateTimeOffset.UtcNow;
        snapshot = await TryMergeRateLimitHistoryAsync(snapshot, now, cancellationToken).ConfigureAwait(false);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LiveRequestTimeout);
            var usage = await client.SendRequestAsync("account/usage/read", null, timeout.Token).ConfigureAwait(false);
            snapshot = CodexUsageParser.MergeUsageSummary(snapshot, usage);
        }
        catch (Exception ex) when (ex is JsonRpcException or JsonException or InvalidOperationException)
        {
            snapshot = CodexUsageParser.WithStatus(snapshot, SyncStatus.Live, "AppServer", "Rate limits are live; account usage summary was unavailable.");
        }

        var recentThread = GetRecentThread();
        if (recentThread is not null)
        {
            snapshot = CodexUsageParser.WithThreadUsage(snapshot, recentThread);
        }
        else
        {
            using var threadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            threadTimeout.CancelAfter(ThreadObservationTimeout);
            await TryObserveLoadedThreadsAsync(client, threadTimeout.Token).ConfigureAwait(false);
            recentThread = GetRecentThread();
            if (recentThread is not null)
            {
                snapshot = CodexUsageParser.WithThreadUsage(snapshot, recentThread);
            }
        }

        var stored = StoreLiveSnapshot(snapshot);
        snapshot = stored.Snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);

        if (snapshot.DailyBuckets.Count > 0)
        {
            RequestAnalyticsRefresh(snapshot.DailyBuckets, now, stored.Generation);
        }

        return snapshot;
    }

    private static async Task<UsageSnapshot> ReadRateLimitsAsync(
        IJsonRpcClient client,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LiveRequestTimeout);

        var now = DateTimeOffset.UtcNow;
        var rateLimits = await client.SendRequestAsync("account/rateLimits/read", null, timeout.Token).ConfigureAwait(false);
        Debug.WriteLine("[pulsemeter] account/rateLimits/read raw result: " + rateLimits.GetRawText());
        return CodexUsageParser.ParseRateLimits(rateLimits, now, "AppServer");
    }

    private static UsageSnapshot PreserveResetCreditMetadata(
        UsageSnapshot confirmation,
        UsageSnapshot candidate)
    {
        return new UsageSnapshot
        {
            Buckets = confirmation.Buckets,
            RateLimitHistory = confirmation.RateLimitHistory,
            LifetimeTokens = confirmation.LifetimeTokens,
            PeakDailyTokens = confirmation.PeakDailyTokens,
            LongestRunningTurnSec = confirmation.LongestRunningTurnSec,
            CurrentStreakDays = confirmation.CurrentStreakDays,
            LongestStreakDays = confirmation.LongestStreakDays,
            DailyBuckets = confirmation.DailyBuckets,
            ProjectUsageRows = confirmation.ProjectUsageRows,
            UsageAttribution = confirmation.UsageAttribution,
            ResetCreditsAvailable = candidate.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = candidate.ResetCreditsExpiresAtUtc,
            ResetCredits = candidate.ResetCredits,
            RecentActiveThread = confirmation.RecentActiveThread,
            SyncStatus = confirmation.SyncStatus,
            LastUpdatedUtc = confirmation.LastUpdatedUtc,
            Source = confirmation.Source,
            StatusMessage = confirmation.StatusMessage,
            RawRateLimitsJson = confirmation.RawRateLimitsJson
        };
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

    private async Task<IReadOnlyList<ProjectUsageRow>?> TryReadProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _projectUsageService.GetProjectUsageAsync(
                    dailyBuckets,
                    now,
                    sessionSummaries,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[pulsemeter] project analytics refresh failed: " + ex.GetType().Name);
            return null;
        }
    }

    private async Task<UsageSnapshot> TryMergeRateLimitHistoryAsync(
        UsageSnapshot snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeWindows = snapshot.Buckets
                .Where(bucket => !string.IsNullOrWhiteSpace(bucket.LimitId)
                    && bucket.WindowDurationMins is > 0
                    && bucket.ResetsAtUtc is not null)
                .Select(bucket => new
                {
                    LimitKey = bucket.LimitId!,
                    WindowDurationMins = bucket.WindowDurationMins!.Value,
                    ResetsAtUtc = bucket.ResetsAtUtc!.Value
                })
                .ToArray();
            if (activeWindows.Length == 0)
            {
                return snapshot;
            }

            var oldestActiveWindow = activeWindows
                .Select(window => window.ResetsAtUtc - TimeSpan.FromMinutes(window.WindowDurationMins))
                .Min();
            var cutoff = oldestActiveWindow > now.AddDays(-7) ? oldestActiveWindow : now.AddDays(-7);
            var history = await _rolloutAnalyticsSource
                .GetRateLimitHistoryAsync(cutoff, cancellationToken)
                .ConfigureAwait(false);
            var activeHistory = history
                .Where(point => activeWindows.Any(window =>
                    string.Equals(window.LimitKey, point.LimitKey, StringComparison.OrdinalIgnoreCase)
                    && window.WindowDurationMins == point.WindowDurationMins
                    && window.ResetsAtUtc == point.ResetsAtUtc))
                .ToArray();
            return CodexUsageParser.WithRateLimitHistory(snapshot, activeHistory);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[pulsemeter] rate-limit history refresh failed: " + ex.GetType().Name);
            return snapshot;
        }
    }

    private async Task<UsageAttributionSnapshot?> TryReadUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _usageAttributionService.GetUsageAttributionAsync(
                    dailyBuckets,
                    now,
                    sessionSummaries,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[pulsemeter] attribution analytics refresh failed: " + ex.GetType().Name);
            return null;
        }
    }

    private (UsageSnapshot Snapshot, long Generation) StoreLiveSnapshot(UsageSnapshot snapshot)
    {
        lock (_snapshotGate)
        {
            if (snapshot.RateLimitHistory.Count == 0
                && _lastGoodLiveSnapshot is { RateLimitHistory.Count: > 0 } previous)
            {
                snapshot = CodexUsageParser.WithRateLimitHistory(snapshot, previous.RateLimitHistory);
            }

            if (snapshot.DailyBuckets.Count > 0 && _lastGoodLiveSnapshot is not null)
            {
                snapshot = CodexUsageParser.WithProjectUsage(snapshot, _lastGoodLiveSnapshot.ProjectUsageRows);
                snapshot = CodexUsageParser.WithUsageAttribution(snapshot, _lastGoodLiveSnapshot.UsageAttribution);
            }

            _lastGoodLiveSnapshot = snapshot;
            _liveSnapshotGeneration++;
            return (snapshot, _liveSnapshotGeneration);
        }
    }

    private UsageSnapshot? GetLastGoodLiveSnapshot()
    {
        lock (_snapshotGate)
        {
            return _lastGoodLiveSnapshot;
        }
    }

    private ThreadUsageSnapshot? GetRecentThread()
    {
        lock (_snapshotGate)
        {
            return _recentThread;
        }
    }

    private void RequestAnalyticsRefresh(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        long generation)
    {
        lock (_analyticsRefreshGate)
        {
            if (_disposed || _analyticsRefreshCancellation.IsCancellationRequested)
            {
                return;
            }

            _pendingAnalyticsRefresh = new AnalyticsRefreshRequest(dailyBuckets, now, generation);
            _analyticsRefreshPump ??= Task.Run(RefreshAnalyticsPumpAsync);
        }
    }

    private async Task RefreshAnalyticsPumpAsync()
    {
        try
        {
            while (TryTakeAnalyticsRefreshRequest(out var request))
            {
                try
                {
                    await RefreshAnalyticsAsync(request, _analyticsRefreshCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_analyticsRefreshCancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[pulsemeter] analytics refresh failed: " + ex.GetType().Name);
                }
            }
        }
        finally
        {
            lock (_analyticsRefreshGate)
            {
                _analyticsRefreshPump = null;

                if (_disposed || _analyticsRefreshCancellation.IsCancellationRequested)
                {
                    _pendingAnalyticsRefresh = null;
                }
                else if (_pendingAnalyticsRefresh is not null)
                {
                    _analyticsRefreshPump = Task.Run(RefreshAnalyticsPumpAsync);
                }
            }
        }
    }

    private bool TryTakeAnalyticsRefreshRequest(out AnalyticsRefreshRequest request)
    {
        lock (_analyticsRefreshGate)
        {
            if (_disposed
                || _analyticsRefreshCancellation.IsCancellationRequested
                || _pendingAnalyticsRefresh is null)
            {
                request = null!;
                return false;
            }

            request = _pendingAnalyticsRefresh;
            _pendingAnalyticsRefresh = null;
            return true;
        }
    }

    private async Task RefreshAnalyticsAsync(
        AnalyticsRefreshRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries;
        try
        {
            var cutoffDate = DateOnly.FromDateTime(request.Now.ToLocalTime().DateTime).AddDays(-29);
            sessionSummaries = await _rolloutAnalyticsSource.GetSessionSummariesAsync(cutoffDate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[pulsemeter] rollout analytics refresh failed: " + ex.GetType().Name);
            return;
        }

        var projectUsageRows = await TryReadProjectUsageAsync(
                request.DailyBuckets,
                request.Now,
                sessionSummaries,
                cancellationToken)
            .ConfigureAwait(false);
        var usageAttribution = await TryReadUsageAttributionAsync(
                request.DailyBuckets,
                request.Now,
                sessionSummaries,
                cancellationToken)
            .ConfigureAwait(false);

        if (projectUsageRows is null && usageAttribution is null)
        {
            return;
        }

        UsageSnapshot? updatedSnapshot;
        lock (_snapshotGate)
        {
            if (_lastGoodLiveSnapshot is null || request.Generation != _liveSnapshotGeneration)
            {
                return;
            }

            updatedSnapshot = _lastGoodLiveSnapshot;
            if (projectUsageRows is not null)
            {
                updatedSnapshot = CodexUsageParser.WithProjectUsage(updatedSnapshot, projectUsageRows);
            }

            if (usageAttribution is not null)
            {
                updatedSnapshot = CodexUsageParser.WithUsageAttribution(updatedSnapshot, usageAttribution);
            }

            _lastGoodLiveSnapshot = updatedSnapshot;
        }

        SnapshotUpdated?.Invoke(this, updatedSnapshot);
    }

    private async Task<IJsonRpcClient> EnsureClientAsync(
        CancellationToken cancellationToken,
        bool forceReconnect = false)
    {
        if (!forceReconnect && _client is not null && _process is not null && !_process.HasExited)
        {
            return _client;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceReconnect && _client is not null && _process is not null && !_process.HasExited)
            {
                return _client;
            }

            await DisposeConnectionAsync().ConfigureAwait(false);
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
                    version = ClientVersion
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

    private async Task ResetConnectionAsync()
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_client is not null)
        {
            _client.NotificationReceived -= OnNotificationReceived;
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        _process?.Dispose();
        _process = null;
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
                    lock (_snapshotGate)
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
        }
        catch (Exception ex) when (ex is JsonRpcException or JsonException or InvalidOperationException or OperationCanceledException)
        {
        }
    }

    private Task<UsageSnapshot> BuildFallbackSnapshotAsync(Exception ex, CancellationToken cancellationToken)
    {
        var failure = ClassifyRefreshFailure(ex);
        var lastGoodLiveSnapshot = GetLastGoodLiveSnapshot();

        if (lastGoodLiveSnapshot is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(CodexUsageParser.WithStatus(
                lastGoodLiveSnapshot,
                SyncStatus.Stale,
                "AppServer",
                failure switch
                {
                    RefreshFailure.LaunchUnavailable => "The monitored CLI is unavailable; showing last good data.",
                    RefreshFailure.Timeout => "Live refresh timed out; showing last good data.",
                    _ => "Live refresh failed; showing last good data."
                }));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Unavailable,
            Source = "AppServer",
            StatusMessage = failure switch
            {
                RefreshFailure.LaunchUnavailable => "The monitored CLI is unavailable. Start it, then sync again.",
                RefreshFailure.Timeout => "The live usage source timed out. Try syncing again.",
                _ => "The live usage source is unavailable. Try syncing again."
            }
        });
    }

    private void OnNotificationReceived(object? sender, JsonRpcNotificationEventArgs args)
    {
        if (args.Method is "thread/tokenUsage/updated" or "thread/status/changed")
        {
            var parsedThread = CodexUsageParser.ParseThreadUsage(args.Parameters, DateTimeOffset.UtcNow);
            if (parsedThread is not null)
            {
                UsageSnapshot? updated = null;
                lock (_snapshotGate)
                {
                    _recentThread = parsedThread;

                    if (_lastGoodLiveSnapshot is not null)
                    {
                        updated = CodexUsageParser.WithThreadUsage(_lastGoodLiveSnapshot, parsedThread);
                        _lastGoodLiveSnapshot = updated;
                    }
                }

                if (updated is not null)
                {
                    SnapshotUpdated?.Invoke(this, updated);
                }
            }
        }

        if (args.Method is "account/rateLimits/updated" or "account/updated")
        {
            RequestNotificationRefresh();
        }
    }

    private void RequestNotificationRefresh()
    {
        lock (_notificationRefreshGate)
        {
            if (_disposed || _notificationRefreshCancellation.IsCancellationRequested)
            {
                return;
            }

            _notificationRefreshRequested = true;
            _notificationRefreshPump ??= Task.Run(RefreshNotificationPumpAsync);
        }
    }

    private async Task RefreshNotificationPumpAsync()
    {
        try
        {
            while (TryTakeNotificationRefreshRequest())
            {
                try
                {
                    await GetSnapshotAsync(_notificationRefreshCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_notificationRefreshCancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is JsonRpcException or JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    Debug.WriteLine("[pulsemeter] notification refresh failed: " + ex.GetType().Name);
                }
            }
        }
        finally
        {
            lock (_notificationRefreshGate)
            {
                _notificationRefreshPump = null;

                if (_disposed || _notificationRefreshCancellation.IsCancellationRequested)
                {
                    _notificationRefreshRequested = false;
                }
                else if (_notificationRefreshRequested)
                {
                    _notificationRefreshPump = Task.Run(RefreshNotificationPumpAsync);
                }
            }
        }
    }

    private bool TryTakeNotificationRefreshRequest()
    {
        lock (_notificationRefreshGate)
        {
            if (_disposed || _notificationRefreshCancellation.IsCancellationRequested || !_notificationRefreshRequested)
            {
                return false;
            }

            _notificationRefreshRequested = false;
            return true;
        }
    }

    private static RefreshFailure ClassifyRefreshFailure(Exception exception)
    {
        return exception switch
        {
            AppServerLaunchException => RefreshFailure.LaunchUnavailable,
            OperationCanceledException => RefreshFailure.Timeout,
            TimeoutException => RefreshFailure.Timeout,
            _ => RefreshFailure.LiveSourceFailure
        };
    }

    private enum RefreshFailure
    {
        LaunchUnavailable,
        Timeout,
        LiveSourceFailure
    }

    private sealed class OperationLease(CodexUsageService owner) : IDisposable
    {
        private CodexUsageService? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ExitOperation();
        }
    }

    private sealed record AnalyticsRefreshRequest(
        IReadOnlyList<DailyUsageBucket> DailyBuckets,
        DateTimeOffset Now,
        long Generation);
}
