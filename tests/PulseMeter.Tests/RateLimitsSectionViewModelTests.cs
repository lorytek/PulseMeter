namespace PulseMeter.Tests;

public sealed class RateLimitsSectionViewModelTests
{
    [Fact]
    public void ApplyBuckets_DrawsFullCountdownRingFromRemainingPercentage()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var viewModel = new RateLimitsSectionViewModel(new RateLimitsPresenter());

        viewModel.ApplyBuckets([Bucket("codex", "General", 10_080, 30)], now);

        var weekly = Assert.Single(viewModel.SelectedQuotaRows);
        Assert.Equal("70%", weekly.RingPercentText);
        Assert.Equal("M 56 13 A 43 43 0 1 1 15.105 69.288", weekly.RingArcData);
        Assert.Equal(11.605, weekly.RingKnobLeft, 3);
        Assert.Equal(65.788, weekly.RingKnobTop, 3);
        Assert.Equal(9.105, weekly.RingKnobHaloLeft, 3);
        Assert.Equal(63.288, weekly.RingKnobHaloTop, 3);
    }

    [Fact]
    public void ApplyWeeklyPace_MarksWeeklyRowAheadWithoutChangingRemainingGauge()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var viewModel = new RateLimitsSectionViewModel(new RateLimitsPresenter());
        viewModel.ApplyBuckets([Bucket("codex", "General", 10_080, 30)], now);
        var originalArc = Assert.Single(viewModel.SelectedQuotaRows).RingArcData;

        viewModel.ApplyWeeklyPace(true, "Wait 1d 2h 36m to get back on pace");

        var weekly = Assert.Single(viewModel.SelectedQuotaRows);
        Assert.Equal("70%", weekly.RingPercentText);
        Assert.Equal(originalArc, weekly.RingArcData);
        Assert.Equal("#F59E0B", weekly.RingBrush);
        Assert.Equal("Ahead of pace", weekly.StatusText);
        Assert.Equal("#D97706", weekly.StatusBrush);
        Assert.Equal("Wait 1d 2h 36m to get back on pace", weekly.PaceText);
        Assert.False(weekly.IsPaceDetailVisible);
        Assert.Equal("#D97706", weekly.PaceBrush);
        Assert.Equal("\uE7BA", weekly.PaceIconGlyph);
    }

    [Fact]
    public void ApplyUsageSignals_AttachesForecastToMatchingShortWindowAndCapsExpandedRows()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var viewModel = new RateLimitsSectionViewModel(new RateLimitsPresenter());

        viewModel.ApplyBuckets(
            [
                Bucket("codex", "General", 300, 96),
                Bucket("codex", "General", 10_080, 88),
                Bucket("codex", "General", 60, 92),
                Bucket("other", "Other", 300, 92)
            ],
            now);

        viewModel.ApplyUsageSignals(new UsageSignalsSnapshot
        {
            RunwaySignals =
            [
                new LimitRunwaySignal(
                    "codex|300",
                    "codex",
                    "5h Window",
                    300,
                    now.AddHours(1),
                    now.AddMinutes(10),
                    TimeSpan.FromMinutes(10),
                    "Runway: about 10m at current pace",
                    "Projected to run out before reset",
                    "At the current pace, 5h Window may run out in about 10m before the 5h reset.",
                    "#F97316")
            ]
        });

        Assert.True(viewModel.HasRunwayHint);
        Assert.Equal(2, viewModel.SelectedQuotaRows.Count);
        Assert.True(viewModel.HasMultipleSelectedQuotaRows);
        Assert.Equal(2, viewModel.SelectedQuotaColumnCount);
        Assert.Collection(
            viewModel.SelectedQuotaRows,
            shortWindow =>
            {
                Assert.Equal("Critical", shortWindow.StatusText);
                Assert.Equal("May run out 50m before reset", shortWindow.PaceText);
                Assert.Equal("#F97316", shortWindow.PaceBrush);
                Assert.True(shortWindow.HasRunwayForecast);
                Assert.True(shortWindow.IsPaceDetailVisible);
                Assert.Equal("5h Window", shortWindow.RowTitleText);
                Assert.Equal("\uE823", shortWindow.RowIconGlyph);
                Assert.Equal("\uE7BA", shortWindow.PaceIconGlyph);
                Assert.True(shortWindow.HasCriticalRingArc);
                Assert.Contains(" A 43 43 ", shortWindow.CriticalRingArcData);
                Assert.StartsWith("Resets ", shortWindow.ResetTimeText);
                Assert.Equal("in 1h", shortWindow.ResetCountdownText);
            },
            weekly =>
            {
                Assert.Equal("On pace", weekly.StatusText);
                Assert.Equal("Within weekly pace", weekly.PaceText);
                Assert.False(weekly.HasRunwayForecast);
                Assert.False(weekly.IsPaceDetailVisible);
                Assert.Equal("Weekly", weekly.RowTitleText);
                Assert.False(weekly.HasCriticalRingArc);
            });

        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.Key == "other");

        Assert.False(viewModel.HasRunwayHint);
        Assert.Single(viewModel.SelectedQuotaRows);
        Assert.False(viewModel.HasMultipleSelectedQuotaRows);
        Assert.Equal(1, viewModel.SelectedQuotaColumnCount);
        Assert.Equal("Warning", viewModel.SelectedQuotaRows[0].StatusText);
        Assert.Equal("Use is above pace", viewModel.SelectedQuotaRows[0].PaceText);
    }

    private static RateLimitBucket Bucket(string limitId, string groupLabel, int windowMinutes, double usedPercent)
    {
        return new RateLimitBucket
        {
            LimitId = limitId,
            GroupLabel = groupLabel,
            Label = "5h Window",
            WindowLabel = "5h",
            WindowDurationMins = windowMinutes,
            UsedPercent = usedPercent,
            ResetsAtUtc = new DateTimeOffset(2026, 7, 6, 21, 0, 0, TimeSpan.Zero),
            ResetCountdown = "1h"
        };
    }
}
