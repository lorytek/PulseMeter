using PulseMeter.Platform.Windows;

namespace PulseMeter.Tests;

public sealed class UsageSignalsTrackerTests
{
    [Fact]
    public void Observe_ReturnsRunwayWhenFiveHourLimitWouldRunOutBeforeReset()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        var first = tracker.Observe(Snapshot(now, usedPercent: 40, resetsAt: now.AddHours(1), windowMinutes: 300), now);
        var second = tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 70, resetsAt: now.AddHours(1), windowMinutes: 300), now.AddMinutes(10));

        Assert.Empty(first.RunwaySignals);
        var signal = Assert.Single(second.RunwaySignals);
        Assert.Equal("codex", signal.LimitKey);
        Assert.Equal("5h Window", signal.WindowLabel);
        Assert.Equal("Runway: about 10m at current pace", signal.HintText);
        Assert.Equal("Projected to run out before reset", signal.Title);
        Assert.Contains("before the 5h reset", signal.Detail);
    }

    [Fact]
    public void Observe_SuppressesRunwayWhenResetArrivesFirst()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        tracker.Observe(Snapshot(now, usedPercent: 20, resetsAt: now.AddMinutes(20), windowMinutes: 300), now);
        var signals = tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 25, resetsAt: now.AddMinutes(20), windowMinutes: 300), now.AddMinutes(10));

        Assert.Empty(signals.RunwaySignals);
    }

    [Fact]
    public void Observe_KeepsIdleDrainIncidentAcrossNoisySyncsUntilReset()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.FromMinutes(7)));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 82, resetsAt: resetAt, windowMinutes: 300), now);
        var detected = tracker.Observe(Snapshot(now.AddMinutes(11), usedPercent: 86, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(11));
        var noisySync = tracker.Observe(new UsageSnapshot { SyncStatus = SyncStatus.Stale }, now.AddMinutes(12));
        var afterReset = tracker.Observe(Snapshot(now.AddMinutes(61), usedPercent: 2, resetsAt: now.AddHours(6), windowMinutes: 300), now.AddMinutes(61));

        Assert.NotNull(detected.IdleDrainIncident);
        Assert.Equal("Usage moved while idle: 82% -> 86% in 11m", detected.IdleDrainIncident.SummaryText);
        Assert.Equal("Usage moved while idle: 82% -> 86% in 11m", noisySync.IdleDrainIncident?.SummaryText);
        Assert.Null(afterReset.IdleDrainIncident);
    }

    [Fact]
    public void DismissIdleDrain_HidesCurrentIncidentUntilBucketResets()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.FromMinutes(9)));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 82, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(11), usedPercent: 86, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(11));

        tracker.DismissIdleDrain();
        var dismissed = tracker.Observe(Snapshot(now.AddMinutes(20), usedPercent: 90, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(20));
        var afterReset = tracker.Observe(Snapshot(now.AddMinutes(61), usedPercent: 1, resetsAt: now.AddHours(6), windowMinutes: 300), now.AddMinutes(61));
        var newIncident = tracker.Observe(Snapshot(now.AddMinutes(70), usedPercent: 5, resetsAt: now.AddHours(6), windowMinutes: 300), now.AddMinutes(70));

        Assert.Null(dismissed.IdleDrainIncident);
        Assert.Null(afterReset.IdleDrainIncident);
        Assert.NotNull(newIncident.IdleDrainIncident);
    }

    [Fact]
    public void Observe_ReturnsAllNeedsAttentionSignalsFromSnapshot()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        var signals = tracker.Observe(
            new UsageSnapshot
            {
                SyncStatus = SyncStatus.Stale,
                StatusMessage = "Using cached usage.",
                Buckets =
                [
                    new RateLimitBucket
                    {
                        Label = "General weekly",
                        WindowDurationMins = 10_080,
                        UsedPercent = 92,
                        ResetsAtUtc = now.AddHours(10),
                        ResetCountdown = CountdownFormatter.FormatResetCountdown(now.AddHours(10).ToUnixTimeSeconds(), now)
                    }
                ],
                ResetCredits =
                [
                    new ResetCreditSnapshot(now.AddDays(-25), now.AddHours(18), "available")
                ],
                DailyBuckets =
                [
                    Bucket(now.AddDays(-3), 100),
                    Bucket(now.AddDays(-2), 120),
                    Bucket(now.AddDays(-1), 100),
                    Bucket(now, 250)
                ],
                ProjectUsageRows =
                [
                    new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 6_000_000, 6_000_000, 8, 61)
                ]
            },
            now);

        Assert.Collection(
            signals.AttentionSignals,
            signal =>
            {
                Assert.Equal("SYNC", signal.BadgeText);
                Assert.Equal("Live data is stale", signal.Title);
            },
            signal =>
            {
                Assert.Equal("LIMIT", signal.BadgeText);
                Assert.Equal("Weekly window is low", signal.Title);
                Assert.Contains("8% left", signal.Detail);
            },
            signal =>
            {
                Assert.Equal("CREDIT", signal.BadgeText);
                Assert.Equal("Reset credit expires soon", signal.Title);
            },
            signal =>
            {
                Assert.Equal("TODAY", signal.BadgeText);
                Assert.Equal("Today is above usual", signal.Title);
            },
            signal =>
            {
                Assert.Equal("PROJECT", signal.BadgeText);
                Assert.Equal("PulseMeter leads recent usage", signal.Title);
            });
    }

    [Fact]
    public void Observe_ReturnsRunwayAndIdleDrainSignalsForMockShowcaseSnapshot()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        var signals = tracker.Observe(
            new UsageSnapshot
            {
                SyncStatus = SyncStatus.Mocked,
                Source = "Mock",
                LastUpdatedUtc = now,
                Buckets =
                [
                    new RateLimitBucket
                    {
                        LimitId = "codex",
                        LimitName = "General",
                        GroupLabel = "General",
                        WindowLabel = "5h",
                        Label = "5h Window",
                        UsedPercent = 96,
                        WindowDurationMins = 300,
                        ResetsAtUtc = now.AddHours(2),
                        ResetsAtUnixSeconds = now.AddHours(2).ToUnixTimeSeconds()
                    }
                ]
            },
            now);

        var runway = Assert.Single(signals.RunwaySignals);
        Assert.Equal("Projected to run out before reset", runway.Title);
        Assert.Contains("demo pace", runway.Detail);
        Assert.NotNull(signals.IdleDrainIncident);
        Assert.Contains("Mock idle-drain demo", signals.IdleDrainIncident.DiagnosticText);
        Assert.Contains(signals.AttentionSignals, signal => signal.BadgeText == "RUNWAY");
        Assert.Contains(signals.AttentionSignals, signal => signal.BadgeText == "IDLE");
    }

    private static UsageSnapshot Snapshot(DateTimeOffset now, double usedPercent, DateTimeOffset resetsAt, int windowMinutes)
    {
        return new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = now,
            Source = "AppServer",
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    LimitName = "General",
                    GroupLabel = "General",
                    WindowLabel = windowMinutes >= 10_080 ? "7-Day Usage" : "5h",
                    Label = windowMinutes >= 10_080 ? "Weekly" : "5h Window",
                    UsedPercent = usedPercent,
                    WindowDurationMins = windowMinutes,
                    ResetsAtUtc = resetsAt,
                    ResetsAtUnixSeconds = resetsAt.ToUnixTimeSeconds()
                }
            ]
        };
    }

    private static DailyUsageBucket Bucket(DateTimeOffset date, long tokens)
    {
        return new DailyUsageBucket
        {
            StartDate = DateOnly.FromDateTime(date.LocalDateTime).ToString("yyyy-MM-dd"),
            Tokens = tokens
        };
    }

    private sealed class FixedUserIdleTimeProvider(TimeSpan idleTime) : IUserIdleTimeProvider
    {
        public TimeSpan GetIdleTime()
        {
            return idleTime;
        }
    }
}
