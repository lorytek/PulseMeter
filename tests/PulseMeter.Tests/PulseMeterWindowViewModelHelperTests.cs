using PulseMeter.Slices.AccountUsage;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.ProjectUsage;
using PulseMeter.Slices.RateLimitsDaily;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowViewModelHelperTests
{
    [Fact]
    public void RateLimitsDailyDisplayBuilder_BuildRows_SplitsWeeklyLimitIntoSevenRows()
    {
        var now = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
        var bucket = new RateLimitBucket
        {
            UsedPercent = 50,
            WindowDurationMins = 10_080,
            WindowLabel = "7d",
            ResetsAtUtc = now.AddDays(4)
        };

        var rows = RateLimitsDailyDisplayBuilder.BuildRows([bucket], now);

        Assert.Equal(7, rows.Count);
        Assert.Equal("Day 1", rows[0].Label);
        Assert.Equal("0%", rows[0].RemainingPercentText);
        Assert.Equal("Day 4", rows[3].Label);
        Assert.Equal("#1F73FF", rows[3].LabelBrush);
        Assert.Equal("50%", rows[3].RemainingPercentText);
        Assert.Equal("100%", rows[4].RemainingPercentText);
    }

    [Fact]
    public void RateLimitsDailyDisplayBuilder_BuildWarningText_DoesNotWarnWhileCurrentResetDayHasAllowance()
    {
        var now = new DateTimeOffset(2026, 7, 7, 10, 40, 0, TimeSpan.Zero);
        var bucket = new RateLimitBucket
        {
            UsedPercent = 4,
            WindowDurationMins = 10_080,
            WindowLabel = "7d",
            ResetsAtUtc = new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.Zero)
        };

        var warning = RateLimitsDailyDisplayBuilder.BuildWarningText([bucket], now);

        Assert.Equal(string.Empty, warning);
    }

    [Fact]
    public void DailyUsageDisplayBuilder_BuildRows_CreatesSevenRowsAndMedianComparison()
    {
        var today = new DateOnly(2026, 7, 5);
        var buckets = new[]
        {
            Bucket(today, 100),
            Bucket(today.AddDays(-1), 50),
            Bucket(today.AddDays(-2), 150)
        };

        var result = DailyUsageDisplayBuilder.BuildRows(buckets, today);

        Assert.Equal(7, result.Rows.Count);
        Assert.Equal("Today", result.Rows[0].DateText);
        Assert.Equal("100", result.Rows[0].TokenText);
        Assert.True(result.Rows[0].HasMedianComparison);
        Assert.Equal("near median", result.Rows[0].MedianComparisonText);
        Assert.Equal("30-day median per day: 100", DailyUsageDisplayBuilder.FormatMedianSummaryText(result.MedianBaseline));
    }

    [Fact]
    public void ProjectUsageDisplayBuilder_BuildRows_FormatsRowsForDisplay()
    {
        var rows = ProjectUsageDisplayBuilder.BuildRows(
        [
            new ProjectUsageRow("PulseMeter", "C:\\PulseMeter", 1_250_000, 900_000, 12, 42.25)
        ]);

        Assert.Single(rows);
        Assert.Equal("PulseMeter", rows[0].DisplayName);
        Assert.Equal("1.3M", rows[0].EstimatedTokensText);
        Assert.Equal("42.3%", rows[0].ShareText);
        Assert.Equal("12", rows[0].ThreadCountText);
        Assert.Equal(42.25, rows[0].SharePercentValue);
    }

    [Fact]
    public void UsageAttributionPresenter_FormatsBurnAnalysisRowsAndSummary()
    {
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new UsageAttributionSnapshot
        {
            Sessions =
            [
                new UsageAttributionSessionRow(
                    "Implement attribution",
                    "thread-1",
                    "PulseMeter",
                    @"C:\Projects\PulseMeter",
                    900_000,
                    1_250_000,
                    42.25,
                    500_000,
                    200_000,
                    100_000,
                    50_000,
                    now.AddMinutes(-18),
                    now.AddMinutes(-12))
            ],
            BurnEvents =
            [
                new UsageAttributionBurnEvent(
                    "Implement attribution",
                    "thread-1",
                    "PulseMeter",
                    @"C:\Projects\PulseMeter",
                    now.AddMinutes(-12),
                    600_000,
                    830_000,
                    300_000,
                    120_000,
                    90_000,
                    40_000)
            ],
            RawLocalTokens = 900_000,
            EstimatedAttributedTokens = 1_250_000,
            AccountWindowTokens = 3_000_000,
            LastUpdatedUtc = now
        };
        var presenter = new UsageAttributionPresenter();

        var sessions = presenter.BuildSessionRows(snapshot, now);
        var burnEvents = presenter.BuildBurnEventRows(snapshot, now);

        Assert.Equal("1.3M attributed across 1 local chat", presenter.SummaryText(snapshot));
        Assert.Equal("Estimated from local chats, scaled to account usage", presenter.EvidenceText(snapshot));
        Assert.True(presenter.HasAttribution(snapshot));
        var session = Assert.Single(sessions);
        Assert.Equal("Implement attribution", session.DisplayName);
        Assert.Equal("PulseMeter", session.ProjectDisplayName);
        Assert.Equal("1.3M", session.EstimatedTokensText);
        Assert.Equal("42.3%", session.ShareText);
        Assert.Equal("local raw 900.0K", session.RawLocalTokensText);
        Assert.Equal("500.0K in / 200.0K out / 100.0K cached / 50.0K reasoning", session.BreakdownText);
        Assert.Equal("12m ago", session.AgeText);
        Assert.Contains("Chat id: thread-1", session.TooltipText);
        Assert.Contains("Project: PulseMeter", session.TooltipText);
        Assert.Contains(@"Path: C:\Projects\PulseMeter", session.TooltipText);

        var burnEvent = Assert.Single(burnEvents);
        Assert.Equal("830.0K", burnEvent.EstimatedTokensText);
        Assert.Equal("12m ago", burnEvent.AgeText);
        Assert.Equal("300.0K in / 120.0K out / 90.0K cached / 40.0K reasoning", burnEvent.BreakdownText);
        Assert.Contains("Chat id: thread-1", burnEvent.TooltipText);
        Assert.Equal("No local burn analysis yet.", presenter.EmptyStateText(UsageAttributionSnapshot.Empty));
    }

    [Fact]
    public void AccountUsageDisplayBuilder_FormatsDashboardValuesAndWarnings()
    {
        var today = new DateOnly(2026, 7, 5);
        var snapshot = new UsageSnapshot
        {
            LifetimeTokens = 9_100_000_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 20,
            DailyBuckets =
            [
                Bucket(today.AddDays(-1), 940_800_000),
                Bucket(today, 0)
            ]
        };
        var median = DailyUsageDisplayBuilder.BuildRows(snapshot.DailyBuckets, today).MedianBaseline;

        Assert.Equal("0", AccountUsageDisplayBuilder.TodayUsageMetricValueText(snapshot, today));
        Assert.Equal("9.1B", AccountUsageDisplayBuilder.LifetimeUsageValueText(snapshot));
        Assert.Equal("940.8M", AccountUsageDisplayBuilder.PeakUsageValueText(snapshot));
        Assert.Equal("20", AccountUsageDisplayBuilder.StreakDaysValueText(snapshot));
        Assert.Equal(today.AddDays(-1).ToString("dd MMM yyyy"), AccountUsageDisplayBuilder.PeakUsageCaptionText(snapshot));
        Assert.Equal("0.0% of 30-day median", AccountUsageDisplayBuilder.TodayMedianDailyPercentText(snapshot, median, today));
        Assert.Equal("Usage summary unavailable.", AccountUsageDisplayBuilder.SummaryText(new UsageSnapshot()));
        Assert.Equal("Today's usage is not available yet.", AccountUsageDisplayBuilder.FreshnessWarningText(hasAccountSummary: true));
    }

    [Fact]
    public void AccountUsageFreshnessEvaluator_DetectsMissingLiveTodayBucket()
    {
        var today = new DateOnly(2026, 7, 5);
        var previous = new UsageSnapshot();
        var next = new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [new RateLimitBucket { WindowLabel = "5h", UsedPercent = 10 }],
            LifetimeTokens = 1,
            DailyBuckets =
            [
                Bucket(today.AddDays(-1), 100)
            ]
        };

        var result = AccountUsageFreshnessEvaluator.Evaluate(
            previous,
            next,
            today,
            useMockMode: false,
            currentDailyWarning: false,
            currentAccountSummaryWarning: false);

        Assert.True(result.HasDailyUsageFreshnessWarning);
        Assert.True(result.HasAccountSummaryFreshnessWarning);
    }

    [Fact]
    public void PulseMeterWindowLayoutCalculator_SanitizesAndScalesExpandedWindow()
    {
        Assert.Equal(1024, PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowWidth(double.NaN));
        Assert.Equal(720, PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowWidth(1));
        Assert.Equal(1300, PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowWidth(9999));
        Assert.Equal(460, PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowHeight(1));
        Assert.Equal(1.0, PulseMeterWindowLayoutCalculator.CalculateExpandedLayoutScale(isExpanded: false, width: 1042, height: 712));
        Assert.Equal(1.0, PulseMeterWindowLayoutCalculator.CalculateExpandedLayoutScale(isExpanded: true, width: 1042, height: 712));
        Assert.Equal(0.72, PulseMeterWindowLayoutCalculator.CalculateExpandedLayoutScale(isExpanded: true, width: 720, height: 460));
    }

    [Fact]
    public void CompactWindowMinimumWidth_FitsTheFixedTrayContentAndSurfaceChrome()
    {
        Assert.Equal(410, PulseMeterWindowLayoutCalculator.CompactWindowWidth);
        Assert.Equal(410, PulseMeterWindowLayoutCalculator.CompactWindowMinWidth);
        Assert.Equal(66, PulseMeterWindowLayoutCalculator.CompactWindowHeight);
        Assert.Equal(66, PulseMeterWindowLayoutCalculator.CompactWindowMinHeight);
    }

    private static DailyUsageBucket Bucket(DateOnly date, long tokens)
    {
        return new DailyUsageBucket
        {
            StartDate = date.ToString("yyyy-MM-dd"),
            Tokens = tokens
        };
    }
}
