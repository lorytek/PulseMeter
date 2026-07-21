using PulseMeter.Platform.Persistence;

namespace PulseMeter.Tests;

public sealed class BudgetAlertTrackerTests
{
    [Fact]
    public void Observe_ReturnsDailyBudgetCriticalSignal()
    {
        var now = new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero);
        var settings = BudgetAlertSettings.Default with
        {
            DailyTokenBudget = 1_000,
            WarningPercent = 75,
            CriticalPercent = 90
        };
        var tracker = new BudgetAlertTracker();

        var first = tracker.Observe(Snapshot(now, todayTokens: 950), settings, now);
        var rows = Assert.Single(first.Rows);

        var signal = Assert.Single(first.AttentionSignals);
        Assert.Equal("BUDGET", signal.BadgeText);
        Assert.Equal(UsageAttentionSignalKind.DailyUsage, signal.Kind);
        Assert.Equal("Daily token budget is critical", signal.Title);
        Assert.Contains("950 tokens of 1.0K", signal.Detail);
        Assert.Equal("Daily token budget", rows.Label);
        Assert.Equal("Critical", rows.LevelText);
    }

    [Fact]
    public void Observe_ReturnsRateLimitWarningSignal()
    {
        var now = new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var tracker = new BudgetAlertTracker();

        var first = tracker.Observe(
                Snapshot(now, rateLimitUsedPercent: 78, resetsAt: resetAt),
                BudgetAlertSettings.Default,
                now);

        var signal = Assert.Single(first.AttentionSignals);
        Assert.Equal("5h budget warning", signal.Title);
        Assert.Equal(UsageAttentionSignalKind.RateLimit, signal.Kind);
        Assert.Equal("codex|300", signal.ScopeId);
        Assert.Equal("Warning", Assert.Single(first.Rows).LevelText);
    }

    [Fact]
    public void Observe_ReturnsMockRows()
    {
        var now = new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero);
        var tracker = new BudgetAlertTracker();

        var snapshot = tracker.Observe(
            Snapshot(
                now,
                rateLimitUsedPercent: 96,
                resetsAt: now.AddHours(2),
                syncStatus: SyncStatus.Mocked,
                source: "Mock"),
            BudgetAlertSettings.Default,
            now);

        var row = Assert.Single(snapshot.Rows);
        Assert.Equal("Critical", row.LevelText);
        Assert.Equal("5h budget is critical", Assert.Single(snapshot.AttentionSignals).Title);
    }

    [Fact]
    public void Observe_IgnoresDisabledSettingsAndUnavailableSnapshots()
    {
        var now = new DateTimeOffset(2026, 7, 7, 15, 0, 0, TimeSpan.Zero);
        var tracker = new BudgetAlertTracker();

        var disabled = tracker.Observe(
            Snapshot(now, todayTokens: 950),
            BudgetAlertSettings.Default with { IsEnabled = false, DailyTokenBudget = 1_000 },
            now);
        var unavailable = tracker.Observe(
            Snapshot(now, todayTokens: 950, syncStatus: SyncStatus.Unavailable),
            BudgetAlertSettings.Default with { DailyTokenBudget = 1_000 },
            now);

        Assert.Empty(disabled.AttentionSignals);
        Assert.Empty(unavailable.AttentionSignals);
    }

    private static UsageSnapshot Snapshot(
        DateTimeOffset now,
        long? todayTokens = null,
        double? rateLimitUsedPercent = null,
        DateTimeOffset? resetsAt = null,
        SyncStatus syncStatus = SyncStatus.Live,
        string source = "AppServer")
    {
        return new UsageSnapshot
        {
            SyncStatus = syncStatus,
            LastUpdatedUtc = now,
            Source = source,
            DailyBuckets = todayTokens is long tokens
                ? [new DailyUsageBucket { StartDate = DateOnly.FromDateTime(now.LocalDateTime).ToString("yyyy-MM-dd"), Tokens = tokens }]
                : [],
            Buckets = rateLimitUsedPercent is double usedPercent
                ? [
                    new RateLimitBucket
                    {
                        LimitId = "codex",
                        LimitName = "General",
                        GroupLabel = "General",
                        Label = "5h Window",
                        WindowLabel = "5h",
                        WindowDurationMins = 300,
                        UsedPercent = usedPercent,
                        ResetsAtUtc = resetsAt,
                        ResetsAtUnixSeconds = resetsAt?.ToUnixTimeSeconds()
                    }
                ]
                : []
        };
    }
}
