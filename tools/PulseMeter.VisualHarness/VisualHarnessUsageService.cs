using PulseMeter.Slices.UsageCollection.Business;
using PulseMeter.Slices.UsageCollection.Models;

namespace PulseMeter.VisualHarness;

public sealed class VisualHarnessUsageService : IMockUsageService
{
    private readonly MockCodexUsageService _mockService = new() { UseMockMode = true };
    private readonly VisualHarnessScenario _scenario;

    public VisualHarnessUsageService(VisualHarnessScenario scenario)
    {
        _scenario = scenario;
    }

    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public bool UseMockMode
    {
        get => _scenario == VisualHarnessScenario.Healthy;
        set => _mockService.UseMockMode = _scenario == VisualHarnessScenario.Healthy && value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return _mockService.StartAsync(cancellationToken);
    }

    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var mockSnapshot = await _mockService.GetSnapshotAsync(cancellationToken);
        var snapshot = _scenario switch
        {
            VisualHarnessScenario.Unavailable => WithScenario(
                mockSnapshot,
                SyncStatus.Unavailable,
                DateTimeOffset.UtcNow.AddMinutes(-2),
                "The monitored app is not running. Start it, then retry."),
            VisualHarnessScenario.Stale => WithScenario(
                mockSnapshot,
                SyncStatus.Stale,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                "Cached usage is older than expected."),
            _ => mockSnapshot
        };

        SnapshotUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    private static UsageSnapshot WithScenario(
        UsageSnapshot snapshot,
        SyncStatus syncStatus,
        DateTimeOffset lastUpdatedUtc,
        string statusMessage)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = syncStatus,
            LastUpdatedUtc = lastUpdatedUtc,
            Source = "VisualHarness",
            StatusMessage = statusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }
}
