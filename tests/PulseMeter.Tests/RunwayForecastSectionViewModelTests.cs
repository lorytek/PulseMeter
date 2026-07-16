namespace PulseMeter.Tests;

public sealed class RunwayForecastSectionViewModelTests
{
    [Fact]
    public void ApplySignals_FormatsRiskAndOnTrackRowsWithEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var viewModel = new RunwayForecastSectionViewModel(new RunwayForecastPresenter());
        var signals = new UsageSignalsSnapshot
        {
            RunwayForecasts =
            [
                Forecast(
                    state: LimitRunwayForecastState.AtRisk,
                    usedPercent: 82,
                    resetsAt: now.AddHours(2),
                    exhaustsAt: now.AddMinutes(42),
                    projectedRemaining: 0,
                    percentPerHour: 25.7,
                    observationDuration: TimeSpan.FromMinutes(7),
                    isActionable: true,
                    confidence: LimitRunwayForecastConfidence.Medium,
                    earliestExhaustsAt: now.AddMinutes(30),
                    latestExhaustsAt: now.AddMinutes(55),
                    sampleCount: 5),
                Forecast(
                    state: LimitRunwayForecastState.OnTrack,
                    usedPercent: 30,
                    resetsAt: now.AddDays(2),
                    exhaustsAt: now.AddDays(4),
                    projectedRemaining: 48,
                    percentPerHour: 1.2,
                    observationDuration: TimeSpan.FromMinutes(4),
                    isActionable: false,
                    windowMinutes: 10_080,
                    windowLabel: "Weekly")
            ]
        };

        viewModel.ApplySignals(signals, "codex", now);

