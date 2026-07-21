using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using PulseMeter.Platform.Windows;
using Rect = System.Windows.Rect;

namespace PulseMeter.Tests;

public sealed class UsageTrendSectionTests
{
    [Fact]
    public void ChartAxis_UsesSixHourClockTicksAndDatesOnlyAtMidnight()
    {
        var start = LocalTime(2026, 7, 17, 0, 0);
        var end = LocalTime(2026, 7, 18, 0, 0);

        var ticks = UsageTrendChart.BuildTimeTicks(start, end);

        Assert.Equal(["00:00", "06:00", "12:00", "18:00", "00:00"], ticks.Select(tick => tick.TimeLabel));
        Assert.Equal(start.ToString("ddd MMM d", CultureInfo.CurrentCulture), ticks[0].DateLabel);
        Assert.All(ticks.Skip(1).Take(3), tick => Assert.Null(tick.DateLabel));
        Assert.Equal(end.ToString("ddd MMM d", CultureInfo.CurrentCulture), ticks[^1].DateLabel);
    }

    [Fact]
    public void ChartAxis_UsesCompactSixHourLabelsAcrossSevenDayCard()
    {
        var start = LocalTime(2026, 7, 19, 0, 0);
        var end = start.AddDays(7);
        var ticks = UsageTrendChart.BuildTimeTicks(start, end);

        var density = UsageTrendChart.ResolveTimeLabelDensity(start, end, 43, 758);

        Assert.Equal(UsageTrendChart.UsageTrendTimeLabelDensity.CompactHours, density);
        Assert.Equal(
            ["00", "06", "12", "18"],
            ticks.Take(4).Select(tick => UsageTrendChart.FormatTimeTickLabel(tick, density)));
        Assert.All(
            ticks.Where(tick => tick.DateLabel is not null),
            tick => Assert.Equal(0, tick.Timestamp.ToLocalTime().Hour));
    }

    [Fact]
    public void ChartAxis_FiveHourWindowUsesHourlyLabelsWithoutDates()
    {
        var start = LocalTime(2026, 7, 18, 13, 30);
        var end = start.AddHours(5);

        var ticks = UsageTrendChart.BuildTimeTicks(start, end);

        Assert.Equal(["14:00", "15:00", "16:00", "17:00", "18:00"], ticks.Select(tick => tick.TimeLabel));
        Assert.All(ticks, tick => Assert.Null(tick.DateLabel));
    }

    [Fact]
    public void ChartAutomationPeer_ExposesSummaryToWindowsAutomation()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var chart = new UsageTrendChart();
                AutomationProperties.SetName(chart, "Coding runway summary for accessibility");

                var peer = UIElementAutomationPeer.CreatePeerForElement(chart);

                Assert.NotNull(peer);
                Assert.Equal("Coding runway summary for accessibility", peer.GetName());
                Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
                Assert.True(peer.IsControlElement());
                Assert.True(peer.IsContentElement());
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The UI Automation peer test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void ChartAxis_WeeklyWindowFocusesMeasuredHistoryWhileTheContextKeepsAllSevenDays()
    {
        var windowStart = LocalTime(2026, 7, 12, 0, 0);
        var windowEnd = windowStart.AddDays(7);
        var firstRecorded = windowStart.AddDays(5).AddHours(4.5);
        var focus = UsageTrendChart.SelectFocusWindow(
            windowStart,
            windowEnd,
            [new UsageTrendPoint(firstRecorded, 42)]);

        var firstRecordedX = UsageTrendChart.MapTimelineTimestampToX(
            firstRecorded,
            windowStart,
            windowEnd,
            focus.FocusStart,
            focus.CompressUnmeasuredHistory,
            0,
            700);
        var contextTicks = UsageTrendChart.BuildDailyContextTicks(windowStart, windowEnd);

        Assert.True(focus.CompressUnmeasuredHistory);
        Assert.True(focus.ShowContextStrip);
        Assert.Equal(firstRecorded, focus.FocusStart);
        Assert.Equal(126, firstRecordedX, precision: 3);
        Assert.Equal(8, contextTicks.Count);
        Assert.All(contextTicks, tick => Assert.Equal(0, tick.Timestamp.ToLocalTime().Hour));
        Assert.Equal(windowStart, contextTicks[0].Timestamp);
        Assert.Equal(windowEnd, contextTicks[^1].Timestamp);
    }

