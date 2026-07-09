namespace PulseMeter.Tests;

public sealed class RateLimitsSectionViewModelTests
{
    [Fact]
    public void ApplyUsageSignals_ShowsRunwayHintOnlyForSelectedLimitGroup()
    {
        var now = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero);
        var viewModel = new RateLimitsSectionViewModel(new RateLimitsPresenter());

        viewModel.ApplyBuckets(
            [
                Bucket("codex", "General", 300),
                Bucket("other", "Other", 300)
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
        Assert.Equal("Runway: about 10m at current pace", viewModel.RunwayHintText);
        Assert.Equal("Estimated", viewModel.RunwayEvidenceText);

        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.Key == "other");

        Assert.False(viewModel.HasRunwayHint);
        Assert.Equal(string.Empty, viewModel.RunwayHintText);
    }

    private static RateLimitBucket Bucket(string limitId, string groupLabel, int windowMinutes)
    {
        return new RateLimitBucket
        {
            LimitId = limitId,
            GroupLabel = groupLabel,
            Label = "5h Window",
            WindowLabel = "5h",
            WindowDurationMins = windowMinutes,
            UsedPercent = 40,
            ResetsAtUtc = new DateTimeOffset(2026, 7, 6, 21, 0, 0, TimeSpan.Zero)
        };
    }
}