        Assert.True(viewModel.HasRows);
        Assert.Collection(
            viewModel.Rows,
            row =>
            {
                Assert.Equal("At risk", row.StatusText);
                Assert.Equal("May run out in 30m to 55m", row.ForecastText);
                Assert.Equal("18% left", row.RemainingText);
                Assert.Contains("7m sample", row.EvidenceText);
                Assert.Contains("5 readings", row.EvidenceText);
                Assert.Contains("Medium confidence", row.EvidenceText);
                Assert.Contains("25.7%/h", row.EvidenceText);
            },
            row =>
            {
                Assert.Equal("On track", row.StatusText);
                Assert.Equal("Reset expected first", row.ForecastText);
                Assert.Contains("48%", row.DetailText);
                Assert.StartsWith("Fri ", row.ResetText);
            });
    }

    [Fact]
    public void ApplySignals_PreservesLearningEmptyEvidenceWithoutCreatingAnAlert()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var viewModel = new RunwayForecastSectionViewModel(new RunwayForecastPresenter());
        var signals = new UsageSignalsSnapshot
        {
            RunwayForecasts =
            [
                Forecast(
                    state: LimitRunwayForecastState.Learning,
                    usedPercent: 10,
                    resetsAt: now.AddHours(4),
                    exhaustsAt: null,
                    projectedRemaining: null,
                    percentPerHour: null,
                    observationDuration: null,
                    isActionable: false)
            ]
        };

        viewModel.ApplySignals(signals, "codex", now);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Learning recent pace", row.ForecastText);
        Assert.Equal("Estimated | awaiting a second live sample", row.EvidenceText);
        Assert.Empty(signals.AttentionSignals);
    }

    [Fact]
    public void ApplySignals_ExplainsNoMovementWithoutClaimingAStableFuture()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var viewModel = new RunwayForecastSectionViewModel(new RunwayForecastPresenter());
        var signals = new UsageSignalsSnapshot
        {
            RunwayForecasts =
            [
                Forecast(
                    state: LimitRunwayForecastState.Stable,
                    usedPercent: 11,
                    resetsAt: now.AddHours(4),
                    exhaustsAt: null,
                    projectedRemaining: 89,
                    percentPerHour: 0,
                    observationDuration: TimeSpan.FromMinutes(5),
                    isActionable: false,
                    confidence: LimitRunwayForecastConfidence.Low,
                    sampleCount: 3)
            ]
        };

        viewModel.ApplySignals(signals, "codex", now);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("No movement", row.StatusText);
        Assert.Equal("No recent movement", row.ForecastText);
        Assert.Contains("not a long-term prediction", row.DetailText);
        Assert.Contains("3 readings", row.EvidenceText);
        Assert.Contains("Low confidence", row.EvidenceText);
    }

    [Fact]
    public void Refresh_RemovesForecastAfterItsRateLimitReset()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var viewModel = new RunwayForecastSectionViewModel(new RunwayForecastPresenter());
        var signals = new UsageSignalsSnapshot
        {
            RunwayForecasts =
            [
                Forecast(
                    state: LimitRunwayForecastState.AtRisk,
                    usedPercent: 80,
                    resetsAt: now.AddMinutes(1),
                    exhaustsAt: now.AddSeconds(30),
                    projectedRemaining: 0,
                    percentPerHour: 40,
                    observationDuration: TimeSpan.FromMinutes(5),
                    isActionable: true)
            ]
        };

        viewModel.ApplySignals(signals, "codex", now);
        Assert.True(viewModel.HasRows);

        viewModel.Refresh(now.AddMinutes(1));

        Assert.False(viewModel.HasRows);
        Assert.Empty(viewModel.Rows);
    }

    [Fact]
    public void SelectLimit_ShowsOnlyForecastsForTheSelectedModel()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var viewModel = new RunwayForecastSectionViewModel(new RunwayForecastPresenter());
        var signals = new UsageSignalsSnapshot
        {
            RunwayForecasts =
            [
                Forecast(
                    state: LimitRunwayForecastState.Stable,
                    usedPercent: 11,
                    resetsAt: now.AddDays(1),
                    exhaustsAt: null,
                    projectedRemaining: null,
                    percentPerHour: 0,
                    observationDuration: TimeSpan.FromMinutes(5),
                    isActionable: false),
                Forecast(
                    state: LimitRunwayForecastState.Learning,
                    usedPercent: 0,
                    resetsAt: now.AddDays(2),
                    exhaustsAt: null,
                    projectedRemaining: null,
                    percentPerHour: null,
                    observationDuration: null,
                    isActionable: false,
                    limitKey: "codex_bengalfox",
                    trackLabel: "GPT-5.3-Codex-Spark")
            ]
        };

        viewModel.ApplySignals(signals, "codex", now);

        Assert.Equal("General", Assert.Single(viewModel.Rows).TrackText);

        viewModel.SelectLimit("codex_bengalfox", now);

        Assert.Equal("GPT-5.3-Codex-Spark", Assert.Single(viewModel.Rows).TrackText);
    }

    private static LimitRunwayForecast Forecast(
        LimitRunwayForecastState state,
        double usedPercent,
        DateTimeOffset resetsAt,
        DateTimeOffset? exhaustsAt,
        double? projectedRemaining,
        double? percentPerHour,
        TimeSpan? observationDuration,
        bool isActionable,
        int windowMinutes = 300,
        string windowLabel = "5h Window",
        string limitKey = "codex",
        string trackLabel = "General",
        LimitRunwayForecastConfidence confidence = LimitRunwayForecastConfidence.Low,
        DateTimeOffset? earliestExhaustsAt = null,
        DateTimeOffset? latestExhaustsAt = null,
        int sampleCount = 1)
    {
        return new LimitRunwayForecast(
            "codex|window",
            limitKey,
            trackLabel,
            windowLabel,
            windowMinutes,
            resetsAt,
            usedPercent,
            state,
            exhaustsAt,
            projectedRemaining,
            percentPerHour,
            observationDuration,
            isActionable,
            IsMock: false,
            Confidence: confidence,
            EarliestExhaustsAtUtc: earliestExhaustsAt,
            LatestExhaustsAtUtc: latestExhaustsAt,
            SampleCount: sampleCount);
    }
}
