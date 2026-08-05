using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using Rect = System.Windows.Rect;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace PulseMeter.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UsageTrendWpfCollection
{
    public const string Name = "Usage trend WPF automation";
}

[Collection(UsageTrendWpfCollection.Name)]
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

                chart.Visibility = System.Windows.Visibility.Collapsed;
                Assert.False(peer.IsControlElement());
                Assert.False(peer.IsContentElement());
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
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
    public void ChartKeyboardNavigation_SelectsActualPointsAnnouncesDetailsAndDismissesWithEscape()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var start = new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero);
                var chart = new UsageTrendChart
                {
                    Width = 720,
                    Height = 380,
                    Model = CreateKeyboardNavigableChartModel(start)
                };
                window = new System.Windows.Window
                {
                    Content = chart,
                    Width = 760,
                    Height = 440,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                chart.UpdateLayout();

                var tooltip = Assert.IsType<System.Windows.Controls.ToolTip>(chart.ToolTip);
                var peer = UIElementAutomationPeer.CreatePeerForElement(chart);
                Assert.NotNull(peer);
                Assert.True(chart.Focusable);
                chart.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    0,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent,
                    Source = chart
                });
                chart.Dispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.True(chart.IsKeyboardFocused);
                Assert.Equal("Keyboard usage trend summary", peer.GetName());
                Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());

                var inputSource = Assert.IsAssignableFrom<System.Windows.PresentationSource>(
                    System.Windows.PresentationSource.FromVisual(chart));
                void Press(System.Windows.Input.Key key)
                {
                    var keyEvent = new System.Windows.Input.KeyEventArgs(
                        System.Windows.Input.Keyboard.PrimaryDevice,
                        inputSource,
                        0,
                        key)
                    {
                        RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent
                    };
                    chart.RaiseEvent(keyEvent);
                    Assert.True(keyEvent.Handled);
                }

                Press(System.Windows.Input.Key.Right);
                Assert.True(tooltip.IsOpen);
                Assert.Contains("Actual usage  35%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Assert.Contains("Earlier forecast", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Assert.Contains("Selected point: ", peer.GetHelpText());
                Assert.Equal("Keyboard usage trend summary", peer.GetName());

                chart.Model = chart.Model! with
                {
                    ActualPoints = chart.Model.ActualPoints
                        .Select(point => point with { Timestamp = point.Timestamp.AddSeconds(30) })
                        .ToArray(),
                    EvaluatedAt = start.AddHours(2).AddMinutes(1)
                };
                Assert.True(tooltip.IsOpen);
                Assert.Contains("Actual usage  35%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Assert.Contains("Selected point: ", peer.GetHelpText());

                Press(System.Windows.Input.Key.Right);
                Assert.Contains("Actual usage  48%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Press(System.Windows.Input.Key.Left);
                Assert.Contains("Actual usage  35%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Press(System.Windows.Input.Key.Left);
                Assert.Contains("Actual usage  35%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);

                Press(System.Windows.Input.Key.Right);
                Press(System.Windows.Input.Key.Right);
                Assert.Contains("Actual usage  68%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);
                Press(System.Windows.Input.Key.Right);
                Assert.Contains("Actual usage  68%", Assert.IsType<System.Windows.Controls.TextBlock>(tooltip.Content).Text);

                Press(System.Windows.Input.Key.Escape);
                Assert.False(tooltip.IsOpen);
                Assert.DoesNotContain("Selected point: ", peer.GetHelpText());
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The chart keyboard-navigation test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void LearningMomentumPreview_OpensForKeyboardFocusAndClosesForEscapeOrFocusLoss()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var section = new PulseMeter.Slices.UsageTrend.UI.UsageTrendSection();
                section.ApplyTemplate();
                section.Measure(new System.Windows.Size(900, 600));
                section.Arrange(new Rect(0, 0, 900, 600));
                section.UpdateLayout();

                var info = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.Button>(section),
                    button => AutomationProperties.GetName(button) == "Preview completed momentum gauge");
                info.Visibility = System.Windows.Visibility.Visible;
                var tooltip = Assert.IsType<System.Windows.Controls.ToolTip>(info.ToolTip);
                var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(info);

                Assert.Equal(System.Windows.Controls.Primitives.PlacementMode.Bottom, tooltip.Placement);
                Assert.Same(info, tooltip.PlacementTarget);
                Assert.Equal("Preview completed momentum gauge", peer.GetName());
                Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());

                info.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    0,
                    null,
                    info)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.GotKeyboardFocusEvent
                });
                Assert.True(tooltip.IsOpen);

                using var inputSource = new System.Windows.Interop.HwndSource(
                    new System.Windows.Interop.HwndSourceParameters("MomentumPreviewKeyboardTest")
                    {
                        Width = 1,
                        Height = 1,
                        WindowStyle = 0
                    });
                var escape = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    inputSource,
                    0,
                    System.Windows.Input.Key.Escape)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
                };
                info.RaiseEvent(escape);
                Assert.True(escape.Handled);
                Assert.False(tooltip.IsOpen);

                tooltip.IsOpen = true;
                info.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    0,
                    info,
                    null)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent
                });
                Assert.False(tooltip.IsOpen);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The learning momentum preview keyboard interaction test did not finish.");
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

        var model = Assert.IsType<UsageTrendChartModel>(chart);
        Assert.Equal(
            "Building 24h baseline • 2.6h usable now · 2 samples",
            model.Summary.ConfidenceText);
        Assert.True(model.Summary.Momentum.IsLearning);
        Assert.Equal("11% ready", model.Summary.Momentum.ValueText);
        Assert.Equal("21.4h more data needed", model.Summary.Momentum.StateText);
        Assert.Equal("2.6h usable now · 2 samples", model.Summary.Momentum.BaselineText);
        Assert.Equal(2.6 / 24, model.Summary.Momentum.BaselineProgress, precision: 3);
        Assert.Contains("21.4h more data needed", model.Summary.Momentum.AccessibleSummary);
        Assert.Contains("2.6h currently usable from 2 samples", model.Summary.Momentum.AccessibleSummary);
    }

    [Fact]
    public void Presenter_WeeklyMomentumUsesRetainedTimelineInsteadOfShrinkingRollingForecastEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var points = Enumerable.Range(0, 11)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 10), index))
            .ToArray();

        var earlierForecastEvidence = UsageTrendPresenter.BuildUsageMomentum(
            points,
            10_080,
            TimeSpan.FromHours(7.9),
            5,
            []);
        var laterShrinkingForecastEvidence = UsageTrendPresenter.BuildUsageMomentum(
            points,
            10_080,
            TimeSpan.FromHours(7.7),
            4,
            []);

        Assert.True(earlierForecastEvidence.IsLearning);
        Assert.Equal("10h usable now · 11 samples", earlierForecastEvidence.BaselineText);
        Assert.Equal(earlierForecastEvidence.ValueText, laterShrinkingForecastEvidence.ValueText);
        Assert.Equal(earlierForecastEvidence.BaselineText, laterShrinkingForecastEvidence.BaselineText);
        Assert.Equal(10d / 24, laterShrinkingForecastEvidence.BaselineProgress, precision: 3);
    }

    [Fact]
    public void Presenter_WeeklyMomentumAccumulatesBeyondARecentGapWithoutCountingTheGap()
    {
        var now = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var gap = new UsageTrendGap(now.AddHours(-20), now.AddHours(-18));
        var nearlyReadyPoints = Enumerable.Range(0, 26)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 25), index))
            .ToArray();
        var readyPoints = Enumerable.Range(0, 27)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 26), index))
            .ToArray();

        var nearlyReady = UsageTrendPresenter.BuildUsageMomentum(
            nearlyReadyPoints,
            10_080,
            measurementGaps: [gap]);
        var ready = UsageTrendPresenter.BuildUsageMomentum(
            readyPoints,
            10_080,
            measurementGaps: [gap]);

        Assert.True(nearlyReady.IsLearning);
        Assert.Equal("96% ready", nearlyReady.ValueText);
        Assert.Equal("23h usable now · 26 samples", nearlyReady.BaselineText);
        Assert.False(ready.IsLearning);
        Assert.Equal("vs median day", ready.BaselineText);
    }

    [Fact]
    public void Presenter_WeeklyMomentumBecomesReadyAtExactlyTwentyFourUsableHours()
    {
        var now = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var points = Enumerable.Range(0, 25)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 24), index))
            .ToArray();

        var momentum = UsageTrendPresenter.BuildUsageMomentum(
            points,
            10_080,
            measurementGaps: []);

        Assert.False(momentum.IsLearning);
        Assert.Equal("vs median day", momentum.BaselineText);
        Assert.Equal("→ 0%/h", momentum.ValueText);
        Assert.Equal("pace steady", momentum.StateText);
    }

    [Fact]
    public void Presenter_FiveHourLearningMomentumShowsRealCollectionProgress()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var points = new[]
        {
            new UsageTrendPoint(now.AddHours(-2.2), 20),
            new UsageTrendPoint(now, 24)
        };

        var momentum = UsageTrendPresenter.BuildUsageMomentum(
            points,
            300,
            TimeSpan.FromHours(2.2),
            54);

        Assert.True(momentum.IsLearning);
        Assert.Equal("44% ready", momentum.ValueText);
        Assert.Equal("2.8h more data needed", momentum.StateText);
        Assert.Equal("2.2h usable now · 54 samples", momentum.BaselineText);
        Assert.Equal(0.44, momentum.BaselineProgress, precision: 3);
        Assert.Contains("Baseline progress: 44% ready", momentum.AccessibleSummary);
    }

    [Fact]
    public void Presenter_MomentumStaysLearningUntilTheDisplayedBaselineTargetIsComplete()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var fiveHourPoints = Enumerable.Range(0, 6)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 5), index))
            .ToArray();
        var weeklyPoints = Enumerable.Range(0, 26)
            .Select(index => new UsageTrendPoint(now.AddHours(index - 25), index * 0.2))
            .ToArray();

        var fiveHour = UsageTrendPresenter.BuildUsageMomentum(
            fiveHourPoints,
            300,
            TimeSpan.FromHours(4.9),
            30);
        var weekly = UsageTrendPresenter.BuildUsageMomentum(
            weeklyPoints,
            10_080,
            TimeSpan.FromHours(23.9),
            120);

        Assert.True(fiveHour.IsLearning);
        Assert.Equal("6m more data needed", fiveHour.StateText);
        Assert.True(weekly.IsLearning);
        Assert.Equal("6m more data needed", weekly.StateText);
    }

    [Fact]
    public void MomentumGauge_LearningStateHidesNeedleUntilMomentumIsMeasured()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var gauge = new UsageMomentumGauge
                {
                    Width = 160,
                    Height = 58,
                    IsLearning = true,
                    BaselineProgress = 0.5
                };
                gauge.Measure(new System.Windows.Size(160, 58));
                gauge.Arrange(new Rect(0, 0, 160, 58));

                AutomationProperties.SetName(
                    gauge,
                    "Usage momentum is building its baseline. 2.5h of 5h collected. 54 samples collected.");
                var peer = UIElementAutomationPeer.CreatePeerForElement(gauge);
                Assert.NotNull(peer);
                Assert.Equal(
                    "Usage momentum is building its baseline. 2.5h of 5h collected. 54 samples collected.",
                    peer.GetName());
                Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
                Assert.True(peer.IsControlElement());
                Assert.True(peer.IsContentElement());

                gauge.Visibility = System.Windows.Visibility.Collapsed;
                Assert.False(peer.IsControlElement());
                Assert.False(peer.IsContentElement());
                gauge.Visibility = System.Windows.Visibility.Visible;

                var learning = RenderGauge(gauge);
                Assert.Equal(0, ReadAlpha(learning, 80, 51));
                AssertLearningArcIsNeutral(learning);

                var measuredGauge = new UsageMomentumGauge
                {
                    Width = 160,
                    Height = 58,
                    IsLearning = false,
                    Value = 0.5
                };
                measuredGauge.Measure(new System.Windows.Size(160, 58));
                measuredGauge.Arrange(new Rect(0, 0, 160, 58));
                var measured = RenderGauge(measuredGauge);
                Assert.True(ReadAlpha(measured, 80, 51) > 0);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The momentum gauge render test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    private static void AssertLearningArcIsNeutral(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var blue = pixels[index];
            var green = pixels[index + 1];
            var red = pixels[index + 2];
            var alpha = pixels[index + 3];
            if (alpha == 0)
            {
                continue;
            }

            Assert.False(blue > red + 80 && blue > green + 40, "The learning gauge must not render a blue progress arc before the baseline is ready.");
        }
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
        Assert.False(momentum.IsLearning);
    }

    [Fact]
    public void Presenter_MomentumForSevenDayWindow_UsesPriorDayHourlyMedian()
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
        Assert.False(momentum.IsLearning);
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
        Assert.DoesNotContain("Plan your next block", chart.AccessibleSummary);
        Assert.DoesNotContain("Next constraint", chart.AccessibleSummary);
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
    public void Presenter_NextBlockAdvisor_TreatsAnAtRiskRangeAsPotentialInterruption()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(40),
            LatestExhaustsAtUtc = now.AddMinutes(80)
        };

        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, reset, isMock: false, [72, 78, 84]),
            forecast,
            now,
            showProjection: true,
            showRange: true,
            selectedBlockDurationMinutes: 60);

        var advisor = Assert.IsType<UsageTrendChartModel>(chart).BlockAdvisor;
        Assert.NotNull(advisor);
        Assert.Equal("May be interrupted", advisor.State);
        Assert.Equal("1h", Assert.Single(advisor.Options, option => option.IsSelected).Label);
        Assert.Contains("Capacity may run short between", advisor.Detail);
        Assert.DoesNotContain("project", advisor.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("thread", advisor.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", advisor.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", advisor.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presenter_NextBlockAdvisor_OnTrackForecastLikelyFitsBeforeReset()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.High, isMock: false) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, reset, isMock: false, [42, 44, 46]),
            forecast,
            now,
            showProjection: true,
            showRange: true,
            selectedBlockDurationMinutes: 120);

        var advisor = Assert.IsType<UsageTrendChartModel>(chart).BlockAdvisor;
        Assert.NotNull(advisor);
        Assert.Equal("Likely fits", advisor.State);
        Assert.Equal(UsageTrendBlockAdvisorStatus.LikelyFits, advisor.Status);
        Assert.Equal("2h", Assert.Single(advisor.Options, option => option.IsSelected).Label);
        Assert.Contains("stay below the limit until reset", advisor.Detail);
    }

    [Theory]
    [InlineData(LimitRunwayForecastState.OnTrack)]
    [InlineData(LimitRunwayForecastState.AtRisk)]
    public void Presenter_NextBlockAdvisor_LowConfidenceLiveForecastStaysNeutral(
        LimitRunwayForecastState state)
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Low, isMock: false) with
        {
            State = state,
            EarliestExhaustsAtUtc = now.AddMinutes(40),
            LatestExhaustsAtUtc = now.AddMinutes(80)
        };

        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, reset, isMock: false, [62, 70, 78]),
            forecast,
            now,
            showProjection: true,
            showRange: true,
            selectedBlockDurationMinutes: 60);

        var advisor = Assert.IsType<UsageTrendChartModel>(chart).BlockAdvisor;
        Assert.NotNull(advisor);
        Assert.Equal("Still learning", advisor.State);
        Assert.Contains("Not enough evidence to plan this 1h block yet", advisor.Detail);
        Assert.DoesNotContain("between", advisor.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("earliest", advisor.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest", advisor.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presenter_NextBlockAdvisor_ResetBoundaryOverridesLearningState()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(1);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Low, isMock: false) with
        {
            State = LimitRunwayForecastState.Learning
        };

        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, reset, isMock: false, [40, 42, 44]),
            forecast,
            now,
            showProjection: true,
            showRange: true,
            selectedBlockDurationMinutes: 120);

        var advisor = Assert.IsType<UsageTrendChartModel>(chart).BlockAdvisor;
        Assert.NotNull(advisor);
        Assert.Equal("May be interrupted", advisor.State);
        Assert.Contains("reaches the reset", advisor.Detail);
        Assert.Contains("not task cost", advisor.Detail);
    }

    [Theory]
    [InlineData(LimitRunwayForecastState.Exhausted, "Wait for reset")]
    [InlineData(LimitRunwayForecastState.Learning, "Still learning")]
    public void Presenter_NextBlockAdvisor_StaysNeutralWhenExhaustedOrLearning(
        LimitRunwayForecastState state,
        string expectedState)
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            State = state,
            UsedPercent = state == LimitRunwayForecastState.Exhausted ? 100 : 55
        };

        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, reset, isMock: false, [40, 46, 55]),
            forecast,
            now,
            showProjection: true,
            showRange: true);

        var advisor = Assert.IsType<UsageTrendChartModel>(chart).BlockAdvisor;
        Assert.NotNull(advisor);
        Assert.Equal(expectedState, advisor.State);
        Assert.Contains("not task cost", advisor.Detail);
    }

    [Fact]
    public void Presenter_NextBlockAdvisor_UsesWindowAppropriateDurationChoices()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var reset = now.AddDays(4);
        var weeklyTrend = Trend(now, reset, isMock: false, [40, 44, 48]) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7d",
            WindowDurationMins = 10_080
        };
        var weeklyForecast = Forecast(now, reset, LimitRunwayForecastConfidence.High, isMock: false) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7d",
            WindowDurationMins = 10_080,
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null
        };

        var weeklyChart = new UsageTrendPresenter().BuildChart(
            weeklyTrend,
            weeklyForecast,
            now,
            showProjection: true,
            showRange: true,
            selectedBlockDurationMinutes: 480);
        var shortChart = new UsageTrendPresenter().BuildChart(
            Trend(now, now.AddHours(3), isMock: false, [40, 44, 48]),
            Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false),
            now,
            showProjection: true,
            showRange: true);

        Assert.Equal(["1h", "2h", "4h", "1 day (8h)"], weeklyChart!.BlockAdvisor!.Options.Select(option => option.Label));
        Assert.Equal(["15m", "30m", "1h", "2h"], shortChart!.BlockAdvisor!.Options.Select(option => option.Label));
    }

    [Fact]
    public void Presenter_NextConstraint_PrefersTheFiveHourLimitWhenItExhaustsFirst()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(35),
            LatestExhaustsAtUtc = now.AddMinutes(50)
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 5h limit", constraint.Headline);
        Assert.Contains("Likely first around", constraint.Detail);
        Assert.Contains("Medium confidence", constraint.Detail);
    }

    [Fact]
    public void Presenter_NextConstraint_PrefersTheSevenDayLimitWhenItExhaustsFirst()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.High, isMock: false) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            EarliestExhaustsAtUtc = now.AddHours(5),
            LatestExhaustsAtUtc = now.AddHours(6)
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 7d limit", constraint.Headline);
        Assert.Contains("Likely first around", constraint.Detail);
    }

    [Fact]
    public void Presenter_NextConstraint_ExhaustedLimitWinsBeforeOtherForecastStates()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Low, isMock: false) with
        {
            State = LimitRunwayForecastState.Exhausted,
            UsedPercent = 100,
            IsActionable = false
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High);

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 5h limit", constraint.Headline);
        Assert.StartsWith("Blocked now · resets ", constraint.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Presenter_NextConstraint_UsesRealOnTrackForecastsBelowTheActionabilityThreshold()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.High, isMock: false) with
        {
            State = LimitRunwayForecastState.OnTrack,
            IsActionable = false,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.Medium) with
        {
            State = LimitRunwayForecastState.OnTrack,
            IsActionable = false,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · No blocker forecast", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_UsesReliableAtRiskForecastBelowTheActionabilityThreshold()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            IsActionable = false,
            EarliestExhaustsAtUtc = now.AddMinutes(35),
            LatestExhaustsAtUtc = now.AddMinutes(50)
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            State = LimitRunwayForecastState.OnTrack,
            IsActionable = false,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 5h limit", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_OverlappingForecastRangesAreTooCloseToCall()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(90)
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(60),
            LatestExhaustsAtUtc = now.AddHours(2)
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · Too close to call", constraint.Headline);
        Assert.Contains("forecast ranges overlap", constraint.Detail);
    }

    [Fact]
    public void Presenter_NextConstraint_LowConfidenceLiveForecastStaysLearning()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Low, isMock: false);
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · Still learning", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_BothOnTrackMeansNoBlockerForecast()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.High, isMock: false) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.Medium) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · No blocker forecast", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_UsesDateAndTimeForAWeeklyCrossDayForecast()
    {
        var now = new DateTimeOffset(2026, 7, 21, 22, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.High, isMock: false) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            ExhaustsAtUtc = now.AddDays(1).AddHours(1),
            EarliestExhaustsAtUtc = now.AddDays(1),
            LatestExhaustsAtUtc = now.AddDays(1).AddHours(2)
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 7d limit", constraint.Headline);
        Assert.Contains(weekly.ExhaustsAtUtc!.Value.ToLocalTime().ToString("ddd, MMM d, h:mm tt", CultureInfo.CurrentCulture), constraint.Detail);
    }

    [Fact]
    public void Presenter_NextConstraint_AllMockForecastsCanProvideAHarnessConstraint()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Low, isMock: true) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(35),
            LatestExhaustsAtUtc = now.AddMinutes(50)
        };
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.Low) with
        {
            IsMock = true,
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · 5h limit", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_MixedMockAndLiveForecastsStayLearning()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.High, isMock: true);
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High);

        var constraint = BuildNextConstraint(now, fiveHour, weekly);

        Assert.Equal("Next constraint · Still learning", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_UsesTheReliableForecastWhenTheOtherWindowIsUnavailable()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false);
        var expiredWeekly = WeeklyForecast(now, now.AddMinutes(-1), LimitRunwayForecastConfidence.High);

        var constraint = BuildNextConstraint(now, fiveHour, expiredWeekly);

        Assert.Equal("Next constraint · 5h limit", constraint.Headline);
    }

    [Fact]
    public void Presenter_NextConstraint_UsesTheOnlyAvailableWeeklyForecast()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var weekly = WeeklyForecast(now, now.AddDays(4), LimitRunwayForecastConfidence.High) with
        {
            EarliestExhaustsAtUtc = now.AddHours(5),
            LatestExhaustsAtUtc = now.AddHours(6)
        };

        var constraint = BuildNextConstraint(now, weekly);

        Assert.Equal("Next constraint · 7d limit", constraint.Headline);
        Assert.Contains("Likely first around", constraint.Detail);
    }

    [Fact]
    public void ViewModel_NextConstraintDoesNotChangeWithTheSelectedChartWindow()
    {
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var fiveHour = Forecast(now, now.AddHours(3), LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            IsActionable = false,
            EarliestExhaustsAtUtc = now.AddMinutes(35),
            LatestExhaustsAtUtc = now.AddMinutes(50)
        };
        var weeklyReset = now.AddDays(4);
        var weekly = WeeklyForecast(now, weeklyReset, LimitRunwayForecastConfidence.High) with
        {
            State = LimitRunwayForecastState.OnTrack,
            IsActionable = false,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        var weeklyTrend = Trend(now, weeklyReset, isMock: false, [42, 44, 46]) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7d",
            WindowDurationMins = 10_080
        };
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, fiveHour.ResetsAtUtc, isMock: false, [40, 45, 50]), weeklyTrend],
            RunwayForecasts = [fiveHour, weekly]
        }, "codex", now);
        var summaryAtFiveHours = (viewModel.NextConstraintHeadline, viewModel.NextConstraintDetail);

        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 10_080);

        Assert.Equal(summaryAtFiveHours, (viewModel.NextConstraintHeadline, viewModel.NextConstraintDetail));
    }

    [Fact]
    public void NextBlockAdvisor_MarkupUsesASelectableAccessibleDurationGroup()
    {
        var workspace = TestWorkspace.FindRoot();
        var xaml = File.ReadAllText(Path.Combine(
            workspace,
            "src",
            "PulseMeter",
            "Slices",
            "BlockPlanner",
            "UI",
            "BlockPlannerSection.xaml"));

        Assert.Contains("<ItemsControl ItemsSource=\"{Binding BlockOptions}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding BlockOptions}\"", xaml);
        Assert.Contains("<RadioButton", xaml);
        Assert.Contains("GroupName=\"NextBlockDuration\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=OneWay}\"", xaml);
        Assert.Contains("The selected duration is announced as selected.", xaml);
        Assert.Contains("Command=\"{Binding DataContext.SelectBlockDurationCommand, RelativeSource={RelativeSource AncestorType={x:Type UserControl}}}\"", xaml);
        Assert.Contains("CommandParameter=\"{Binding DurationMinutes}\"", xaml);
        Assert.Contains("Text=\"{Binding NextConstraintHeadline}\"", xaml);
        Assert.Contains("Text=\"{Binding NextConstraintDetail}\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding NextConstraintAccessibleSummary}\"", xaml);
        Assert.Contains("IsKeyboardFocused", xaml);
        Assert.Contains("Recovery watch", xaml);
        Assert.Contains("Command=\"{Binding ToggleRecoveryWatchCommand}\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding RecoveryWatchAccessibleSummary}\"", xaml);
        Assert.Contains("DataTrigger Binding=\"{Binding CanManageRecoveryWatch}\" Value=\"False\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.Contains("x:Name=\"RecoveryConfirmationTextBlock\"", xaml);
        Assert.Contains("NotifyOnTargetUpdated=True", xaml);
        Assert.Contains("RecoveryConfirmationTextBlock_TargetUpdated", xaml);
        Assert.Contains("Waiting for limit data", xaml);
        Assert.Contains("The planner will appear after PulseMeter receives a live 5h or 7d usage sample.", xaml);
        Assert.Contains("DataTrigger Binding=\"{Binding HasBlockAdvisor}\" Value=\"False\"", xaml);
        Assert.Contains("IsKeyboardFocusWithin", xaml);
        Assert.Contains("BorderThickness\" Value=\"2\"", xaml);
    }

    [Fact]
    public void RecoveryWatch_EnablesCancelsAndKeepsScopesSeparate()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var shortReset = now.AddHours(3);
        var weeklyReset = now.AddDays(4);
        var shortTrend = Trend(now, shortReset, isMock: false, [70, 78, 86]);
        var weeklyTrend = Trend(now, weeklyReset, isMock: false, [70, 78, 86]) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7d",
            WindowDurationMins = 10_080
        };
        var shortForecast = Forecast(now, shortReset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(45)
        };
        var weeklyForecast = WeeklyForecast(now, weeklyReset, LimitRunwayForecastConfidence.High) with
        {
            EarliestExhaustsAtUtc = now.AddHours(4),
            LatestExhaustsAtUtc = now.AddHours(6)
        };
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [shortTrend, weeklyTrend],
            RunwayForecasts = [shortForecast, weeklyForecast]
        }, "codex", now);
        viewModel.SelectedBlockDurationMinutes = 60;
        viewModel.ToggleRecoveryWatchCommand.Execute(null);
        Assert.True(viewModel.HasActiveRecoveryWatch);
        Assert.Contains("reset in 3h 0m", viewModel.RecoveryWatchText);

        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 10_080);
        viewModel.SelectedBlockDurationMinutes = 480;
        viewModel.ToggleRecoveryWatchCommand.Execute(null);
        Assert.Equal(2, viewModel.CaptureRecoveryWatches().Count);

        viewModel.ToggleRecoveryWatchCommand.Execute(null);
        Assert.Single(viewModel.CaptureRecoveryWatches());
        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 300);
        Assert.True(viewModel.HasActiveRecoveryWatch);
    }

    [Fact]
    public void RecoveryWatch_CompletesOnceForEarlierLiveRecoveryAndAtReset()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var completions = new List<UsageTrendRecoveryCompletedEventArgs>();
        viewModel.RecoveryWatchCompleted += (_, completion) => completions.Add(completion);
        var atRisk = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(45)
        };

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
            RunwayForecasts = [atRisk]
        }, "codex", now);
        viewModel.SelectedBlockDurationMinutes = 60;
        viewModel.ToggleRecoveryWatchCommand.Execute(null);

        var recovered = atRisk with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            EarliestExhaustsAtUtc = null,
            LatestExhaustsAtUtc = null
        };
        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now.AddMinutes(15), reset, isMock: false, [65, 66, 67])],
            RunwayForecasts = [recovered]
        }, "codex", now.AddMinutes(15));
        viewModel.Refresh(now.AddMinutes(30));

        Assert.Single(completions);
        Assert.Equal("Ready to code again", completions[0].Title);
        Assert.Contains("now likely fits", viewModel.RecoveryConfirmationText);
        Assert.Empty(viewModel.CaptureRecoveryWatches());

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
            RunwayForecasts = [atRisk]
        }, "codex", now);
        viewModel.SelectedBlockDurationMinutes = 60;
        viewModel.ToggleRecoveryWatchCommand.Execute(null);
        viewModel.Refresh(reset);
        viewModel.Refresh(reset.AddMinutes(15));

        Assert.Equal(2, completions.Count);
        Assert.Equal("Quota reset reached", completions[1].Title);
        Assert.Contains("re-sync if no fresh sample", completions[1].Message);
    }

    [Fact]
    public void RecoveryWatch_SuppressesMockDataAndRestoresPersistedScopes()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var mock = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        mock.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, reset, isMock: true, [70, 78, 86])],
            RunwayForecasts = [Forecast(now, reset, LimitRunwayForecastConfidence.High, isMock: true)]
        }, "codex", now);
        mock.SelectedBlockDurationMinutes = 60;
        mock.ToggleRecoveryWatchCommand.Execute(null);
        Assert.Empty(mock.CaptureRecoveryWatches());

        var restored = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        restored.RestoreRecoveryWatches([new RecoveryWatchSettings("codex", 300, 60, reset)]);
        restored.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
            RunwayForecasts = [Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false)]
        }, "codex", now);
        Assert.True(restored.HasActiveRecoveryWatch);
        Assert.Contains("PulseMeter may alert sooner", restored.RecoveryWatchText);
    }

    [Fact]
    public void RecoveryWatch_RestoreIgnoresNullPersistedEntries()
    {
        var reset = new DateTimeOffset(2026, 7, 22, 13, 0, 0, TimeSpan.Zero);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.RestoreRecoveryWatches(
        [
            null!,
            new RecoveryWatchSettings("codex", 300, 60, reset)
        ]);

        var restored = Assert.Single(viewModel.CaptureRecoveryWatches());
        Assert.Equal("codex", restored.LimitKey);
        Assert.Equal(reset, restored.ResetAtUtc);
    }

    [Fact]
    public void RecoveryWatch_ExpiredRestoredScopeMissingFromFirstLiveSnapshot_CompletesOnce()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var expiredReset = now.AddMinutes(-5);
        var liveReset = now.AddHours(3);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var completions = new List<UsageTrendRecoveryCompletedEventArgs>();
        viewModel.RecoveryWatchCompleted += (_, completion) => completions.Add(completion);
        viewModel.RestoreRecoveryWatches([new RecoveryWatchSettings("codex", 300, 60, expiredReset)]);

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, liveReset, isMock: true, [70, 78, 86])],
            RunwayForecasts = [Forecast(now, liveReset, LimitRunwayForecastConfidence.Medium, isMock: true)]
        }, "codex", now);

        Assert.Single(viewModel.CaptureRecoveryWatches());
        Assert.Empty(completions);

        var liveTrend = Trend(now, liveReset, isMock: false, [70, 78, 86]) with
        {
            LimitKey = "other",
            BucketId = "other|300"
        };
        var liveForecast = Forecast(now, liveReset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            LimitKey = "other",
            BucketId = "other|300"
        };
        var authoritativeSnapshot = new UsageSignalsSnapshot
        {
            UsageTrends = [liveTrend],
            RunwayForecasts = [liveForecast]
        };

        viewModel.ApplySignals(authoritativeSnapshot, "other", now);
        viewModel.ApplySignals(authoritativeSnapshot, "other", now);

        var completion = Assert.Single(completions);
        Assert.Equal("Quota reset reached", completion.Title);
        Assert.Empty(viewModel.CaptureRecoveryWatches());
    }

    [Fact]
    public void RecoveryWatch_ReconcilesStalePersistedResetBeforeConsideringCompletion()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var persistedReset = now.AddMinutes(-5);
        var authoritativeReset = now.AddHours(2);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var completions = new List<UsageTrendRecoveryCompletedEventArgs>();
        viewModel.RecoveryWatchCompleted += (_, completion) => completions.Add(completion);
        viewModel.RestoreRecoveryWatches([new RecoveryWatchSettings("codex", 300, 60, persistedReset)]);
        var atRisk = Forecast(now, authoritativeReset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(45)
        };

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, authoritativeReset, isMock: false, [70, 78, 86])],
            RunwayForecasts = [atRisk]
        }, "codex", now);
        viewModel.Refresh(now.AddMinutes(15));
        viewModel.Refresh(now.AddMinutes(30));

        var retained = Assert.Single(viewModel.CaptureRecoveryWatches());
        Assert.Equal(authoritativeReset, retained.ResetAtUtc);
        Assert.Empty(completions);
    }

    [Fact]
    public void RecoveryWatch_CompletesWhenFreshPostResetTrendMovesToNextWindow()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var afterReset = reset.AddMinutes(1);
        var nextReset = reset.AddHours(8);
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
        var completions = new List<UsageTrendRecoveryCompletedEventArgs>();
        viewModel.RecoveryWatchCompleted += (_, completion) => completions.Add(completion);
        var atRisk = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(45)
        };

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
            RunwayForecasts = [atRisk]
        }, "codex", now);
        viewModel.SelectedBlockDurationMinutes = 60;
        viewModel.ToggleRecoveryWatchCommand.Execute(null);

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [Trend(afterReset, nextReset, isMock: false, [12, 15, 18])],
            RunwayForecasts = [Forecast(afterReset, nextReset, LimitRunwayForecastConfidence.Medium, isMock: false)]
        }, "codex", afterReset);
        viewModel.Refresh(afterReset.AddMinutes(1));

        var completion = Assert.Single(completions);
        Assert.Equal("Quota reset reached", completion.Title);
        Assert.Empty(viewModel.CaptureRecoveryWatches());
    }

    [Fact]
    public void RecoveryConfirmation_TextBlockExposesPoliteLiveAutomationPeer()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
                var reset = now.AddHours(3);
                var atRisk = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
                {
                    EarliestExhaustsAtUtc = now.AddMinutes(30),
                    LatestExhaustsAtUtc = now.AddMinutes(45)
                };
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
                    RunwayForecasts = [atRisk]
                }, "codex", now);
                viewModel.SelectedBlockDurationMinutes = 60;
                viewModel.ToggleRecoveryWatchCommand.Execute(null);
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now.AddMinutes(5), reset, isMock: false, [65, 66, 67])],
                    RunwayForecasts = [atRisk with
                    {
                        State = LimitRunwayForecastState.OnTrack,
                        ExhaustsAtUtc = null,
                        EarliestExhaustsAtUtc = null,
                        LatestExhaustsAtUtc = null
                    }]
                }, "codex", now.AddMinutes(5));

                var section = new PulseMeter.Slices.BlockPlanner.UI.BlockPlannerSection { DataContext = viewModel };
                section.ApplyTemplate();
                section.Measure(new System.Windows.Size(900, 600));
                section.Arrange(new Rect(0, 0, 900, 600));
                section.UpdateLayout();
                var confirmation = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.TextBlock>(section),
                    text => text.Text == viewModel.RecoveryConfirmationText);
                var peer = UIElementAutomationPeer.CreatePeerForElement(confirmation);

                Assert.NotNull(peer);
                Assert.Equal("Recovery confirmation", peer.GetName());
                Assert.Equal(viewModel.RecoveryConfirmationText, peer.GetHelpText());
                Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The recovery confirmation automation test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void RecoveryConfirmation_RequestsLiveRegionChangedAfterBindingUpdate()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
                var reset = now.AddHours(3);
                var atRisk = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
                {
                    EarliestExhaustsAtUtc = now.AddMinutes(30),
                    LatestExhaustsAtUtc = now.AddMinutes(45)
                };
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
                    RunwayForecasts = [atRisk]
                }, "codex", now);
                viewModel.SelectedBlockDurationMinutes = 60;
                viewModel.ToggleRecoveryWatchCommand.Execute(null);

                var liveRegionChangedRaised = false;
                var section = new PulseMeter.Slices.BlockPlanner.UI.BlockPlannerSection { DataContext = viewModel };
                section.RecoveryConfirmationLiveRegionChanged += (_, _) => liveRegionChangedRaised = true;
                window = new System.Windows.Window
                {
                    Content = section,
                    Width = 900,
                    Height = 600,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                section.UpdateLayout();

                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now.AddMinutes(5), reset, isMock: false, [65, 66, 67])],
                    RunwayForecasts = [atRisk with
                    {
                        State = LimitRunwayForecastState.OnTrack,
                        ExhaustsAtUtc = null,
                        EarliestExhaustsAtUtc = null,
                        LatestExhaustsAtUtc = null
                    }]
                }, "codex", now.AddMinutes(5));
                section.Dispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                Assert.True(
                    liveRegionChangedRaised,
                    "Recovery confirmation did not request the LiveRegionChanged automation event.");
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The live-region event test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void BlockPlanner_WindowSelectorShowsKeyboardFocusBorder()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
                var reset = now.AddHours(3);
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
                    RunwayForecasts = [Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false)]
                }, "codex", now);
                var section = new PulseMeter.Slices.BlockPlanner.UI.BlockPlannerSection { DataContext = viewModel };
                window = new System.Windows.Window
                {
                    Content = section,
                    Width = 900,
                    Height = 600,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                section.UpdateLayout();

                var selector = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.ListBox>(section),
                    list => AutomationProperties.GetName(list) == "Block planner limit window");
                var item = Assert.IsType<System.Windows.Controls.ListBoxItem>(
                    selector.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(item.Focus());
                section.Dispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                item.ApplyTemplate();
                var segment = Assert.IsType<System.Windows.Controls.Border>(
                    item.Template.FindName("Segment", item));
                var borderBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(segment.BorderBrush);

                Assert.True(item.IsKeyboardFocusWithin);
                Assert.Equal(new System.Windows.Thickness(2), segment.BorderThickness);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0x1F, 0x73, 0xFF), borderBrush.Color);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The Block planner keyboard-focus test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void UsageTrend_WindowSelectorShowsKeyboardFocusBorder()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
                var reset = now.AddHours(3);
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [70, 78, 86])],
                    RunwayForecasts = [Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false)]
                }, "codex", now);
                var section = new PulseMeter.Slices.UsageTrend.UI.UsageTrendSection { DataContext = viewModel };
                window = new System.Windows.Window
                {
                    Content = section,
                    Width = 900,
                    Height = 600,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                section.UpdateLayout();

                var selector = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.ListBox>(section),
                    list => AutomationProperties.GetName(list) == "Usage limit window");
                var item = Assert.IsType<System.Windows.Controls.ListBoxItem>(
                    selector.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(item.Focus());
                section.Dispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                item.ApplyTemplate();
                var segment = Assert.IsType<System.Windows.Controls.Border>(
                    item.Template.FindName("Segment", item));
                var borderBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(segment.BorderBrush);

                Assert.True(item.IsKeyboardFocusWithin);
                Assert.Equal(new System.Windows.Thickness(2), segment.BorderThickness);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0x1F, 0x73, 0xFF), borderBrush.Color);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The Usage Trend keyboard-focus test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void BlockPlanner_WithoutLimitDataShowsItsWaitingState()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var section = new PulseMeter.Slices.BlockPlanner.UI.BlockPlannerSection
                {
                    DataContext = new UsageTrendSectionViewModel(new UsageTrendPresenter())
                };
                section.ApplyTemplate();
                section.Measure(new System.Windows.Size(900, 600));
                section.Arrange(new Rect(0, 0, 900, 600));
                section.UpdateLayout();

                var waiting = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.Border>(section),
                    border => AutomationProperties.GetName(border) == "Waiting for limit data");
                var planner = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.Border>(section),
                    border => AutomationProperties.GetName(border) == "Plan your next block");

                Assert.Equal(System.Windows.Visibility.Visible, waiting.Visibility);
                Assert.Equal(System.Windows.Visibility.Collapsed, planner.Visibility);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The Block planner empty-state test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void NextBlockAdvisor_RadioClickUpdatesTheViewModelAndAdvisor()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
                var reset = now.AddHours(3);
                var forecast = Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false) with
                {
                    EarliestExhaustsAtUtc = now.AddMinutes(40),
                    LatestExhaustsAtUtc = now.AddMinutes(80)
                };
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [72, 78, 84])],
                    RunwayForecasts = [forecast]
                }, "codex", now);

                var section = new PulseMeter.Slices.BlockPlanner.UI.BlockPlannerSection { DataContext = viewModel };
                section.ApplyTemplate();
                section.Measure(new System.Windows.Size(900, 600));
                section.Arrange(new Rect(0, 0, 900, 600));
                section.UpdateLayout();
                var oneHour = Assert.Single(
                    FindVisualDescendants<WpfRadioButton>(section),
                    button => button.DataContext is UsageTrendBlockOption { DurationMinutes: 60 });

                Assert.NotNull(oneHour.Command);
                oneHour.Command.Execute(oneHour.CommandParameter);
                section.UpdateLayout();

                Assert.Equal(60, viewModel.SelectedBlockDurationMinutes);
                Assert.Equal(60, Assert.Single(viewModel.BlockOptions, option => option.IsSelected).DurationMinutes);
                Assert.Contains("Capacity may run short", viewModel.BlockAdvisorDetail);

                var constraint = Assert.Single(
                    FindVisualDescendants<System.Windows.Controls.TextBlock>(section),
                    text => text.Text == viewModel.NextConstraintHeadline);
                var constraintPeer = UIElementAutomationPeer.CreatePeerForElement(constraint);
                Assert.NotNull(constraintPeer);
                Assert.Equal(viewModel.NextConstraintHeadline, constraintPeer.GetName());
                Assert.Equal(viewModel.NextConstraintAccessibleSummary, constraintPeer.GetHelpText());
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The next-block selector interaction test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
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
            // The model's exact exhaustion time is authoritative. The presenter inserts
            // that crossing into the coarse projection so the guide and blue line agree.
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
        Assert.Equal(likelyLimit.AddMinutes(-7), chart.ForecastLimitAt);
        Assert.Contains(
            chart.ProjectedPoints,
            point => point.Timestamp == likelyLimit.AddMinutes(-7) && point.UsedPercent == 100);
        Assert.Equal(likelyLimit.AddHours(2), chart.ForecastWindowEnd);
        Assert.Equal("Estimated to reach the limit", chart.Summary.ForecastLeadText);
        Assert.Equal(
            likelyLimit.AddMinutes(-7).ToLocalTime().ToString("ddd, MMM d, h:mm tt", CultureInfo.CurrentCulture),
            chart.Summary.ForecastWhenText);
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
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null,
            ProjectedRemainingAtResetPercent = 16,
            IsActionable = false,
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
    public void BoundWindowSelector_DoesNotClearChartDuringSignalRefresh()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
                var reset = now.AddDays(7);
                var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(now, reset, isMock: false, [20, 24, 28])],
                    RunwayForecasts = [Forecast(now, reset, LimitRunwayForecastConfidence.Medium, isMock: false)]
                }, "codex", now);

                var section = new PulseMeter.Slices.UsageTrend.UI.UsageTrendSection
                {
                    DataContext = viewModel
                };
                window = new System.Windows.Window
                {
                    Content = section,
                    Width = 900,
                    Height = 600,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                section.UpdateLayout();

                var chartModels = new List<UsageTrendChartModel?>();
                viewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(UsageTrendSectionViewModel.ChartModel))
                    {
                        chartModels.Add(viewModel.ChartModel);
                    }
                };

                var refreshedAt = now.AddMinutes(5);
                viewModel.ApplySignals(new UsageSignalsSnapshot
                {
                    UsageTrends = [Trend(refreshedAt, reset, isMock: false, [20, 24, 28, 31])],
                    RunwayForecasts = [Forecast(refreshedAt, reset, LimitRunwayForecastConfidence.Medium, isMock: false)]
                }, "codex", refreshedAt);

                Assert.NotEmpty(chartModels);
                Assert.DoesNotContain(null, chartModels);
                Assert.NotNull(viewModel.ChartModel);
                Assert.Equal(31, viewModel.ChartModel.ActualPoints[^1].UsedPercent);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The bound selector refresh test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void ViewModel_RemembersNextBlockSelectionPerWindow()
    {
        var now = new DateTimeOffset(2026, 7, 17, 10, 10, 0, TimeSpan.Zero);
        var shortReset = now.AddHours(3);
        var weeklyReset = now.AddDays(3);
        var shortTrend = Trend(now, shortReset, isMock: true, [20, 30, 40]);
        var weeklyTrend = Trend(now, weeklyReset, isMock: true, [42, 44, 46]) with
        {
            BucketId = "codex|10080",
            WindowLabel = "Weekly",
            WindowDurationMins = 10_080
        };
        var shortForecast = Forecast(now, shortReset, LimitRunwayForecastConfidence.High, isMock: true) with
        {
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null
        };
        var weeklyForecast = Forecast(now, weeklyReset, LimitRunwayForecastConfidence.High, isMock: true) with
        {
            BucketId = "codex|10080",
            WindowLabel = "Weekly",
            WindowDurationMins = 10_080,
            State = LimitRunwayForecastState.OnTrack,
            ExhaustsAtUtc = null
        };
        var viewModel = new UsageTrendSectionViewModel(new UsageTrendPresenter());

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [shortTrend, weeklyTrend],
            RunwayForecasts = [shortForecast, weeklyForecast]
        }, "codex", now);

        viewModel.SelectedBlockDurationMinutes = 120;
        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 10_080);
        viewModel.SelectedBlockDurationMinutes = 480;

        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 300);
        Assert.Equal(120, viewModel.SelectedBlockDurationMinutes);
        Assert.Equal(120, Assert.Single(viewModel.BlockOptions, option => option.IsSelected).DurationMinutes);

        viewModel.SelectedWindow = viewModel.WindowOptions.Single(option => option.WindowDurationMins == 10_080);
        Assert.Equal(480, viewModel.SelectedBlockDurationMinutes);
        Assert.Equal(480, Assert.Single(viewModel.BlockOptions, option => option.IsSelected).DurationMinutes);
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

    private static UsageTrendChartModel CreateKeyboardNavigableChartModel(DateTimeOffset start)
    {
        var points = new[]
        {
            new UsageTrendPoint(start, 35),
            new UsageTrendPoint(start.AddHours(1), 48),
            new UsageTrendPoint(start.AddHours(2), 68)
        };
        var momentum = new UsageMomentumSummary("", "", "", 0);
        var summary = new UsageTrendRunwaySummary(
            "",
            "",
            "",
            "",
            "",
            momentum,
            "",
            "",
            "",
            "",
            "",
            false);

        return new UsageTrendChartModel(
            points,
            [],
            [],
            [],
            start,
            start.AddHours(3),
            start.AddHours(2),
            start.AddHours(4),
            null,
            null,
            null,
            UsageTrendChartMode.UsageTrend,
            ShowProjection: true,
            ShowRange: false,
            Summary: summary,
            AccessibleSummary: "Keyboard usage trend summary")
        {
            ReferenceProjectedPoints =
            [
                new UsageTrendPoint(start, 30),
                new UsageTrendPoint(start.AddHours(1), 40),
                new UsageTrendPoint(start.AddHours(2), 60)
            ]
        };
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

    private static LimitRunwayForecast WeeklyForecast(
        DateTimeOffset now,
        DateTimeOffset reset,
        LimitRunwayForecastConfidence confidence)
    {
        return Forecast(now, reset, confidence, isMock: false) with
        {
            BucketId = "codex|10080",
            WindowLabel = "7d",
            WindowDurationMins = 10_080,
            ExhaustsAtUtc = now.AddHours(6),
            EarliestExhaustsAtUtc = now.AddHours(5),
            LatestExhaustsAtUtc = now.AddHours(7)
        };
    }

    private static UsageTrendNextConstraint BuildNextConstraint(
        DateTimeOffset now,
        params LimitRunwayForecast[] forecasts)
    {
        var selected = forecasts[0];
        var chart = new UsageTrendPresenter().BuildChart(
            Trend(now, selected.ResetsAtUtc, isMock: false, [40, 45, 50]) with
            {
                WindowDurationMins = selected.WindowDurationMins,
                WindowLabel = selected.WindowDurationMins == 10_080 ? "7d" : "5h"
            },
            selected,
            now,
            showProjection: true,
            showRange: true,
            liveForecasts: forecasts);

        return Assert.IsType<UsageTrendChartModel>(chart).NextConstraint!;
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

    private static System.Windows.Media.Imaging.RenderTargetBitmap RenderGauge(UsageMomentumGauge gauge)
    {
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            160,
            58,
            96,
            96,
            System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(gauge);
        return bitmap;
    }

    private static byte ReadAlpha(
        System.Windows.Media.Imaging.RenderTargetBitmap bitmap,
        int x,
        int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3];
    }

    private static IEnumerable<T> FindVisualDescendants<T>(System.Windows.DependencyObject root)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class FixedUserIdleTimeProvider : IUserIdleTimeProvider
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
    }
}
