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
        tracker.Observe(Snapshot(now.AddMinutes(5), usedPercent: 55, resetsAt: now.AddHours(1), windowMinutes: 300), now.AddMinutes(5));
        var second = tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 70, resetsAt: now.AddHours(1), windowMinutes: 300), now.AddMinutes(10));

        Assert.Empty(first.RunwaySignals);
        var learning = Assert.Single(first.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.Learning, learning.State);
        var signal = Assert.Single(second.RunwaySignals);
        Assert.Equal("codex", signal.LimitKey);
        Assert.Equal("5h Window", signal.WindowLabel);
        Assert.Equal("Runway: about 10m at current pace", signal.HintText);
        Assert.Equal("Projected to run out before reset", signal.Title);
        Assert.Contains("before the 5h reset", signal.Detail);
        var forecast = Assert.Single(second.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.AtRisk, forecast.State);
        Assert.True(forecast.IsActionable);
        Assert.Equal(180, Assert.IsType<double>(forecast.PercentPerHour), precision: 6);
        Assert.Equal(LimitRunwayForecastConfidence.Medium, forecast.Confidence);
        Assert.NotNull(forecast.EarliestExhaustsAtUtc);
        Assert.NotNull(forecast.LatestExhaustsAtUtc);
        Assert.Equal(3, forecast.SampleCount);
    }

    [Fact]
    public void Observe_SuppressesRunwayWhenResetArrivesFirst()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        tracker.Observe(Snapshot(now, usedPercent: 20, resetsAt: now.AddMinutes(20), windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(5), usedPercent: 22.5, resetsAt: now.AddMinutes(20), windowMinutes: 300), now.AddMinutes(5));
        var signals = tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 25, resetsAt: now.AddMinutes(20), windowMinutes: 300), now.AddMinutes(10));

        Assert.Empty(signals.RunwaySignals);
        var forecast = Assert.Single(signals.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.OnTrack, forecast.State);
        Assert.False(forecast.IsActionable);
    }

    [Fact]
    public void Observe_ReturnsStableForecastWhenUsageDoesNotMove()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(2);

        tracker.Observe(Snapshot(now, usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(2.5), usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(2.5));
        var signals = tracker.Observe(Snapshot(now.AddMinutes(5), usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(5));

        var forecast = Assert.Single(signals.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.Stable, forecast.State);
        Assert.Equal(0, forecast.PercentPerHour);
        Assert.False(forecast.IsActionable);
        Assert.Equal(LimitRunwayForecastConfidence.Low, forecast.Confidence);
        Assert.Equal(3, forecast.SampleCount);
    }

    [Fact]
    public void Observe_UsesLatestEligibleSampleSoSuddenAccelerationIsNotAveragedAway()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 10, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 10, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(10));
        var accelerated = tracker.Observe(Snapshot(now.AddMinutes(12), usedPercent: 20, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(12));

        var signal = Assert.Single(accelerated.RunwaySignals);
        Assert.True(signal.TimeToExhaustion < TimeSpan.FromHours(1));
        var forecast = Assert.Single(accelerated.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.AtRisk, forecast.State);
        Assert.True(forecast.IsActionable);
        Assert.True(forecast.PercentPerHour > 0);
        Assert.Equal(LimitRunwayForecastConfidence.Medium, forecast.Confidence);
        Assert.NotNull(forecast.EarliestExhaustsAtUtc);
        Assert.NotNull(forecast.LatestExhaustsAtUtc);
    }

    [Fact]
    public void Observe_IgnoresBucketsWhoseResetAlreadyPassed()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        var signals = tracker.Observe(
            Snapshot(now, usedPercent: 80, resetsAt: now.AddMinutes(-1), windowMinutes: 300),
            now);

        Assert.Empty(signals.RunwayForecasts);
        Assert.Empty(signals.RunwaySignals);
    }

    [Fact]
    public void Observe_AccumulatesNinetySecondRefreshesIntoRunwayAndIdleDrainWindows()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new SequencedUserIdleTimeProvider(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(90),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(6)));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 50, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddSeconds(90), usedPercent: 55, resetsAt: resetAt, windowMinutes: 300), now.AddSeconds(90));
        var runwayDetected = tracker.Observe(Snapshot(now.AddMinutes(3), usedPercent: 60, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(3));
        var idleDrainDetected = tracker.Observe(Snapshot(now.AddMinutes(6), usedPercent: 70, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(6));

        Assert.Single(runwayDetected.RunwaySignals);
        Assert.NotNull(idleDrainDetected.IdleDrainIncident);
        Assert.Equal("Usage moved while idle: 50% -> 70% in 6m", idleDrainDetected.IdleDrainIncident.SummaryText);
    }

    [Fact]
    public void Observe_RebasesIdleDrainAfterActiveUseBeforeAttributingNewIdleUsage()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new SequencedUserIdleTimeProvider(
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(6)));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 40, resetsAt: resetAt, windowMinutes: 300), now);
        var activeObservation = tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 60, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(10));
        var idleObservation = tracker.Observe(Snapshot(now.AddMinutes(16), usedPercent: 70, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(16));

        Assert.Null(activeObservation.IdleDrainIncident);
        Assert.NotNull(idleObservation.IdleDrainIncident);
        Assert.Equal("Usage moved while idle: 60% -> 70% in 6m", idleObservation.IdleDrainIncident.SummaryText);
    }

    [Fact]
    public void Observe_RequiresThreeSamplesAcrossLongerObservationGap()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 50, resetsAt: resetAt, windowMinutes: 300), now);
        var afterStaleGap = tracker.Observe(Snapshot(now.AddMinutes(20), usedPercent: 70, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(20));
        var recentObservation = tracker.Observe(Snapshot(now.AddMinutes(23), usedPercent: 85, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(23));

        Assert.Empty(afterStaleGap.RunwaySignals);
        Assert.Single(recentObservation.RunwaySignals);
        Assert.Null(recentObservation.IdleDrainIncident);
    }

    [Fact]
    public void Observe_DoesNotAlertOnSmallNoisyMovementWithLowConfidence()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(2), usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(2));
        tracker.Observe(Snapshot(now.AddMinutes(4), usedPercent: 35, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(4));
        var signals = tracker.Observe(Snapshot(now.AddMinutes(6), usedPercent: 36, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(6));

        Assert.Empty(signals.RunwaySignals);
        var forecast = Assert.Single(signals.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastConfidence.Low, forecast.Confidence);
        Assert.False(forecast.IsActionable);
    }

    [Fact]
    public void Observe_ReportsHighConfidenceForConsistentLongerHistory()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(3);
        UsageSignalsSnapshot? signals = null;

        for (var index = 0; index < 8; index++)
        {
            var observedAt = now.AddMinutes(index * 3);
            signals = tracker.Observe(
                Snapshot(observedAt, usedPercent: 20 + (index * 3), resetsAt: resetAt, windowMinutes: 300),
                observedAt);
        }

        var forecast = Assert.Single(Assert.IsType<UsageSignalsSnapshot>(signals).RunwayForecasts);
        Assert.Equal(LimitRunwayForecastConfidence.High, forecast.Confidence);
        Assert.Equal(8, forecast.SampleCount);
        Assert.NotNull(forecast.EarliestExhaustsAtUtc);
        Assert.NotNull(forecast.LatestExhaustsAtUtc);
    }

    [Fact]
    public void Observe_DoesNotCreateRunwayOrIdleDrainFromOneLiveSample()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.FromMinutes(10)));

        var signals = tracker.Observe(
            Snapshot(now, usedPercent: 90, resetsAt: now.AddHours(1), windowMinutes: 300),
            now);

        Assert.Empty(signals.RunwaySignals);
        Assert.Null(signals.IdleDrainIncident);
    }

    [Fact]
    public void Observe_KeepsIdleDrainIncidentAcrossNoisySyncsUntilReset()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.FromMinutes(11)));
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
        var tracker = new UsageSignalsTracker(new SequencedUserIdleTimeProvider(
            TimeSpan.Zero,
            TimeSpan.FromMinutes(11),
            TimeSpan.FromMinutes(20),
            TimeSpan.Zero,
            TimeSpan.FromMinutes(9)));
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
        Assert.Equal(LimitRunwayForecastState.AtRisk, Assert.Single(signals.RunwayForecasts).State);
    }

    [Fact]
    public async Task Observe_RealMockSnapshotShowsEveryAttentionCategory()
    {
        var snapshot = await new MockCodexUsageService().GetSnapshotAsync();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        var signals = tracker.Observe(snapshot, snapshot.LastUpdatedUtc ?? DateTimeOffset.UtcNow);
        var badges = signals.AttentionSignals.Select(signal => signal.BadgeText).ToHashSet();

        Assert.True(signals.ShowAllAttentionSignals);
        Assert.Contains("IDLE", badges);
        Assert.Contains("RUNWAY", badges);
        Assert.Contains("LIMIT", badges);
        Assert.Contains("CREDIT", badges);
        Assert.Contains("TODAY", badges);
        Assert.Contains("PROJECT", badges);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.AtRisk);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.OnTrack);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.Exhausted);
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

    private sealed class SequencedUserIdleTimeProvider(params TimeSpan[] idleTimes) : IUserIdleTimeProvider
    {
        private readonly Queue<TimeSpan> _idleTimes = new(idleTimes);

        public TimeSpan GetIdleTime()
        {
            return _idleTimes.Dequeue();
        }
    }
}