    [Fact]
    public void ChartAxis_FiveHourWindowRemainsAContinuousTimelineWithoutFocusCompression()
    {
        var start = LocalTime(2026, 7, 18, 13, 30);
        var end = start.AddHours(5);
        var point = start.AddHours(2.5);
        var focus = UsageTrendChart.SelectFocusWindow(start, end, [new UsageTrendPoint(point, 48)]);

        var x = UsageTrendChart.MapTimelineTimestampToX(
            point,
            start,
            end,
            focus.FocusStart,
            focus.CompressUnmeasuredHistory,
            10,
            510);

        Assert.False(focus.CompressUnmeasuredHistory);
        Assert.False(focus.ShowContextStrip);
        Assert.Equal(260, x, precision: 3);
    }

    [Fact]
    public void ChartTooltip_OnlyAssociatesRangeWithinForecastSpanAndInterpolatesIt()
    {
        var start = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var points = new[]
        {
            new UsageTrendBandPoint(start, 60, 70),
            new UsageTrendBandPoint(start.AddHours(1), 80, 100)
        };

        Assert.Null(UsageTrendChart.FindBandPointAt(points, start.AddMinutes(-1)));
        Assert.Null(UsageTrendChart.FindBandPointAt(points, start.AddHours(1).AddMinutes(1)));

        var midpoint = UsageTrendChart.FindBandPointAt(points, start.AddMinutes(30));
        Assert.NotNull(midpoint);
        Assert.Equal(70, midpoint.LowerPercent);
        Assert.Equal(85, midpoint.UpperPercent);
    }

    [Fact]
    public void ChartLatestMarker_UsesNowOnlyForFreshSamples()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

