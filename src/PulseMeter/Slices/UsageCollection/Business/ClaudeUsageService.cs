using PulseMeter.Platform.Diagnostics;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

/// <summary>
/// Live usage for Claude Code: subscription rate limits from the signed-in Claude Code
/// account, plus local token analytics from Claude Code session logs.
/// </summary>
public sealed class ClaudeUsageService : IUsageService
{
    private static readonly TimeSpan LiveRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly IMockUsageService _mockUsageService;
    private readonly IClaudeUsageApiClient _apiClient;
    private readonly IClaudeLocalUsageSource _localUsageSource;
    private readonly IProjectUsageService _projectUsageService;
    private readonly IUsageAttributionService _usageAttributionService;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private UsageSnapshot? _lastGoodLiveSnapshot;

    public ClaudeUsageService(
        IMockUsageService mockUsageService,
        IClaudeUsageApiClient apiClient,
        IClaudeLocalUsageSource localUsageSource,
        IProjectUsageService projectUsageService,
        IUsageAttributionService usageAttributionService,
        Func<DateTimeOffset>? clock = null)
    {
        _mockUsageService = mockUsageService;
        _apiClient = apiClient;
        _localUsageSource = localUsageSource;
        _projectUsageService = projectUsageService;
        _usageAttributionService = usageAttributionService;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public bool UseMockMode { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (UseMockMode)
            {
                var mock = await _mockUsageService.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
                Publish(mock);
                return mock;
            }

            var snapshot = await GetLiveSnapshotAsync(cancellationToken).ConfigureAwait(false);
            Publish(snapshot);
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<UsageSnapshot> GetLiveSnapshotAsync(CancellationToken cancellationToken)
    {
        var now = _clock();
        var limitsTask = FetchLimitsAsync(cancellationToken);
        var localTask = ReadLocalUsageAsync(now, cancellationToken);
        await Task.WhenAll(limitsTask, localTask).ConfigureAwait(false);

        var limits = await limitsTask.ConfigureAwait(false);
        var local = await localTask.ConfigureAwait(false);
        var isLive = limits.Status == ClaudeUsageFetchStatus.Success;

        IReadOnlyList<RateLimitBucket> buckets = isLive
            ? ClaudeUsageParser.ParseRateLimitBuckets(limits.Payload, now)
            : _lastGoodLiveSnapshot?.Buckets ?? Array.Empty<RateLimitBucket>();
        var syncStatus = isLive
            ? SyncStatus.Live
            : _lastGoodLiveSnapshot is not null ? SyncStatus.Stale : SyncStatus.Unavailable;

        var dailyBuckets = local?.DailyBuckets ?? Array.Empty<DailyUsageBucket>();
        var sessions = local?.SessionSummaries ?? Array.Empty<SharedRolloutSessionSummary>();
        var projectRows = await TryAsync(
                () => _projectUsageService.GetProjectUsageAsync(dailyBuckets, now, sessions, cancellationToken),
                Array.Empty<ProjectUsageRow>() as IReadOnlyList<ProjectUsageRow>,
                "claude project analytics failed",
                cancellationToken)
            .ConfigureAwait(false);
        var attribution = await TryAsync(
                () => _usageAttributionService.GetUsageAttributionAsync(dailyBuckets, now, sessions, cancellationToken),
                UsageAttributionSnapshot.Empty,
                "claude attribution analytics failed",
                cancellationToken)
            .ConfigureAwait(false);

        var snapshot = new UsageSnapshot
        {
            Buckets = buckets,
            LifetimeTokens = local?.LifetimeTokens,
            PeakDailyTokens = local?.PeakDailyTokens,
            CurrentStreakDays = local?.CurrentStreakDays,
            LongestStreakDays = local?.LongestStreakDays,
            DailyBuckets = dailyBuckets,
            ProjectUsageRows = projectRows,
            UsageAttribution = attribution,
            ActivityEvidence = local?.ActivityEvidence ?? ActivityEvidenceSnapshot.Unavailable,
            RecentActiveThread = local?.RecentSession,
            SyncStatus = syncStatus,
            LastUpdatedUtc = isLive ? now : _lastGoodLiveSnapshot?.LastUpdatedUtc ?? now,
            Source = ClaudeUsageParser.Source,
            StatusMessage = BuildStatusMessage(limits.Status, syncStatus, local is not null, limits.Detail)
        };

        if (isLive)
        {
            _lastGoodLiveSnapshot = snapshot;
        }

        return snapshot;
    }

    private async Task<ClaudeUsageFetchResult> FetchLimitsAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LiveRequestTimeout);
        try
        {
            return await _apiClient.FetchAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.Failed);
        }
    }

    private async Task<ClaudeLocalUsage?> ReadLocalUsageAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            return await _localUsageSource.ReadAsync(now, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            PrivacySafeDiagnostics.WriteFailure("claude local usage read failed", ex);
            return null;
        }
    }

    private static async Task<T> TryAsync<T>(
        Func<Task<T>> action,
        T fallback,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            PrivacySafeDiagnostics.WriteFailure(operation, ex);
            return fallback;
        }
    }

    internal static string? BuildStatusMessage(
        ClaudeUsageFetchStatus status,
        SyncStatus syncStatus,
        bool hasLocalUsage,
        string? detail = null)
    {
        if (status == ClaudeUsageFetchStatus.Success)
        {
            return hasLocalUsage
                ? null
                : "Rate limits are live; local Claude Code usage logs were unavailable.";
        }

        var reason = status switch
        {
            ClaudeUsageFetchStatus.NotSignedIn => string.IsNullOrWhiteSpace(detail)
                ? "Claude Code sign-in was not found. Run `claude` and sign in with your Claude subscription, then sync again."
                : $"Claude Code sign-in was not found. Checked: {detail}. Run `claude` and sign in with your Claude subscription, then sync again.",
            ClaudeUsageFetchStatus.SignInExpired =>
                "The Claude Code sign-in has expired. Open Claude Code once to refresh it, then sync again.",
            ClaudeUsageFetchStatus.RateLimited =>
                "Claude usage was requested too often. PulseMeter will try again on the next sync.",
            _ => "Claude usage limits are unavailable right now. Try syncing again."
        };

        return syncStatus == SyncStatus.Stale
            ? $"{reason} Showing the last confirmed limits."
            : reason;
    }

    private void Publish(UsageSnapshot snapshot)
    {
        var handlers = SnapshotUpdated;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<UsageSnapshot> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, snapshot);
            }
            catch (Exception ex)
            {
                PrivacySafeDiagnostics.WriteFailure("snapshot subscriber failed", ex);
            }
        }
    }
}
