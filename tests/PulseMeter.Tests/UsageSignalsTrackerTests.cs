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
        Assert.InRange(Assert.IsType<double>(forecast.PercentPerHour), 180, 190);
        Assert.Equal(LimitRunwayForecastConfidence.Medium, forecast.Confidence);
        Assert.NotNull(forecast.EarliestExhaustsAtUtc);
        Assert.NotNull(forecast.LatestExhaustsAtUtc);
        Assert.Equal(3, forecast.SampleCount);
        Assert.InRange(Assert.IsType<double>(forecast.ExhaustionProbabilityBeforeReset), 0.90, 1);
        Assert.Equal(13, Assert.IsAssignableFrom<IReadOnlyList<LimitRunwayProjectionPoint>>(forecast.ProjectionPoints).Count);
        Assert.All(
            forecast.ProjectionPoints!,
            point => Assert.True(
                point.LowerUsedPercent <= point.ExpectedUsedPercent
                && point.ExpectedUsedPercent <= point.UpperUsedPercent));
        var firstFuturePoint = forecast.ProjectionPoints![1];
        var expectedFromPosteriorMean = forecast.UsedPercent
            + (Assert.IsType<double>(forecast.PercentPerHour)
               * (firstFuturePoint.Timestamp - now.AddMinutes(10)).TotalHours);
        Assert.Equal(expectedFromPosteriorMean, firstFuturePoint.ExpectedUsedPercent, precision: 6);
    }

    [Fact]
    public void Observe_WeeklyProjectionContinuesThroughItsForecastWindow()
    {
        var start = new DateTimeOffset(2026, 7, 18, 9, 0, 0, TimeSpan.Zero);
        var reset = start.AddDays(7);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        tracker.Observe(Snapshot(start, usedPercent: 10, resetsAt: reset, windowMinutes: 10_080), start);
        tracker.Observe(Snapshot(start.AddHours(3), usedPercent: 15, resetsAt: reset, windowMinutes: 10_080), start.AddHours(3));
        var signals = tracker.Observe(
            Snapshot(start.AddHours(6), usedPercent: 20, resetsAt: reset, windowMinutes: 10_080),
            start.AddHours(6));

        var forecast = Assert.Single(signals.RunwayForecasts);
        var likelyLimit = Assert.IsType<DateTimeOffset>(forecast.ExhaustsAtUtc);
        var projection = Assert.IsAssignableFrom<IReadOnlyList<LimitRunwayProjectionPoint>>(forecast.ProjectionPoints);

        Assert.True(likelyLimit > start.AddHours(30));
        Assert.True(projection[^1].Timestamp >= likelyLimit);
        Assert.Equal(100, projection[^1].ExpectedUsedPercent);
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
        Assert.InRange(Assert.IsType<double>(forecast.ExhaustionProbabilityBeforeReset), 0, 0.50);
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
    public void Observe_DoesNotTreatOneTerminalBurstAsSustainedPace()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(1);

        tracker.Observe(Snapshot(now, usedPercent: 10, resetsAt: resetAt, windowMinutes: 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(10), usedPercent: 10, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(10));
        var accelerated = tracker.Observe(Snapshot(now.AddMinutes(12), usedPercent: 20, resetsAt: resetAt, windowMinutes: 300), now.AddMinutes(12));

        Assert.Empty(accelerated.RunwaySignals);
        var forecast = Assert.Single(accelerated.RunwayForecasts);
        Assert.False(forecast.IsActionable);
        Assert.True(forecast.PercentPerHour > 0);
        Assert.Equal(LimitRunwayForecastConfidence.Low, forecast.Confidence);
        Assert.InRange(Assert.IsType<double>(forecast.ExhaustionProbabilityBeforeReset), 0, 1);
        Assert.NotNull(forecast.EarliestExhaustsAtUtc);
        Assert.NotNull(forecast.LatestExhaustsAtUtc);
    }

    [Fact]
    public void Observe_LongIdlePlateauLowersDiscountedPaceAndResetRisk()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(2);

        tracker.Observe(Snapshot(now, 20, resetAt, 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(5), 30, resetAt, 300), now.AddMinutes(5));
        var burst = tracker.Observe(Snapshot(now.AddMinutes(10), 40, resetAt, 300), now.AddMinutes(10));
        tracker.Observe(Snapshot(now.AddMinutes(30), 40, resetAt, 300), now.AddMinutes(30));
        var idle = tracker.Observe(Snapshot(now.AddMinutes(50), 40, resetAt, 300), now.AddMinutes(50));

        var burstForecast = Assert.Single(burst.RunwayForecasts);
        var idleForecast = Assert.Single(idle.RunwayForecasts);
        Assert.True(idleForecast.PercentPerHour < burstForecast.PercentPerHour);
        Assert.True(idleForecast.ExhaustionProbabilityBeforeReset < burstForecast.ExhaustionProbabilityBeforeReset);
        Assert.False(idleForecast.IsActionable);
    }

    [Fact]
    public void Observe_UsesExposureTimeForIrregularRefreshSchedules()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var regular = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var irregular = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        regular.Observe(Snapshot(now, 20, resetAt, 300), now);
        regular.Observe(Snapshot(now.AddMinutes(5), 25, resetAt, 300), now.AddMinutes(5));
        var regularResult = regular.Observe(Snapshot(now.AddMinutes(10), 30, resetAt, 300), now.AddMinutes(10));

        irregular.Observe(Snapshot(now, 20, resetAt, 300), now);
        irregular.Observe(Snapshot(now.AddMinutes(2), 22, resetAt, 300), now.AddMinutes(2));
        var irregularResult = irregular.Observe(Snapshot(now.AddMinutes(10), 30, resetAt, 300), now.AddMinutes(10));

        var regularPace = Assert.IsType<double>(Assert.Single(regularResult.RunwayForecasts).PercentPerHour);
        var irregularPace = Assert.IsType<double>(Assert.Single(irregularResult.RunwayForecasts).PercentPerHour);
        Assert.InRange(Math.Abs(regularPace - irregularPace), 0, 0.1);
    }

    [Fact]
    public void Observe_RoundedPlateauDoesNotCreateFalseExhaustionRisk()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        var resetAt = now.AddHours(2);

        tracker.Observe(Snapshot(now, 20.1, resetAt, 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(3), 20.3, resetAt, 300), now.AddMinutes(3));
        var signals = tracker.Observe(Snapshot(now.AddMinutes(6), 20.4, resetAt, 300), now.AddMinutes(6));

        var forecast = Assert.Single(signals.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.Stable, forecast.State);
        Assert.Equal(0, forecast.PercentPerHour);
        Assert.Equal(0, forecast.ExhaustionProbabilityBeforeReset);
        Assert.Empty(signals.RunwaySignals);
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
        Assert.Equal("5h usage increased from 50% to 70% in 6m.", idleDrainDetected.IdleDrainIncident.SummaryText);
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
        Assert.Equal("5h usage increased from 60% to 70% in 6m.", idleObservation.IdleDrainIncident.SummaryText);
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
        Assert.Equal("5h usage increased from 82% to 86% in 11m.", detected.IdleDrainIncident.SummaryText);
        Assert.Equal("5h usage increased from 82% to 86% in 11m.", noisySync.IdleDrainIncident?.SummaryText);
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
                Assert.Equal(UsageAttentionSignalKind.Sync, signal.Kind);
                Assert.Equal("Live data is stale", signal.Title);
            },
            signal =>
            {
                Assert.Equal("LIMIT", signal.BadgeText);
                Assert.Equal(UsageAttentionSignalKind.RateLimit, signal.Kind);
                Assert.Equal("Weekly window is low", signal.Title);
                Assert.Contains("8% left", signal.Detail);
                Assert.Equal("Usage|10080", signal.ScopeId);
            },
            signal =>
            {
                Assert.Equal("CREDIT", signal.BadgeText);
                Assert.Equal(UsageAttentionSignalKind.ResetCredit, signal.Kind);
                Assert.Equal("Reset credit expires soon", signal.Title);
            },
            signal =>
            {
                Assert.Equal("TODAY", signal.BadgeText);
                Assert.Equal(UsageAttentionSignalKind.DailyUsage, signal.Kind);
                Assert.Equal("Today is above usual", signal.Title);
            },
            signal =>
            {
                Assert.Equal("PROJECT", signal.BadgeText);
                Assert.Equal(UsageAttentionSignalKind.ProjectUsage, signal.Kind);
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
        var forecast = Assert.Single(signals.RunwayForecasts);
        Assert.Equal(LimitRunwayForecastState.AtRisk, forecast.State);
        Assert.Equal(LimitRunwayForecastConfidence.Medium, forecast.Confidence);
        Assert.True(forecast.EarliestExhaustsAtUtc < forecast.ExhaustsAtUtc);
        Assert.True(forecast.ExhaustsAtUtc < forecast.LatestExhaustsAtUtc);
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
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.Idle);
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.Runway);
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.RateLimit);
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.ResetCredit);
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.DailyUsage);
        Assert.Contains(signals.AttentionSignals, signal => signal.Kind == UsageAttentionSignalKind.ProjectUsage);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.AtRisk);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.OnTrack);
        Assert.Contains(signals.RunwayForecasts, forecast => forecast.State == LimitRunwayForecastState.Exhausted);
    }

    [Fact]
    public void Observe_RestoresRunwayForecastAcrossTrackerInstances()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var store = new InMemoryRunwayObservationStateStore();
        var first = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        first.Observe(Snapshot(now, 20, resetAt, 300), now);
        first.Observe(Snapshot(now.AddMinutes(2), 30, resetAt, 300), now.AddMinutes(2));
        first.Observe(Snapshot(now.AddMinutes(4), 40, resetAt, 300), now.AddMinutes(4));
        first.Observe(Snapshot(now.AddMinutes(5), 45, resetAt, 300), now.AddMinutes(5));

        var restarted = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var resumed = restarted.Observe(Snapshot(now.AddMinutes(6), 50, resetAt, 300), now.AddMinutes(6));

        var forecast = Assert.Single(resumed.RunwayForecasts);
        Assert.True(forecast.SampleCount >= 3);
        Assert.NotEqual(LimitRunwayForecastState.Learning, forecast.State);
    }

    [Theory]
    [InlineData(300, 4, 2)]
    [InlineData(10_080, 25, 23)]
    public void Observe_RestoresFullTrendWindowWhileForecastKeepsRecentHorizon(
        int windowMinutes,
        int staleAgeHours,
        int recentAgeHours)
    {
        var now = new DateTimeOffset(2026, 7, 19, 15, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(2);
        var isWeekly = windowMinutes >= 10_080;
        var windowLabel = isWeekly ? "Weekly" : "5h Window";
        var forecastWindowLabel = isWeekly ? "7-Day Usage" : "5h";
        var bucketId = $"codex|{windowMinutes}";
        var store = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(
                RunwayObservationStateStore.CurrentSchemaVersion,
                [
                    new RunwayObservationSample(bucketId, "codex", "General", windowLabel, forecastWindowLabel, windowMinutes, 40, resetAt, now.AddHours(-staleAgeHours)),
                    new RunwayObservationSample(bucketId, "codex", "General", windowLabel, forecastWindowLabel, windowMinutes, 45, resetAt, now.AddHours(-recentAgeHours)),
                    new RunwayObservationSample(bucketId, "codex", "General", windowLabel, forecastWindowLabel, windowMinutes, 50, resetAt, now.AddHours(-1))
                ])
        };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        var resumed = tracker.Observe(Snapshot(now, 55, resetAt, windowMinutes), now);

        var trend = Assert.Single(resumed.UsageTrends);
        Assert.Equal(4, trend.Points.Count);
        Assert.Equal(now.AddHours(-staleAgeHours), trend.Points[0].ObservedAtUtc);
        Assert.Equal([40d, 45d, 50d, 55d], trend.Points.Select(point => point.UsedPercent));
        Assert.Equal(3, Assert.Single(resumed.RunwayForecasts).SampleCount);
    }

    [Fact]
    public void Observe_RestoresWeeklyChartHistoryOlderThanForecastHorizon()
    {
        var now = new DateTimeOffset(2026, 7, 19, 15, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(2);
        var store = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(
                RunwayObservationStateStore.CurrentSchemaVersion,
                [
                    new RunwayObservationSample("codex|10080", "codex", "General", "Weekly", "7-Day Usage", 10_080, 40, resetAt, now.AddHours(-26)),
                    new RunwayObservationSample("codex|10080", "codex", "General", "Weekly", "7-Day Usage", 10_080, 45, resetAt, now.AddHours(-25))
                ])
        };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        var resumed = tracker.Observe(Snapshot(now, 55, resetAt, 10_080), now);

        var trend = Assert.Single(resumed.UsageTrends);
        Assert.Equal(3, trend.Points.Count);
        Assert.Equal(now.AddHours(-26), trend.Points[0].ObservedAtUtc);
        Assert.Equal([40d, 45d, 55d], trend.Points.Select(point => point.UsedPercent));
        Assert.Equal(1, Assert.Single(resumed.RunwayForecasts).SampleCount);
    }

    [Fact]
    public void Observe_BackfillsMissingChartHistoryFromLocalRateLimitEvents()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(4);
        var store = new InMemoryRunwayObservationStateStore();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var history = new[]
        {
            new RateLimitHistoryPoint("codex", 10_080, 70, resetAt, now.AddHours(-3)),
            new RateLimitHistoryPoint("codex", 10_080, 74, resetAt, now.AddHours(-1))
        };

        var signals = tracker.Observe(Snapshot(now, 75, resetAt, 10_080, history), now);

        var trend = Assert.Single(signals.UsageTrends);
        Assert.Equal([70d, 74d, 75d], trend.Points.Select(point => point.UsedPercent));
        Assert.Equal(now.AddHours(-3), trend.Points[0].ObservedAtUtc);
        Assert.Equal(3, store.State!.Samples!.Count);

        var restarted = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var resumed = restarted.Observe(Snapshot(now.AddMinutes(1), 75, resetAt, 10_080), now.AddMinutes(1));

        Assert.Equal(now.AddHours(-3), Assert.Single(resumed.UsageTrends).Points[0].ObservedAtUtc);
    }

    [Fact]
    public void Observe_PreservesActiveWindowHistoryWhenBucketTemporarilyDisappears()
    {
        var now = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(5);
        var store = new InMemoryRunwayObservationStateStore();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        tracker.Observe(Snapshot(now, 68, resetAt, 10_080), now);
        tracker.Observe(
            new UsageSnapshot { SyncStatus = SyncStatus.Live, LastUpdatedUtc = now.AddMinutes(10) },
            now.AddMinutes(10));
        var resumed = tracker.Observe(
            Snapshot(now.AddMinutes(20), 74, resetAt, 10_080),
            now.AddMinutes(20));

        var trend = Assert.Single(resumed.UsageTrends);
        Assert.Equal([68d, 74d], trend.Points.Select(point => point.UsedPercent));
        Assert.Equal(now, trend.Points[0].ObservedAtUtc);

        var restarted = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var afterRestart = restarted.Observe(
            Snapshot(now.AddMinutes(21), 75, resetAt, 10_080),
            now.AddMinutes(21));

        var restartedTrend = Assert.Single(afterRestart.UsageTrends);
        Assert.Equal([68d, 74d, 75d], restartedTrend.Points.Select(point => point.UsedPercent));
        Assert.Equal(now, restartedTrend.Points[0].ObservedAtUtc);
    }

    [Fact]
    public void Observe_IgnoresRegressiveReadingWithoutErasingActiveWindowHistory()
    {
        var now = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(5);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        tracker.Observe(Snapshot(now, 68, resetAt, 10_080), now);
        tracker.Observe(Snapshot(now.AddMinutes(6), 74, resetAt, 10_080), now.AddMinutes(6));
        tracker.Observe(Snapshot(now.AddMinutes(12), 70, resetAt, 10_080), now.AddMinutes(12));
        var recovered = tracker.Observe(
            Snapshot(now.AddMinutes(18), 75, resetAt, 10_080),
            now.AddMinutes(18));

        var trend = Assert.Single(recovered.UsageTrends);
        Assert.Equal([68d, 74d, 75d], trend.Points.Select(point => point.UsedPercent));
        Assert.Equal(now, trend.Points[0].ObservedAtUtc);
    }

    [Fact]
    public void Observe_PersistsMeasurementGapAndExcludesItFromEvidenceDuration()
    {
        var now = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(5);
        var store = new InMemoryRunwayObservationStateStore();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        tracker.Observe(Snapshot(now, 68, resetAt, 10_080), now);
        tracker.Observe(Snapshot(now.AddMinutes(5), 69, resetAt, 10_080), now.AddMinutes(5));
        var resumedTracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var resumed = resumedTracker.Observe(
            Snapshot(now.AddMinutes(30), 74, resetAt, 10_080),
            now.AddMinutes(30));

        var gap = Assert.Single(Assert.Single(resumed.UsageTrends).MeasurementGaps);
        Assert.Equal(now.AddMinutes(5), gap.StartedAtUtc);
        Assert.Equal(now.AddMinutes(30), gap.EndedAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(5), Assert.Single(resumed.RunwayForecasts).ObservationDuration);
        Assert.Contains(store.State!.Samples!, sample => sample!.StartsAfterMeasurementGap);

        var restarted = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var afterRestart = restarted.Observe(
            Snapshot(now.AddMinutes(31), 75, resetAt, 10_080),
            now.AddMinutes(31));

        Assert.Single(Assert.Single(afterRestart.UsageTrends).MeasurementGaps);
        Assert.Equal(TimeSpan.FromMinutes(6), Assert.Single(afterRestart.RunwayForecasts).ObservationDuration);
    }

    [Fact]
    public void Observe_DoesNotCreateMeasurementGapDuringRegularFlatSampling()
    {
        var now = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(5);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));

        tracker.Observe(Snapshot(now, 74, resetAt, 10_080), now);
        tracker.Observe(Snapshot(now.AddMinutes(5), 74, resetAt, 10_080), now.AddMinutes(5));
        tracker.Observe(Snapshot(now.AddMinutes(10), 74, resetAt, 10_080), now.AddMinutes(10));
        var current = tracker.Observe(
            Snapshot(now.AddMinutes(15), 74, resetAt, 10_080),
            now.AddMinutes(15));

        Assert.Empty(Assert.Single(current.UsageTrends).MeasurementGaps);
    }

    [Fact]
    public void Observe_RetainsRegularWeeklyCheckpointsAcrossTwentyFourHours()
    {
        var start = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);
        var resetAt = start.AddDays(7);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero));
        UsageSignalsSnapshot current = UsageSignalsSnapshot.Empty;

        for (var index = 0; index <= 288; index++)
        {
            var observedAt = start.AddMinutes(index * 5);
            current = tracker.Observe(Snapshot(observedAt, 74, resetAt, 10_080), observedAt);
        }

        var trend = Assert.Single(current.UsageTrends);
        Assert.InRange(trend.Points.Count, 140, 150);
        Assert.Equal(start, trend.Points[0].ObservedAtUtc);
        Assert.Equal(start.AddHours(24), trend.Points[^1].ObservedAtUtc);

        var forecast = Assert.Single(current.RunwayForecasts);
        Assert.InRange(forecast.SampleCount, 140, 150);
        Assert.Equal(TimeSpan.FromHours(24), forecast.ObservationDuration);
    }

    [Fact]
    public void Observe_FirstLiveSampleAfterRestoreRebasesIdleDrain()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var store = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(
                RunwayObservationStateStore.CurrentSchemaVersion,
                [new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 40, resetAt, now.AddMinutes(-6))])
        };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.FromMinutes(10)), store);

        var signals = tracker.Observe(Snapshot(now, 55, resetAt, 300), now);

        Assert.Null(signals.IdleDrainIncident);
    }

    [Fact]
    public void Observe_IgnoresExpiredAndInvalidRestoredRunwayState()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(
                RunwayObservationStateStore.CurrentSchemaVersion,
                [
                    new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 20, now.AddMinutes(-1), now.AddMinutes(-2)),
                    new RunwayObservationSample("bad|300", "bad", "General", "5h Window", "5h", 300, double.NaN, now.AddHours(1), now.AddMinutes(-2))
                ])
        };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        var signals = tracker.Observe(Snapshot(now, 30, now.AddHours(1), 300), now);

        Assert.Equal(1, Assert.Single(signals.RunwayForecasts).SampleCount);
    }

    [Fact]
    public void Observe_PersistsAtMostSixteenRunwayBuckets()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRunwayObservationStateStore();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var snapshot = new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = Enumerable.Range(0, 17).Select(index => new RateLimitBucket
            {
                LimitId = $"limit-{index}",
                Label = "5h Window",
                WindowLabel = "5h",
                WindowDurationMins = 300,
                UsedPercent = 10,
                ResetsAtUtc = now.AddHours(1),
                ResetsAtUnixSeconds = now.AddHours(1).ToUnixTimeSeconds()
            }).ToList()
        };

        tracker.Observe(snapshot, now);

        Assert.NotNull(store.State);
        Assert.NotNull(store.State!.Samples);
        Assert.InRange(store.State.Samples!.Select(sample => sample!.BucketId).Distinct(StringComparer.OrdinalIgnoreCase).Count(), 1, 16);
    }

    [Fact]
    public void Observe_PersistsEveryNewRunwaySampleImmediately()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRunwayObservationStateStore();
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var resetAt = now.AddHours(2);

        tracker.Observe(Snapshot(now, 20, resetAt, 300), now);
        tracker.Observe(Snapshot(now, 20, resetAt, 300), now);
        tracker.Observe(Snapshot(now.AddMinutes(1), 30, resetAt, 300), now.AddMinutes(1));

        Assert.Equal(2, store.SaveCount);
        Assert.Equal(2, store.State!.Samples!.Count);
    }

    [Fact]
    public void Flush_RetriesDirtySamplesAfterImmediateSaveFailures()
    {
        var now = new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddDays(5);
        var store = new InMemoryRunwayObservationStateStore { FailedSaveAttempts = 2 };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        tracker.Observe(Snapshot(now, 20, resetAt, 10_080), now);
        tracker.Observe(Snapshot(now.AddMinutes(1), 30, resetAt, 10_080), now.AddMinutes(1));

        Assert.Equal(2, store.SaveCount);
        Assert.Null(store.State);

        tracker.Flush();

        Assert.Equal(3, store.SaveCount);
        Assert.Equal(2, store.State!.Samples!.Count);

        var restarted = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var resumed = restarted.Observe(
            Snapshot(now.AddMinutes(2), 40, resetAt, 10_080),
            now.AddMinutes(2));

        Assert.Equal([20d, 30d, 40d], Assert.Single(resumed.UsageTrends).Points.Select(point => point.UsedPercent));
    }

    [Fact]
    public void Observe_RetriesDirtyCheckpointImmediatelyAfterFailedSave()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRunwayObservationStateStore { FailedSaveAttempts = 1 };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);
        var snapshot = Snapshot(now, 20, now.AddHours(2), 300);

        tracker.Observe(snapshot, now);
        tracker.Observe(snapshot, now);

        Assert.Equal(2, store.SaveCount);
        Assert.NotNull(store.State);
    }

    [Fact]
    public void Observe_RetriesUnavailableRestoreThenHydratesWithoutOverwriting()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var store = new InMemoryRunwayObservationStateStore
        {
            Results = new Queue<RunwayObservationLoadResult>([
                new(RunwayObservationLoadStatus.Unavailable),
                new(RunwayObservationLoadStatus.Loaded, new RunwayObservationState(
                    RunwayObservationStateStore.CurrentSchemaVersion,
                    [new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 20, resetAt, now.AddMinutes(-4))]))
            ])
        };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        tracker.Observe(Snapshot(now, 25, resetAt, 300), now);
        Assert.Equal(0, store.SaveCount);
        var resumed = tracker.Observe(Snapshot(now.AddMinutes(2), 30, resetAt, 300), now.AddMinutes(2));

        Assert.True(Assert.Single(resumed.RunwayForecasts).SampleCount >= 2);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void Observe_StopsAfterThreeUnavailableRestoreAttemptsWithoutPersisting()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRunwayObservationStateStore { AlwaysUnavailable = true };
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), store);

        for (var index = 0; index < 4; index++)
        {
            tracker.Observe(Snapshot(now.AddMinutes(index), 20 + index, now.AddHours(2), 300), now.AddMinutes(index));
        }

        Assert.Equal(3, store.LoadCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Observe_IgnoresMissingOrNullPersistedSamplesWithoutCrashing()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var resetAt = now.AddHours(2);
        var missingSamples = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(RunwayObservationStateStore.CurrentSchemaVersion, null)
        };
        var nullElement = new InMemoryRunwayObservationStateStore
        {
            State = new RunwayObservationState(RunwayObservationStateStore.CurrentSchemaVersion, [null])
        };

        var first = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), missingSamples);
        var second = new UsageSignalsTracker(new FixedUserIdleTimeProvider(TimeSpan.Zero), nullElement);

        Assert.Single(first.Observe(Snapshot(now, 20, resetAt, 300), now).RunwayForecasts);
        Assert.Single(second.Observe(Snapshot(now, 20, resetAt, 300), now).RunwayForecasts);
    }

    private static UsageSnapshot Snapshot(
        DateTimeOffset now,
        double usedPercent,
        DateTimeOffset resetsAt,
        int windowMinutes,
        IReadOnlyList<RateLimitHistoryPoint>? rateLimitHistory = null)
    {
        return new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = now,
            Source = "AppServer",
            RateLimitHistory = rateLimitHistory ?? Array.Empty<RateLimitHistoryPoint>(),
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

    private sealed class InMemoryRunwayObservationStateStore : IRunwayObservationStateStore
    {
        public RunwayObservationState? State { get; set; }

        public Queue<RunwayObservationLoadResult>? Results { get; set; }

        public bool AlwaysUnavailable { get; set; }

        public int LoadCount { get; private set; }

        public int SaveCount { get; private set; }

        public int FailedSaveAttempts { get; set; }

        public RunwayObservationLoadResult Load()
        {
            LoadCount++;
            if (AlwaysUnavailable)
            {
                return new RunwayObservationLoadResult(RunwayObservationLoadStatus.Unavailable);
            }

            return Results is { Count: > 0 }
                ? Results.Dequeue()
                : State is null
                    ? new RunwayObservationLoadResult(RunwayObservationLoadStatus.Missing)
                    : new RunwayObservationLoadResult(RunwayObservationLoadStatus.Loaded, State);
        }

        public bool Save(RunwayObservationState state)
        {
            SaveCount++;
            if (FailedSaveAttempts > 0)
            {
                FailedSaveAttempts--;
                return false;
            }

            State = state;
            return true;
        }
    }
}