        Assert.StartsWith("Now · ", UsageTrendChart.FormatLatestPointLabel(now.AddMinutes(-4), now));
        Assert.StartsWith("Latest · ", UsageTrendChart.FormatLatestPointLabel(now.AddMinutes(-6), now));
        Assert.Equal("Now · 53%", UsageTrendChart.FormatLatestUsageLabel(now.AddMinutes(-4), now, 53));
        Assert.Equal("Latest · 53%", UsageTrendChart.FormatLatestUsageLabel(now.AddMinutes(-6), now, 53));
        Assert.DoesNotContain(Environment.NewLine, UsageTrendChart.FormatLatestUsageLabel(now, now, 53));
    }

    [Fact]
    public void ChartInlineSeriesLabels_RequireEnoughHorizontalRoom()
    {
        Assert.False(UsageTrendChart.ShouldShowInlineSeriesLabel(70, 40));
        Assert.True(UsageTrendChart.ShouldShowInlineSeriesLabel(76, 40));
    }

    [Fact]
    public void ChartHistoryLabel_HidesWhenItWouldOverlapTheFirstRecordedLabel()
    {
        var firstRecordedBounds = new Rect(60, 20, 92, 14);

        Assert.False(UsageTrendChart.HasLabelClearance(new Rect(48, 18, 68, 12), firstRecordedBounds));
        Assert.True(UsageTrendChart.HasLabelClearance(new Rect(48, 42, 68, 12), firstRecordedBounds));
    }

    [Fact]
    public void ChartTooltipTimestamp_IncludesDateAndTime()
    {
        var timestamp = new DateTimeOffset(2026, 7, 20, 21, 34, 0, TimeSpan.Zero);

        Assert.Equal(
            timestamp.ToLocalTime().ToString("ddd, MMM d · h:mm tt", CultureInfo.CurrentCulture),
            UsageTrendChart.FormatTooltipTimestamp(timestamp));
    }

    [Fact]
    public void ChartHover_RequiresThePointerToBeNearAnActualPoint()
    {
        Assert.True(UsageTrendChart.IsWithinActualHoverRadius(100, 100, 110, 110));
        Assert.False(UsageTrendChart.IsWithinActualHoverRadius(100, 100, 130, 100));
    }

    [Fact]
    public void ChartForecastWindowTooltip_ExplainsTheEstimatedTiming()
    {
        var earliest = new DateTimeOffset(2026, 7, 21, 17, 30, 0, TimeSpan.Zero);
        var latest = earliest.AddHours(3);
        var mostLikely = earliest.AddHours(1);

        var tooltip = UsageTrendChart.BuildForecastWindowTooltipText(earliest, latest, mostLikely);

        Assert.Contains("Estimated reach limit", tooltip);
        Assert.Contains($"Most likely  {UsageTrendChart.FormatTooltipTimestamp(mostLikely)}", tooltip);
        Assert.Contains($"Earliest  {UsageTrendChart.FormatTooltipTimestamp(earliest)}", tooltip);
        Assert.Contains($"Latest  {UsageTrendChart.FormatTooltipTimestamp(latest)}", tooltip);
        Assert.DoesNotContain("Actual usage", tooltip);
    }

    [Fact]
    public void ChartActualSeries_SplitsAtPersistedMeasurementGap()
    {
        var start = new DateTimeOffset(2026, 7, 20, 20, 0, 0, TimeSpan.Zero);
        var points = new[]
        {
            new UsageTrendPoint(start, 40),
            new UsageTrendPoint(start.AddMinutes(5), 45),
            new UsageTrendPoint(start.AddHours(2), 55),
            new UsageTrendPoint(start.AddHours(2).AddMinutes(5), 60)
        };
        var gaps = new[] { new UsageTrendGap(start.AddMinutes(5), start.AddHours(2)) };

        var segments = UsageTrendChart.SplitSeriesAtGaps(points, gaps);

        Assert.Equal(2, segments.Count);
        Assert.Equal(points.Take(2), segments[0]);
        Assert.Equal(points.Skip(2), segments[1]);
        Assert.Equal("1.9h", UsageTrendChart.FormatGapDuration(gaps[0].EndedAt - gaps[0].StartedAt));
    }

    [Fact]
    public void Presenter_MapsMeasurementGapIntoChartAndAccessibilitySummary()
    {
        var start = new DateTimeOffset(2026, 7, 20, 20, 0, 0, TimeSpan.Zero);
        var reset = start.AddDays(7);
        var trend = new LimitUsageTrend(
            "codex|10080",
            "codex",
            "General",
            "7-Day Usage",
            10_080,
            reset,
            [
                new LimitUsagePoint(start, 40),
                new LimitUsagePoint(start.AddMinutes(5), 45),
                new LimitUsagePoint(start.AddHours(2), 55)
            ],
            IsMock: false)
        {
            MeasurementGaps = [new LimitUsageGap(start.AddMinutes(5), start.AddHours(2))]
        };

        var chart = new UsageTrendPresenter().BuildChart(
            trend,
            forecast: null,
            start.AddHours(2),
            showProjection: false,
            showRange: false);

        var gap = Assert.Single(Assert.IsType<UsageTrendChartModel>(chart).MeasurementGaps);
        Assert.Equal(start.AddMinutes(5), gap.StartedAt);
        Assert.Equal(start.AddHours(2), gap.EndedAt);
        Assert.Contains("1 measurement gap is shown as not measured", chart.AccessibleSummary);
    }

    [Fact]
    public void Presenter_WeeklyEvidenceShowsProgressTowardTwentyFourHourBaseline()
    {
        var now = new DateTimeOffset(2026, 7, 21, 8, 30, 0, TimeSpan.Zero);
        var reset = now.AddDays(4);
        var trend = new LimitUsageTrend(
            "codex|10080",
            "codex",
            "General",
            "7-Day Usage",
            10_080,
            reset,
            [
                new LimitUsagePoint(now.AddHours(-2.6), 74),
                new LimitUsagePoint(now, 74)
            ],
            IsMock: false);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Low, isMock: false) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7-Day Usage",
            WindowDurationMins = 10_080,
            ObservationDuration = TimeSpan.FromHours(2.6),
            SampleCount = 3
        };

        var chart = new UsageTrendPresenter().BuildChart(
            trend,
            forecast,
            now,
            showProjection: true,
            showRange: true);

        Assert.Equal(
            "Building 24h baseline • 2.6h of 24h collected · 3 samples",
            Assert.IsType<UsageTrendChartModel>(chart).Summary.ConfidenceText);
    }

    [Fact]
    public void ChartTopLabels_DetectNearbyHorizontalCollisions()
    {
        Assert.True(UsageTrendChart.DoLabelRangesOverlap(40, 90, 118, 100));
        Assert.False(UsageTrendChart.DoLabelRangesOverlap(40, 60, 120, 80));
    }

    [Fact]
    public void Presenter_MomentumForFiveHourWindow_UsesPriorHourlyMedian()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var points = new[] { 0d, 1, 2, 3, 4, 6 }
            .Select((value, index) => new UsageTrendPoint(now.AddHours(index - 5), value))
            .ToArray();

        var momentum = UsageTrendPresenter.BuildUsageMomentum(points, 300);

        Assert.Equal("↗ +1%/h", momentum.ValueText);
        Assert.Equal("usage accelerating", momentum.StateText);
        Assert.Equal("vs 5h window median", momentum.BaselineText);
        Assert.Equal(1, momentum.GaugeValue);
    }

    [Fact]
    public void Presenter_MomentumForSevenDayWindow_UsesCompletedDayMedian()
    {
        var localDate = new DateTime(2026, 7, 20);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDate);
        var today = new DateTimeOffset(localDate, offset);
        var points = new[]
        {
            new UsageTrendPoint(today.AddDays(-3), 0),
            new UsageTrendPoint(today.AddDays(-2), 4.8),
            new UsageTrendPoint(today.AddDays(-1), 9.6),
            new UsageTrendPoint(today, 14.4),
            new UsageTrendPoint(today.AddHours(12), 18)
        };

        var momentum = UsageTrendPresenter.BuildUsageMomentum(points, 10_080);

        Assert.Equal("↗ +0.1%/h", momentum.ValueText);
        Assert.Equal("usage accelerating", momentum.StateText);
        Assert.Equal("vs median day", momentum.BaselineText);
        Assert.True(momentum.GaugeValue > 0);
    }

    [Fact]
    public void Presenter_BuildsActualProjectionAndHonestForecastRange()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(2).AddMinutes(47);
        var trend = Trend(now, reset, isMock: false, [20, 39, 61, 82, 96]);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            ProjectedRemainingAtResetPercent = -12,
            EarliestExhaustsAtUtc = now.AddMinutes(40),
            LatestExhaustsAtUtc = now.AddMinutes(56),
            ProjectionPoints =
            [
                new LimitRunwayProjectionPoint(now, 96, 96, 96),
                new LimitRunwayProjectionPoint(now.AddMinutes(30), 98, 97, 99),
                new LimitRunwayProjectionPoint(now.AddMinutes(48), 100, 99, 100)
            ]
        };

        var chart = new UsageTrendPresenter().BuildChart(trend, forecast, now, showProjection: true, showRange: true);

        Assert.NotNull(chart);
        Assert.Equal(5, chart.ActualPoints.Count);
        Assert.Equal(reset.AddMinutes(-300), chart.RangeStart);
        Assert.Equal(TimeSpan.FromHours(5), chart.RangeEnd - chart.RangeStart);
        Assert.Equal(now.AddMinutes(-40), chart.ActualPoints[0].Timestamp);
        Assert.Equal(3, chart.ProjectedPoints.Count);
        Assert.NotEmpty(chart.TypicalRange);
        Assert.Collection(
            chart.SustainablePoints,
            point => Assert.Equal(96, point.UsedPercent),
            point => Assert.Equal((reset, 100d), (point.Timestamp, point.UsedPercent)));
        Assert.True(chart.RangeStart < chart.RangeEnd);
        Assert.Equal(reset, chart.ResetAt);
        Assert.Equal(now.AddMinutes(40), chart.ForecastWindowStart);
        Assert.Equal(now.AddMinutes(56), chart.ForecastWindowEnd);
        Assert.Contains("left at this pace", chart.Summary.Headline);
        Assert.Equal("Medium evidence • 5 samples over 40m", chart.Summary.ConfidenceText);
        Assert.Equal("5%/h", chart.Summary.CurrentPaceText);
        Assert.Equal("-12%", chart.Summary.PaceComparisonText);
        Assert.Equal("will reach limit before reset", chart.Summary.PaceComparisonLabel);
        Assert.Contains("Reduce pace", chart.Summary.RecommendationText);
        Assert.Contains("5 observed points", chart.AccessibleSummary);
        Assert.Contains("Resets", chart.AccessibleSummary);
        Assert.True(UsageTrendChart.ShouldDrawForecastWindow(chart));
        Assert.True(UsageTrendChart.ShouldDrawForecastLimit(chart));

        var rangeOnly = chart with { ShowProjection = false, ShowRange = true };
        Assert.True(UsageTrendChart.ShouldDrawForecastWindow(rangeOnly));
        Assert.False(UsageTrendChart.ShouldDrawForecastLimit(rangeOnly));

        var projectionOnly = chart with { ShowProjection = true, ShowRange = false };
        Assert.False(UsageTrendChart.ShouldDrawForecastWindow(projectionOnly));
        Assert.True(UsageTrendChart.ShouldDrawForecastLimit(projectionOnly));
    }

    [Fact]
    public void Presenter_KeepsWindowStartSeparateFromFirstRecordedAndRetainsCentralLimitTime()
    {
        var now = new DateTimeOffset(2026, 7, 18, 10, 30, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2026, 7, 19, 10, 48, 0, TimeSpan.Zero);
        var windowStart = reset.AddMinutes(-10_080);
        var firstRecorded = new DateTimeOffset(2026, 7, 18, 4, 30, 0, TimeSpan.Zero);
        var likelyLimit = new DateTimeOffset(2026, 7, 18, 18, 20, 0, TimeSpan.Zero);
        var trend = new LimitUsageTrend(
            "codex|10080",
            "codex",
            "General",
            "7d",
            10_080,
            reset,
            [
                new LimitUsagePoint(firstRecorded, 47),
                new LimitUsagePoint(now.AddHours(-1), 69),
                new LimitUsagePoint(now, 78)
            ],
            IsMock: false);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            // The plotted projection is authoritative for the marker so the
            // vertical guide cannot drift away from the blue 100% crossing.
            ExhaustsAtUtc = likelyLimit.AddMinutes(-7),
            EarliestExhaustsAtUtc = likelyLimit.AddHours(-1),
            LatestExhaustsAtUtc = likelyLimit.AddHours(2),
            ProjectionPoints =
            [
                new LimitRunwayProjectionPoint(now, 78, 78, 78),
                new LimitRunwayProjectionPoint(likelyLimit, 100, 94, 100),
                new LimitRunwayProjectionPoint(reset, 100, 97, 100)
            ]
        };

        var chart = new UsageTrendPresenter().BuildChart(trend, forecast, now, showProjection: true, showRange: true);

        Assert.NotNull(chart);
        Assert.Equal(windowStart, chart.RangeStart);
        Assert.Equal(reset, chart.RangeEnd);
        Assert.Equal(TimeSpan.FromDays(7), chart.RangeEnd - chart.RangeStart);
        Assert.Equal(firstRecorded, chart.ActualPoints[0].Timestamp);
        Assert.DoesNotContain(chart.ActualPoints, point => point.Timestamp == windowStart);
        Assert.Equal(likelyLimit.AddHours(-1), chart.ForecastWindowStart);
        Assert.Equal(likelyLimit, chart.ForecastLimitAt);
        Assert.Equal(likelyLimit.AddHours(2), chart.ForecastWindowEnd);
        Assert.Equal("Estimated to reach the limit", chart.Summary.ForecastLeadText);
        Assert.Equal(likelyLimit.ToLocalTime().ToString("ddd, MMM d, h:mm tt", CultureInfo.CurrentCulture), chart.Summary.ForecastWhenText);
        Assert.Contains("earlier history was not measured", chart.AccessibleSummary);
        Assert.Contains("estimated limit time at the modeled pace", chart.AccessibleSummary);
    }

    [Fact]
    public void Presenter_DoesNotInventRangeForLowConfidenceLiveForecast()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(2);
        var trend = Trend(now, reset, isMock: false, [40, 42, 45]);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Low, isMock: false);

        var chart = new UsageTrendPresenter().BuildChart(trend, forecast, now, showProjection: true, showRange: true);

        Assert.NotNull(chart);
        Assert.Empty(chart.TypicalRange);
        Assert.False(chart.ShowRange);
        Assert.NotEmpty(chart.ProjectedPoints);
    }

    [Fact]
    public void Presenter_ExhaustedLimitWaitsForResetAndHidesPacingAction()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(2);
        var trend = Trend(now, reset, isMock: false, [96, 98, 100]);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.High, isMock: false) with
        {
            UsedPercent = 100,
            State = LimitRunwayForecastState.Exhausted,
            ExhaustsAtUtc = now
        };

        var chart = new UsageTrendPresenter().BuildChart(trend, forecast, now, showProjection: true, showRange: true);

        Assert.NotNull(chart);
        Assert.Equal("Limit reached", chart.Summary.Headline);
        Assert.Contains("Wait until the limit resets", chart.Summary.RecommendationText);
        Assert.False(chart.Summary.CanOpenPacingPlan);
    }

    [Fact]
    public void Presenter_UsesStatisticalProjectionAndItsP10P90Band()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(2);
        var trend = Trend(now, reset, isMock: false, [40, 44, 48, 52, 56]);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            ProjectionPoints =
            [
                new LimitRunwayProjectionPoint(now, 56, 56, 56),
                new LimitRunwayProjectionPoint(now.AddHours(1), 70, 64, 79),
                new LimitRunwayProjectionPoint(reset, 84, 72, 100)
            ],
            ExhaustionProbabilityBeforeReset = 0.36
        };

        var chart = new UsageTrendPresenter().BuildChart(trend, forecast, now, showProjection: true, showRange: true);

        Assert.NotNull(chart);
        Assert.Collection(
            chart.ProjectedPoints,
            point => Assert.Equal(56, point.UsedPercent),
            point => Assert.Equal(70, point.UsedPercent),
            point => Assert.Equal(84, point.UsedPercent));
        Assert.Collection(
            chart.TypicalRange,
            point => Assert.Equal((56d, 56d), (point.LowerPercent, point.UpperPercent)),
            point => Assert.Equal((64d, 79d), (point.LowerPercent, point.UpperPercent)),
            point => Assert.Equal((72d, 100d), (point.LowerPercent, point.UpperPercent)));
        Assert.DoesNotContain("80% model range", chart.AccessibleSummary);
    }

    [Fact]
    public void Presenter_ClipsEarlierForecastAndMarksOnlyMateriallyHigherActualUsage()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var now = capturedAt.AddMinutes(10);
        var reset = capturedAt.AddHours(5);
        var trend = new LimitUsageTrend(
            "codex|300",
            "codex",
            "General",
            "5h",
            300,
            reset,
            [
                new LimitUsagePoint(capturedAt, 40),
                new LimitUsagePoint(now, 55)
            ],
            IsMock: false);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            UsedPercent = 55,
            ProjectionPoints =
            [
                new LimitRunwayProjectionPoint(now, 55, 55, 55),
                new LimitRunwayProjectionPoint(now.AddHours(1), 70, 65, 78),
                new LimitRunwayProjectionPoint(reset, 100, 90, 100)
            ]
        };
        var reference = new UsageTrendForecastReference(
            capturedAt,
            reset,
            [
                new UsageTrendPoint(capturedAt, 40),
                new UsageTrendPoint(capturedAt.AddMinutes(20), 50),
                new UsageTrendPoint(reset, 100)
            ]);

        var chart = new UsageTrendPresenter().BuildChart(
            trend,
            forecast,
            now,
            showProjection: true,
            showRange: false,
            reference);

        Assert.NotNull(chart);
        Assert.Equal(capturedAt, chart.ReferenceForecastCapturedAt);
        Assert.Collection(
            chart.ReferenceProjectedPoints,
            point => Assert.Equal((capturedAt, 40d), (point.Timestamp, point.UsedPercent)),
            point => Assert.Equal((now, 45d), (point.Timestamp, point.UsedPercent)));
        var variance = Assert.Single(chart.UnfavorableVarianceSegments);
        Assert.Equal(capturedAt.AddMinutes(1), variance.Start.Timestamp);
        Assert.Equal(41.5, variance.Start.UsedPercent, precision: 3);
        Assert.Equal((now, 55d), (variance.End.Timestamp, variance.End.UsedPercent));
        Assert.Contains("10 percentage points above", chart.AccessibleSummary);

        var hidden = new UsageTrendPresenter().BuildChart(
            trend,
            forecast,
            now,
            showProjection: false,
            showRange: false,
            reference);
        Assert.NotNull(hidden);
        Assert.Empty(hidden.ReferenceProjectedPoints);
        Assert.Empty(hidden.UnfavorableVarianceSegments);
        Assert.Null(hidden.ReferenceForecastCapturedAt);
    }

    [Fact]
    public void Presenter_EvaluatesVarianceAtInteriorForecastVertices()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var now = capturedAt.AddMinutes(10);
        var reset = capturedAt.AddHours(5);
        var trend = new LimitUsageTrend(
            "codex|300",
            "codex",
            "General",
            "5h",
            300,
            reset,
            [new LimitUsagePoint(capturedAt, 40), new LimitUsagePoint(now, 60)],
            IsMock: false);
        var reference = new UsageTrendForecastReference(
            capturedAt,
            reset,
            [
                new UsageTrendPoint(capturedAt, 40),
                new UsageTrendPoint(capturedAt.AddMinutes(5), 45),
                new UsageTrendPoint(now, 60)
            ]);

        var chart = new UsageTrendPresenter().BuildChart(
            trend,
            Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false),
            now,
            showProjection: true,
            showRange: false,
            reference);

        Assert.NotNull(chart);
        Assert.Collection(
            chart.UnfavorableVarianceSegments,
            first =>
            {
                Assert.Equal(capturedAt.AddMinutes(1), first.Start.Timestamp);
                Assert.Equal(capturedAt.AddMinutes(5), first.End.Timestamp);
            },
            second =>
            {
                Assert.Equal(capturedAt.AddMinutes(5), second.Start.Timestamp);
                Assert.Equal(capturedAt.AddMinutes(9), second.End.Timestamp);
            });
    }

    [Fact]
    public void Presenter_DoesNotMarkVarianceAtOnePercentagePointOrLess()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var now = capturedAt.AddMinutes(10);
        var reset = capturedAt.AddHours(5);
        var trend = new LimitUsageTrend(
            "codex|300",
            "codex",
            "General",
            "5h",
            300,
            reset,
            [new LimitUsagePoint(capturedAt, 40), new LimitUsagePoint(now, 46)],
            IsMock: false);
        var reference = new UsageTrendForecastReference(
            capturedAt,
            reset,
            [new UsageTrendPoint(capturedAt, 40), new UsageTrendPoint(capturedAt.AddMinutes(20), 50)]);

        var chart = new UsageTrendPresenter().BuildChart(
            trend,
            Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false),
            now,
            showProjection: true,
            showRange: false,
            reference);

        Assert.NotNull(chart);
        Assert.NotEmpty(chart.ReferenceProjectedPoints);
        Assert.Empty(chart.UnfavorableVarianceSegments);
        Assert.Contains("in line with", chart.AccessibleSummary);
    }

    [Fact]
    public void ViewModel_RetainsOneReferenceUntilTheQuotaWindowResets()
    {
        var firstObservedAt = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var laterObservedAt = firstObservedAt.AddMinutes(10);
        var reset = firstObservedAt.AddHours(5);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.ApplySignals(
            new UsageSignalsSnapshot
            {
                UsageTrends =
                [
                    new LimitUsageTrend(
                        "codex|300",
                        "codex",
                        "General",
                        "5h",
                        300,
                        reset,
                        [new LimitUsagePoint(firstObservedAt.AddMinutes(-10), 35), new LimitUsagePoint(firstObservedAt, 40)],
                        IsMock: false)
                ],
                RunwayForecasts =
                [
                    Forecast(firstObservedAt, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
                    {
                        UsedPercent = 40,
                        ProjectionPoints =
                        [
                            new LimitRunwayProjectionPoint(firstObservedAt, 40, 40, 40),
                            new LimitRunwayProjectionPoint(firstObservedAt.AddHours(1), 60, 54, 68),
                            new LimitRunwayProjectionPoint(reset, 100, 90, 100)
                        ]
                    }
                ]
            },
            "codex",
            firstObservedAt);

        Assert.NotNull(viewModel.ChartModel);
        Assert.Empty(viewModel.ChartModel.ReferenceProjectedPoints);
        Assert.Null(viewModel.ChartModel.ReferenceForecastCapturedAt);

        viewModel.ApplySignals(
            new UsageSignalsSnapshot
            {
                UsageTrends =
                [
                    new LimitUsageTrend(
                        "codex|300",
                        "codex",
                        "General",
                        "5h",
                        300,
                        reset,
                        [
                            new LimitUsagePoint(firstObservedAt.AddMinutes(-10), 35),
                            new LimitUsagePoint(firstObservedAt, 40),
                            new LimitUsagePoint(laterObservedAt, 48)
                        ],
                        IsMock: false)
                ],
                RunwayForecasts =
                [
                    Forecast(laterObservedAt, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
                    {
                        UsedPercent = 48,
                        ProjectionPoints =
                        [
                            new LimitRunwayProjectionPoint(laterObservedAt, 48, 48, 48),
                            new LimitRunwayProjectionPoint(laterObservedAt.AddHours(1), 65, 58, 74),
                            new LimitRunwayProjectionPoint(reset, 100, 90, 100)
                        ]
                    }
                ]
            },
            "codex",
            laterObservedAt);

        Assert.Equal(firstObservedAt, viewModel.ChartModel!.ReferenceForecastCapturedAt);
        Assert.NotEmpty(viewModel.ChartModel.ReferenceProjectedPoints);

        viewModel.ShowProjection = false;
        Assert.Empty(viewModel.ChartModel!.ReferenceProjectedPoints);
        Assert.Empty(viewModel.ChartModel.UnfavorableVarianceSegments);

        viewModel.ShowProjection = true;
        var rolloverAt = reset.AddMinutes(1);
        var nextReset = rolloverAt.AddHours(5);
        viewModel.ApplySignals(
            new UsageSignalsSnapshot
            {
                UsageTrends =
                [
                    new LimitUsageTrend(
                        "codex|300",
                        "codex",
                        "General",
                        "5h",
                        300,
                        nextReset,
                        [new LimitUsagePoint(rolloverAt, 8)],
                        IsMock: false)
                ],
                RunwayForecasts =
                [
                    Forecast(rolloverAt, nextReset, LimitRunwayForecastConfidence.Low, isMock: false) with
                    {
                        UsedPercent = 8,
                        ProjectionPoints =
                        [
                            new LimitRunwayProjectionPoint(rolloverAt, 8, 8, 8),
                            new LimitRunwayProjectionPoint(nextReset, 50, 35, 70)
                        ]
                    }
                ]
            },
            "codex",
            rolloverAt);

        Assert.NotNull(viewModel.ChartModel);
        Assert.Empty(viewModel.ChartModel.ReferenceProjectedPoints);
        Assert.Null(viewModel.ChartModel.ReferenceForecastCapturedAt);
    }

    [Fact]
    public void ViewModel_SelectsShortestWindowAndChartTogglesAreFunctional()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var shortReset = now.AddHours(2);
        var weeklyReset = now.AddDays(3);
        var shortTrend = Trend(now, shortReset, isMock: true, [20, 55, 96]);
        var weeklyTrend = new LimitUsageTrend(
            "codex|10080",
            "codex",
            "General",
            "Weekly",
            10_080,
            weeklyReset,
            [new LimitUsagePoint(now.AddHours(-2), 74), new LimitUsagePoint(now, 92)],
            IsMock: true);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [weeklyTrend, shortTrend],
            RunwayForecasts = [Forecast(now, shortReset, LimitRunwayForecastConfidence.Low, isMock: true)]
        }, "codex", now);

        Assert.Equal(2, viewModel.WindowOptions.Count);
        Assert.Equal("codex|300", viewModel.SelectedWindow?.BucketId);
        Assert.Equal("5-hour limit", viewModel.SelectedWindow?.Label);
        Assert.True(viewModel.HasChart);
        Assert.True(viewModel.ShowProjection);

        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 10_080);
        Assert.Equal("7-day limit", viewModel.SelectedWindow.Label);
        Assert.Equal(weeklyReset, viewModel.ChartModel!.ResetAt);
        Assert.Equal(74, viewModel.ChartModel.ActualPoints[0].UsedPercent);

        viewModel.ShowProjection = false;
        viewModel.ShowRange = false;
        Assert.Empty(viewModel.ChartModel!.ProjectedPoints);

        viewModel.ResetChartCommand.Execute(null);
        Assert.True(viewModel.ShowProjection);
        Assert.True(viewModel.ShowRange);
        Assert.NotEmpty(viewModel.ChartModel!.ProjectedPoints);
    }

    [Fact]
    public void UsageSignalsTracker_ExposesTimestampedObservedPoints()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var tracker = new UsageSignalsTracker(new FixedUserIdleTimeProvider());

        tracker.Observe(Snapshot(now, 20, reset), now);
        var signals = tracker.Observe(Snapshot(now.AddMinutes(10), 44, reset), now.AddMinutes(10));

        var trend = Assert.Single(signals.UsageTrends);
        Assert.Equal("codex|300", trend.BucketId);
        Assert.Collection(
            trend.Points,
            point => Assert.Equal(20, point.UsedPercent),
            point => Assert.Equal(44, point.UsedPercent));
        Assert.False(trend.IsMock);
    }

    private static LimitUsageTrend Trend(
        DateTimeOffset now,
        DateTimeOffset reset,
        bool isMock,
        IReadOnlyList<double> values)
    {
        var points = values
            .Select((value, index) => new LimitUsagePoint(now.AddMinutes(-40 + (index * 10)), value))
            .ToArray();
        return new LimitUsageTrend("codex|300", "codex", "General", "5h", 300, reset, points, isMock);
    }

    private static LimitRunwayForecast Forecast(
        DateTimeOffset now,
        DateTimeOffset reset,
        LimitRunwayForecastConfidence confidence,
        bool isMock)
    {
        return new LimitRunwayForecast(
            "codex|300",
            "codex",
            "General",
            "5h",
            300,
            reset,
            96,
            LimitRunwayForecastState.AtRisk,
            now.AddMinutes(48),
            0,
            5,
            TimeSpan.FromMinutes(40),
            IsActionable: true,
            IsMock: isMock,
            Confidence: confidence,
            SampleCount: 5);
    }

    private static UsageSnapshot Snapshot(DateTimeOffset now, double usedPercent, DateTimeOffset reset)
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
                    WindowLabel = "5h",
                    Label = "5h Window",
                    UsedPercent = usedPercent,
                    WindowDurationMins = 300,
                    ResetsAtUtc = reset,
                    ResetsAtUnixSeconds = reset.ToUnixTimeSeconds()
                }
            ]
        };
    }

    private static DateTimeOffset LocalTime(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private sealed class FixedUserIdleTimeProvider : IUserIdleTimeProvider
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
    }
}
