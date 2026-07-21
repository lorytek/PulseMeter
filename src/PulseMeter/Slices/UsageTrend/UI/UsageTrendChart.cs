using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using ToolTip = System.Windows.Controls.ToolTip;

namespace PulseMeter.Slices.UsageTrend.UI;

/// <summary>Draws a compact, accessible usage timeline without requiring a charting package.</summary>
public sealed class UsageTrendChart : FrameworkElement
{
    private const double LeftGutter = 43;
    private const double RightGutter = 22;
    private const double TopGutter = 48;
    private const double BottomGutter = 74;
    private const double ContextStripHeight = 15;
    private const double ContextStripTopOffset = 33;
    private const double FocusPrefixRatio = 0.18;
    private const double AxisFontSize = 10;
    private const double CompactAxisFontSize = 9;
    private const double DirectLabelFontSize = 10.5;
    private const double FullTimeLabelMinimumSpacing = 42;
    private const double CompactTimeLabelMinimumSpacing = 18;
    private const double ActualHoverRadius = 18;
    private static readonly TimeSpan HourlyTickMaximumWindow = TimeSpan.FromHours(12);

    private static readonly Typeface ChartTypeface = new("Segoe UI");
    private static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240)));
    private static readonly Brush MinorTimeGridBrush = Freeze(new SolidColorBrush(Color.FromArgb(95, 226, 232, 240)));
    private static readonly Brush AxisBrush = Freeze(new SolidColorBrush(Color.FromRgb(71, 85, 105)));
    private static readonly Brush MissingHistoryBrush = Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));
    private static readonly Brush ReferenceBrush = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
    private static readonly Brush BlueBrush = Freeze(new SolidColorBrush(Color.FromRgb(37, 99, 235)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(22, 163, 74)));
    private static readonly Brush AdverseBrush = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));
    private static readonly Brush AmberBrush = Freeze(new SolidColorBrush(Color.FromRgb(234, 88, 12)));
    private static readonly Brush AmberBandBrush = Freeze(new SolidColorBrush(Color.FromArgb(28, 251, 146, 60)));
    private static readonly Brush ForecastWindowBrush = Freeze(new SolidColorBrush(Color.FromArgb(125, 234, 88, 12)));
    private static readonly Brush HoverFillBrush = Freeze(new SolidColorBrush(Color.FromArgb(45, 37, 99, 235)));
    private static readonly Pen GridPen = Freeze(new Pen(GridBrush, 1));
    private static readonly Pen TimeGridPen = CreateDashedPen(MinorTimeGridBrush, 1, DashStyles.Dot);
    private static readonly Pen MidnightGridPen = Freeze(new Pen(GridBrush, 1));
    private static readonly Pen MissingHistoryPen = CreateDashedPen(MissingHistoryBrush, 1.75, DashStyles.Dash);
    private static readonly Pen ReferencePen = CreateDashedPen(ReferenceBrush, 1.35, DashStyles.Dash);
    private static readonly Pen ActualPen = Freeze(new Pen(BlueBrush, 2.45));
    private static readonly Pen AdverseVariancePen = Freeze(new Pen(AdverseBrush, 2.75));
    private static readonly Pen ProjectionPen = CreateDashedPen(BlueBrush, 1.9, DashStyles.Dot);
    private static readonly Pen SustainablePen = CreateDashedPen(GreenBrush, 1.55, DashStyles.Dash);
    private static readonly Pen BudgetPen = CreateDashedPen(AxisBrush, 1.25, DashStyles.Dash);
    private static readonly Pen ResetPen = CreateDashedPen(GreenBrush, 1.15, DashStyles.Dash);
    private static readonly Pen ForecastWindowPen = CreateDashedPen(ForecastWindowBrush, 1, DashStyles.Dot);
    private static readonly Pen ForecastLimitPen = CreateDashedPen(AmberBrush, 1.15, DashStyles.Dash);
    private static readonly Pen HoverOutlinePen = Freeze(new Pen(Brushes.White, 1.5));
    private static readonly Pen NowOutlinePen = Freeze(new Pen(BlueBrush, 2));

    private readonly ToolTip _tooltip;
    private UsageTrendPoint? _hoveredPoint;

    public UsageTrendChart()
    {
        Focusable = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Cursor = Cursors.Cross;

        _tooltip = new ToolTip
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
        };
        ToolTip = _tooltip;
    }

    /// <summary>Gets or sets the immutable data rendered by the chart.</summary>
    public UsageTrendChartModel? Model
    {
        get => (UsageTrendChartModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model),
        typeof(UsageTrendChartModel),
        typeof(UsageTrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnModelChanged));

    protected override AutomationPeer OnCreateAutomationPeer() => new UsageTrendChartAutomationPeer(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        const double desiredWidth = 360;
        const double desiredHeight = 300;

        return new Size(
            double.IsInfinity(availableSize.Width) ? desiredWidth : Math.Min(desiredWidth, availableSize.Width),
            double.IsInfinity(availableSize.Height) ? desiredHeight : Math.Min(desiredHeight, availableSize.Height));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!TryCreateViewport(out var viewport))
        {
            DrawEmptyState(drawingContext, "Usage trend is not large enough to display.");
            return;
        }

        DrawGrid(drawingContext, viewport);

        var model = Model;
        if (model is null)
        {
            DrawTimeTicks(drawingContext, viewport);
            DrawEmptyState(drawingContext, "Usage data will appear here.");
            return;
        }

        var actual = GetUsablePoints(model.ActualPoints);
        var measurementGaps = GetUsableGaps(model.MeasurementGaps);
        var projection = model.ShowProjection ? GetUsablePoints(model.ProjectedPoints) : [];
        var referenceProjection = model.ShowProjection ? GetUsablePoints(model.ReferenceProjectedPoints) : [];
        var sustainable = GetUsablePoints(model.SustainablePoints);
        var range = model.ShowRange ? GetUsableBandPoints(model.TypicalRange) : [];
        var dataBounds = GetTimeBounds(model, actual, projection, sustainable, range);
        var focus = SelectFocusWindow(dataBounds.Start, dataBounds.End, actual);
        viewport = viewport with
        {
            Start = dataBounds.Start,
            End = dataBounds.End,
            FocusStart = focus.FocusStart,
            CompressUnmeasuredHistory = focus.CompressUnmeasuredHistory,
            ShowContextStrip = focus.ShowContextStrip
        };

        DrawTimeTicks(drawingContext, viewport);
        DrawForecastWindow(drawingContext, viewport, model);
        DrawBudgetLimit(drawingContext, viewport);
        DrawUnmeasuredHistory(drawingContext, viewport, actual.FirstOrDefault());
        DrawAxisBreak(drawingContext, viewport);
        DrawSeries(drawingContext, viewport, sustainable, SustainablePen, "Sustainable pace", GreenBrush, 0.82, 18);
        if (referenceProjection.Count > 1 && model.ReferenceForecastCapturedAt is DateTimeOffset referenceCapturedAt)
        {
            var referenceLabel = $"Earlier forecast · {FormatPointTime(referenceCapturedAt)}";
            DrawSeries(
                drawingContext,
                viewport,
                referenceProjection,
                ReferencePen,
                referenceLabel,
                ReferenceBrush,
                0.68,
                18,
                HasSeriesLabelRoom(viewport, referenceProjection, referenceLabel));
        }
        DrawSeries(drawingContext, viewport, projection, ProjectionPen, string.Empty, BlueBrush, 0.48, -22, showLabel: false);
        DrawActualSeries(drawingContext, viewport, actual, measurementGaps);
        DrawMeasurementGaps(drawingContext, viewport, actual, measurementGaps);
        DrawUnfavorableVariance(drawingContext, viewport, model.UnfavorableVarianceSegments);
        DrawLatestMarker(drawingContext, viewport, actual.LastOrDefault(), model.EvaluatedAt);
        DrawResetMarker(drawingContext, viewport, model);
        DrawContextStrip(drawingContext, viewport);

        if (_hoveredPoint is not null)
        {
            DrawHoveredPoint(drawingContext, viewport, _hoveredPoint);
        }

        if (actual.Count == 0 && projection.Count == 0 && sustainable.Count == 0 && range.Count == 0)
        {
            DrawEmptyState(drawingContext, "No usage data in this time window.");
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var model = Model;
        if (model is null || !TryCreateViewport(out var viewport))
        {
            ClearHover();
            return;
        }

        var actual = GetUsablePoints(model.ActualPoints);
        var range = model.ShowRange ? GetUsableBandPoints(model.TypicalRange) : [];
        var bounds = GetTimeBounds(
            model,
            actual,
            model.ShowProjection ? GetUsablePoints(model.ProjectedPoints) : [],
            GetUsablePoints(model.SustainablePoints),
            range);
        var focus = SelectFocusWindow(bounds.Start, bounds.End, actual);
        viewport = viewport with
        {
            Start = bounds.Start,
            End = bounds.End,
            FocusStart = focus.FocusStart,
            CompressUnmeasuredHistory = focus.CompressUnmeasuredHistory,
            ShowContextStrip = focus.ShowContextStrip
        };
        var mouse = e.GetPosition(this);

        if (mouse.X < viewport.Left || mouse.X > viewport.Right || mouse.Y < viewport.Top || mouse.Y > viewport.Bottom)
        {
            ClearHover();
            return;
        }

        var nearest = FindActualHoverPoint(viewport, actual, mouse);
        if (nearest is not null)
        {
            if (Equals(nearest, _hoveredPoint))
            {
                return;
            }

            _hoveredPoint = nearest;
            var referenceProjection = model.ShowProjection ? GetUsablePoints(model.ReferenceProjectedPoints) : [];
            _tooltip.Content = BuildTooltip(
                nearest,
                FindPointAt(referenceProjection, nearest.Timestamp));
            _tooltip.IsOpen = true;
            InvalidateVisual();
            return;
        }

        if (TryShowForecastWindowTooltip(model, viewport, mouse.X))
        {
            return;
        }

        ClearHover();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
    }

    private static void OnModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var chart = (UsageTrendChart)dependencyObject;
        chart.ClearHover();
        var summary = ((UsageTrendChartModel?)e.NewValue)?.AccessibleSummary;
        AutomationProperties.SetName(chart, string.IsNullOrWhiteSpace(summary) ? "Usage trend chart" : summary);
        AutomationProperties.SetHelpText(chart, "Usage percentage over time, including not-measured gaps and comparison with the earlier forecast when available.");
        chart.InvalidateVisual();
    }

    private bool TryCreateViewport(out Viewport viewport)
    {
        var left = LeftGutter;
        var top = TopGutter;
        var right = Math.Max(left, ActualWidth - RightGutter);
        var bottom = Math.Max(top, ActualHeight - BottomGutter);
        viewport = new Viewport(
            left,
            top,
            right,
            bottom,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow,
            CompressUnmeasuredHistory: false,
            ShowContextStrip: false);
        return right - left >= 50 && bottom - top >= 50;
    }

    private void DrawGrid(DrawingContext context, Viewport viewport)
    {
        foreach (var percent in new[] { 0d, 25d, 50d, 75d, 100d })
        {
            var y = AlignToPixel(ToY(viewport, percent), false);
            context.DrawLine(GridPen, new Point(viewport.Left, y), new Point(viewport.Right, y));
            DrawText(context, $"{percent:0}%", new Point(4, y - 6), AxisFontSize, AxisBrush);
        }
    }

    private void DrawTimeTicks(DrawingContext context, Viewport viewport)
    {
        var tickStart = viewport.CompressUnmeasuredHistory ? viewport.FocusStart : viewport.Start;
        var ticks = BuildTimeTicks(tickStart, viewport.End);
        var labelDensity = ResolveTimeLabelDensity(
            tickStart,
            viewport.End,
            viewport.Left,
            viewport.Right);

        foreach (var tick in ticks)
        {
            var x = ToX(viewport, tick.Timestamp);
            if (x < viewport.Left - 0.5 || x > viewport.Right + 0.5)
            {
                continue;
            }

            context.DrawLine(
                string.IsNullOrEmpty(tick.DateLabel) ? TimeGridPen : MidnightGridPen,
                new Point(x, viewport.Top),
                new Point(x, viewport.Bottom));

            var timeLabel = FormatTimeTickLabel(tick, labelDensity);
            if (!string.IsNullOrEmpty(timeLabel))
            {
                var fontSize = labelDensity == UsageTrendTimeLabelDensity.Full
                    ? AxisFontSize
                    : CompactAxisFontSize;
                DrawCenteredText(context, timeLabel, x, viewport.Bottom + 10, fontSize, AxisBrush, viewport);
            }

            if (!viewport.CompressUnmeasuredHistory && !string.IsNullOrEmpty(tick.DateLabel))
            {
                DrawCenteredText(context, tick.DateLabel, x, viewport.Bottom + 25, AxisFontSize, AxisBrush, viewport);
            }
        }
    }

    private void DrawForecastWindow(DrawingContext context, Viewport viewport, UsageTrendChartModel model)
    {
        if (ShouldDrawForecastWindow(model)
            && model.ForecastWindowStart is DateTimeOffset rawStart
            && model.ForecastWindowEnd is DateTimeOffset rawEnd
            && rawEnd > viewport.Start
            && rawStart < viewport.End)
        {
            var start = rawStart < viewport.Start ? viewport.Start : rawStart;
            var end = rawEnd > viewport.End ? viewport.End : rawEnd;
            var left = ToX(viewport, start);
            var right = Math.Max(left + 4, ToX(viewport, end));
            context.DrawRectangle(
                AmberBandBrush,
                null,
                new Rect(left, viewport.Top, right - left, viewport.Bottom - viewport.Top));
            context.DrawLine(ForecastWindowPen, new Point(left, viewport.Top), new Point(left, viewport.Bottom));
            context.DrawLine(ForecastWindowPen, new Point(right, viewport.Top), new Point(right, viewport.Bottom));

            var windowLabel = $"Possible {FormatPointTime(rawStart)} – {FormatPointTime(rawEnd)}";
            var labelCenter = (left + right) / 2;
            if (right - left >= MeasureText(windowLabel, CompactAxisFontSize).Width + 14)
            {
                DrawCenteredText(
                    context,
                    windowLabel,
                    labelCenter,
                    viewport.Bottom - 18,
                    CompactAxisFontSize,
                    AmberBrush,
                    viewport);
            }
        }

        if (!ShouldDrawForecastLimit(model))
        {
            return;
        }

        if (model.ForecastLimitAt is not DateTimeOffset limitAt
            || limitAt < viewport.Start
            || limitAt > viewport.End)
        {
            return;
        }

        var limitX = ToX(viewport, limitAt);
        context.DrawLine(ForecastLimitPen, new Point(limitX, viewport.Top), new Point(limitX, viewport.Bottom));
        context.DrawEllipse(Brushes.White, new Pen(AmberBrush, 2), new Point(limitX, ToY(viewport, 100)), 5, 5);
        var label = $"Estimated reach limit · {FormatPointTime(limitAt)}";
        DrawCenteredText(
            context,
            label,
            limitX,
            GetTopLabelY(label, DirectLabelFontSize, viewport),
            DirectLabelFontSize,
            AmberBrush,
            viewport);
    }

    internal static bool ShouldDrawForecastWindow(UsageTrendChartModel model) => model.ShowRange;

    internal static bool ShouldDrawForecastLimit(UsageTrendChartModel model) => model.ShowProjection;

    private void DrawBudgetLimit(DrawingContext context, Viewport viewport)
    {
        var y = AlignToPixel(ToY(viewport, 100), false);
        context.DrawLine(BudgetPen, new Point(viewport.Left, y), new Point(viewport.Right, y));
        const string label = "Limit 100%";
        DrawText(
            context,
            label,
            new Point(viewport.Left + 4, GetTopLabelY(label, CompactAxisFontSize, viewport)),
            CompactAxisFontSize,
            AxisBrush);
    }

    private void DrawUnmeasuredHistory(DrawingContext context, Viewport viewport, UsageTrendPoint? firstRecorded)
    {
        if (firstRecorded is null || firstRecorded.Timestamp <= viewport.Start)
        {
            return;
        }

        var start = new Point(ToX(viewport, viewport.Start), ToY(viewport, 0));
        var end = new Point(ToX(viewport, firstRecorded.Timestamp), ToY(viewport, firstRecorded.UsedPercent));
        context.DrawLine(MissingHistoryPen, start, end);
        context.DrawEllipse(Brushes.White, new Pen(MissingHistoryBrush, 1.75), start, 4.5, 4.5);
        context.DrawEllipse(Brushes.White, new Pen(MissingHistoryBrush, 1.75), end, 4.5, 4.5);

        var firstLabel = $"First · {FormatPointTime(firstRecorded.Timestamp)} · {ClampPercent(firstRecorded.UsedPercent):0}%";
        var firstLabelSize = MeasureText(firstLabel, DirectLabelFontSize);
        var preferredLeft = end.X - firstLabelSize.Width - 8;
        var firstLabelX = preferredLeft >= viewport.Left
            ? preferredLeft
            : Math.Clamp(
                end.X + 8,
                viewport.Left,
                Math.Max(viewport.Left, viewport.Right - firstLabelSize.Width));
        var firstLabelY = Math.Min(
            viewport.Bottom - firstLabelSize.Height - 3,
            end.Y + 8);
        var firstLabelPosition = new Point(firstLabelX, Math.Max(viewport.Top + 24, firstLabelY));
        var firstLabelBounds = new Rect(firstLabelPosition, firstLabelSize);

        var historyX = start.X + ((end.X - start.X) * 0.46);
        var historyLabel = "Not measured";
        var historyLabelSize = MeasureText(historyLabel, CompactAxisFontSize);
        if (end.X - start.X >= historyLabelSize.Width + 14)
        {
            var historyY = Math.Max(viewport.Top + 4, start.Y + ((end.Y - start.Y) * 0.46) - 18);
            var historyLabelX = Math.Clamp(
                historyX - (historyLabelSize.Width / 2),
                viewport.Left,
                Math.Max(viewport.Left, viewport.Right - historyLabelSize.Width));
            var historyLabelBounds = new Rect(new Point(historyLabelX, historyY), historyLabelSize);
            if (HasLabelClearance(historyLabelBounds, firstLabelBounds))
            {
                DrawText(
                    context,
                    historyLabel,
                    new Point(historyLabelX, historyY),
                    CompactAxisFontSize,
                    MissingHistoryBrush);
            }
        }

        DrawText(
            context,
            firstLabel,
            firstLabelPosition,
            DirectLabelFontSize,
            MissingHistoryBrush);
    }

    internal static bool HasLabelClearance(Rect candidateBounds, Rect occupiedBounds)
    {
        occupiedBounds.Inflate(4, 4);
        return !candidateBounds.IntersectsWith(occupiedBounds);
    }

    private void DrawAxisBreak(DrawingContext context, Viewport viewport)
    {
        if (!viewport.CompressUnmeasuredHistory)
        {
            return;
        }

        var x = ToX(viewport, viewport.FocusStart);
        var y = viewport.Bottom;
        context.DrawLine(new Pen(Brushes.White, 3), new Point(x - 6, y), new Point(x + 6, y));
        context.DrawLine(MissingHistoryPen, new Point(x - 5, y + 3), new Point(x - 1, y - 3));
        context.DrawLine(MissingHistoryPen, new Point(x + 1, y + 3), new Point(x + 5, y - 3));
    }

    private void DrawContextStrip(DrawingContext context, Viewport viewport)
    {
        if (!viewport.ShowContextStrip)
        {
            return;
        }

        var top = viewport.Bottom + ContextStripTopOffset;
        var bounds = new Rect(viewport.Left, top, viewport.Right - viewport.Left, ContextStripHeight);
        context.DrawRoundedRectangle(Brushes.White, GridPen, bounds, 3, 3);

        var focusLeft = MapTimestampToX(viewport.FocusStart, viewport.Start, viewport.End, viewport.Left, viewport.Right);
        context.DrawRectangle(HoverFillBrush, null, new Rect(focusLeft, top + 1, viewport.Right - focusLeft - 1, ContextStripHeight - 2));

        foreach (var tick in BuildDailyContextTicks(viewport.Start, viewport.End))
        {
            var x = MapTimestampToX(tick.Timestamp, viewport.Start, viewport.End, viewport.Left, viewport.Right);
            context.DrawLine(MidnightGridPen, new Point(x, top), new Point(x, top + ContextStripHeight));

            var label = tick.Timestamp.ToLocalTime().ToString("ddd", CultureInfo.CurrentCulture);
            DrawCenteredText(context, label, x, top + ContextStripHeight + 2, CompactAxisFontSize, AxisBrush, viewport);
        }

        var contextLabel = viewport.End - viewport.Start >= TimeSpan.FromDays(6.5)
            ? "Full 7d"
            : "Full window";
        DrawText(context, contextLabel, new Point(viewport.Left, top - 14), CompactAxisFontSize, AxisBrush);
    }

    private void DrawSeries(
        DrawingContext context,
        Viewport viewport,
        IReadOnlyList<UsageTrendPoint> points,
        Pen pen,
        string label,
        Brush labelBrush,
        double labelProgress,
        double labelOffset,
        bool showLabel = true)
    {
        if (points.Count == 0)
        {
            return;
        }

        if (points.Count == 1)
        {
            var point = points[0];
            context.DrawEllipse(BlueBrush, null, new Point(ToX(viewport, point.Timestamp), ToY(viewport, point.UsedPercent)), 2.5, 2.5);
            if (showLabel)
            {
                DrawSeriesLabel(context, viewport, point, label, labelBrush, labelOffset);
            }
            return;
        }

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var first = points[0];
            stream.BeginFigure(new Point(ToX(viewport, first.Timestamp), ToY(viewport, first.UsedPercent)), false, false);
            foreach (var point in points.Skip(1))
            {
                stream.LineTo(new Point(ToX(viewport, point.Timestamp), ToY(viewport, point.UsedPercent)), true, false);
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
        if (showLabel)
        {
            DrawSeriesLabel(
                context,
                viewport,
                SelectSeriesLabelPoint(points, labelProgress),
                label,
                labelBrush,
                labelOffset);
        }
    }

    private void DrawActualSeries(
        DrawingContext context,
        Viewport viewport,
        IReadOnlyList<UsageTrendPoint> points,
        IReadOnlyList<UsageTrendGap> gaps)
    {
        var segments = SplitSeriesAtGaps(points, gaps);
        foreach (var segment in segments)
        {
            DrawSeries(context, viewport, segment, ActualPen, "Actual", BlueBrush, 0.58, 11, showLabel: false);
        }

        var labelSegment = segments
            .Where(segment => segment.Count >= 2)
            .OrderByDescending(segment => ToX(viewport, segment[^1].Timestamp) - ToX(viewport, segment[0].Timestamp))
            .FirstOrDefault();
        if (labelSegment is not null && HasSeriesLabelRoom(viewport, labelSegment, "Actual"))
        {
            DrawSeriesLabel(
                context,
                viewport,
                SelectSeriesLabelPoint(labelSegment, 0.58),
                "Actual",
                BlueBrush,
                11);
        }
    }

    private void DrawMeasurementGaps(
        DrawingContext context,
        Viewport viewport,
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> gaps)
    {
        foreach (var gap in gaps)
        {
            var before = FindPointAt(actual, gap.StartedAt);
            var after = FindPointAt(actual, gap.EndedAt);
            if (before is null || after is null)
            {
                continue;
            }

            var start = new Point(ToX(viewport, before.Timestamp), ToY(viewport, before.UsedPercent));
            var end = new Point(ToX(viewport, after.Timestamp), ToY(viewport, after.UsedPercent));
            context.DrawLine(MissingHistoryPen, start, end);
            context.DrawEllipse(Brushes.White, new Pen(MissingHistoryBrush, 1.75), start, 3.5, 3.5);
            context.DrawEllipse(Brushes.White, new Pen(MissingHistoryBrush, 1.75), end, 3.5, 3.5);

            var label = $"Not measured · {FormatGapDuration(gap.EndedAt - gap.StartedAt)}";
            if (end.X - start.X >= MeasureText(label, CompactAxisFontSize).Width + 14)
            {
                DrawCenteredText(
                    context,
                    label,
                    (start.X + end.X) / 2,
                    Math.Clamp(Math.Min(start.Y, end.Y) - 18, viewport.Top + 4, viewport.Bottom - 16),
                    CompactAxisFontSize,
                    MissingHistoryBrush,
                    viewport);
            }
        }
    }

    internal static IReadOnlyList<IReadOnlyList<UsageTrendPoint>> SplitSeriesAtGaps(
        IReadOnlyList<UsageTrendPoint> points,
        IReadOnlyList<UsageTrendGap> gaps)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var segments = new List<IReadOnlyList<UsageTrendPoint>>();
        var current = new List<UsageTrendPoint> { points[0] };
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var point = points[index];
            if (gaps.Any(gap => previous.Timestamp <= gap.StartedAt && point.Timestamp >= gap.EndedAt))
            {
                segments.Add(current.ToArray());
                current = [point];
                continue;
            }

            current.Add(point);
        }

        segments.Add(current.ToArray());
        return segments;
    }

    internal static string FormatGapDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{duration.TotalDays:0.#}d";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{duration.TotalHours:0.#}h";
        }

        return $"{Math.Max(1, Math.Round(duration.TotalMinutes)):0}m";
    }

    private bool HasSeriesLabelRoom(
        Viewport viewport,
        IReadOnlyList<UsageTrendPoint> points,
        string label)
    {
        if (points.Count < 2)
        {
            return false;
        }

        var span = Math.Abs(ToX(viewport, points[^1].Timestamp) - ToX(viewport, points[0].Timestamp));
        return ShouldShowInlineSeriesLabel(span, MeasureText(label, DirectLabelFontSize).Width);
    }

    internal static bool ShouldShowInlineSeriesLabel(double seriesPixelWidth, double labelPixelWidth) =>
        seriesPixelWidth >= labelPixelWidth + 36;

    private static void DrawUnfavorableVariance(
        DrawingContext context,
        Viewport viewport,
        IReadOnlyList<UsageTrendVarianceSegment> segments)
    {
        foreach (var segment in segments)
        {
            context.DrawLine(
                AdverseVariancePen,
                new Point(ToX(viewport, segment.Start.Timestamp), ToY(viewport, segment.Start.UsedPercent)),
                new Point(ToX(viewport, segment.End.Timestamp), ToY(viewport, segment.End.UsedPercent)));
        }
    }

    private static UsageTrendPoint SelectSeriesLabelPoint(
        IReadOnlyList<UsageTrendPoint> points,
        double progress)
    {
        var index = (int)Math.Round((points.Count - 1) * Math.Clamp(progress, 0, 1));
        return points[Math.Clamp(index, 0, points.Count - 1)];
    }

    private void DrawSeriesLabel(
        DrawingContext context,
        Viewport viewport,
        UsageTrendPoint point,
        string label,
        Brush brush,
        double offsetY)
    {
        var position = new Point(ToX(viewport, point.Timestamp), ToY(viewport, point.UsedPercent));
        var size = MeasureText(label, DirectLabelFontSize);
        var x = Math.Clamp(position.X + 8, viewport.Left + 4, Math.Max(viewport.Left + 4, viewport.Right - size.Width));
        var y = Math.Clamp(position.Y + offsetY, viewport.Top + 4, viewport.Bottom - size.Height - 3);
        DrawText(context, label, new Point(x, y), DirectLabelFontSize, brush);
    }

    private void DrawResetMarker(DrawingContext context, Viewport viewport, UsageTrendChartModel model)
    {
        var resetAt = model.ResetAt;
        if (resetAt < viewport.Start || resetAt > viewport.End)
        {
            return;
        }

        var x = AlignToPixel(ToX(viewport, resetAt), true);
        context.DrawLine(ResetPen, new Point(x, viewport.Top), new Point(x, viewport.Bottom));
        context.DrawEllipse(Brushes.White, new Pen(GreenBrush, 2), new Point(x, ToY(viewport, 100)), 5, 5);

        var label = $"Reset · {FormatPointTime(resetAt)}";
        var labelSize = MeasureText(label, DirectLabelFontSize);
        var labelWidth = labelSize.Width;
        var labelX = Math.Clamp(x - labelWidth - 7, viewport.Left, Math.Max(viewport.Left, viewport.Right - labelWidth));
        var labelY = GetTopLabelY(label, DirectLabelFontSize, viewport);

        if (ShouldDrawForecastLimit(model)
            && model.ForecastLimitAt is DateTimeOffset limitAt
            && limitAt >= viewport.Start
            && limitAt <= viewport.End)
        {
            var limitLabel = $"Estimated reach limit · {FormatPointTime(limitAt)}";
            var limitLabelWidth = MeasureText(limitLabel, DirectLabelFontSize).Width;
            var limitCenterX = ToX(viewport, limitAt);
            var limitLabelX = Math.Clamp(
                limitCenterX - (limitLabelWidth / 2),
                viewport.Left,
                Math.Max(viewport.Left, viewport.Right - limitLabelWidth));

            if (DoLabelRangesOverlap(labelX, labelWidth, limitLabelX, limitLabelWidth))
            {
                labelY = Math.Max(2, labelY - labelSize.Height - 3);
            }
        }

        DrawText(
            context,
            label,
            new Point(labelX, labelY),
            DirectLabelFontSize,
            GreenBrush);
    }

    internal static bool DoLabelRangesOverlap(
        double firstX,
        double firstWidth,
        double secondX,
        double secondWidth,
        double minimumGap = 8) =>
        firstX < secondX + secondWidth + minimumGap
        && secondX < firstX + firstWidth + minimumGap;

    private void DrawLatestMarker(
        DrawingContext context,
        Viewport viewport,
        UsageTrendPoint? point,
        DateTimeOffset evaluatedAt)
    {
        if (point is null)
        {
            return;
        }

        var position = new Point(ToX(viewport, point.Timestamp), ToY(viewport, point.UsedPercent));
        context.DrawEllipse(Brushes.White, NowOutlinePen, position, 4.5, 4.5);

        var label = FormatLatestUsageLabel(point.Timestamp, evaluatedAt, point.UsedPercent);
        var labelSize = MeasureText(label, DirectLabelFontSize);
        var labelX = Math.Clamp(
            position.X + 8,
            viewport.Left + 4,
            Math.Max(viewport.Left + 4, viewport.Right - labelSize.Width));
        var labelY = point.UsedPercent >= 90
            ? Math.Min(viewport.Bottom - labelSize.Height - 3, position.Y + 8)
            : Math.Max(viewport.Top + 4, position.Y - labelSize.Height - 8);
        DrawText(context, label, new Point(labelX, labelY), DirectLabelFontSize, BlueBrush);
    }

    private void DrawHoveredPoint(DrawingContext context, Viewport viewport, UsageTrendPoint point)
    {
        var position = new Point(ToX(viewport, point.Timestamp), ToY(viewport, point.UsedPercent));
        context.DrawEllipse(HoverFillBrush, null, position, 7, 7);
        context.DrawEllipse(BlueBrush, HoverOutlinePen, position, 4.5, 4.5);
    }

    private void DrawEmptyState(DrawingContext context, string message)
    {
        var size = MeasureText(message, 12);
        var x = Math.Max(4, (ActualWidth - size.Width) / 2);
        var y = Math.Max(4, (ActualHeight - size.Height) / 2);
        DrawText(context, message, new Point(x, y), 12, AxisBrush);
    }

    private void ClearHover()
    {
        if (_hoveredPoint is null && !_tooltip.IsOpen)
        {
            return;
        }

        _hoveredPoint = null;
        _tooltip.IsOpen = false;
        InvalidateVisual();
    }

    private UsageTrendPoint? FindActualHoverPoint(
        Viewport viewport,
        IReadOnlyList<UsageTrendPoint> actual,
        Point mouse)
    {
        var nearest = actual.MinBy(point =>
        {
            var pointX = ToX(viewport, point.Timestamp);
            var pointY = ToY(viewport, point.UsedPercent);
            var deltaX = pointX - mouse.X;
            var deltaY = pointY - mouse.Y;
            return (deltaX * deltaX) + (deltaY * deltaY);
        });
        if (nearest is null)
        {
            return null;
        }

        return IsWithinActualHoverRadius(
            mouse.X,
            mouse.Y,
            ToX(viewport, nearest.Timestamp),
            ToY(viewport, nearest.UsedPercent))
            ? nearest
            : null;
    }

    private bool TryShowForecastWindowTooltip(UsageTrendChartModel model, Viewport viewport, double mouseX)
    {
        if (!ShouldDrawForecastWindow(model)
            || model.ForecastWindowStart is not DateTimeOffset rawStart
            || model.ForecastWindowEnd is not DateTimeOffset rawEnd
            || rawEnd <= viewport.Start
            || rawStart >= viewport.End)
        {
            return false;
        }

        var start = rawStart < viewport.Start ? viewport.Start : rawStart;
        var end = rawEnd > viewport.End ? viewport.End : rawEnd;
        var left = ToX(viewport, start);
        var right = Math.Max(left + 4, ToX(viewport, end));
        if (mouseX < left || mouseX > right)
        {
            return false;
        }

        var hadHoveredPoint = _hoveredPoint is not null;
        _hoveredPoint = null;
        _tooltip.Content = BuildForecastWindowTooltip(rawStart, rawEnd, model.ForecastLimitAt);
        _tooltip.IsOpen = true;
        if (hadHoveredPoint)
        {
            InvalidateVisual();
        }

        return true;
    }

    internal static bool IsWithinActualHoverRadius(
        double mouseX,
        double mouseY,
        double pointX,
        double pointY)
    {
        var deltaX = pointX - mouseX;
        var deltaY = pointY - mouseY;
        return (deltaX * deltaX) + (deltaY * deltaY) <= ActualHoverRadius * ActualHoverRadius;
    }

    private static UIElement BuildTooltip(
        UsageTrendPoint point,
        UsageTrendPoint? referencePoint)
    {
        var comparison = referencePoint is null
            ? string.Empty
            : BuildTooltipComparison(point.UsedPercent - referencePoint.UsedPercent, referencePoint.UsedPercent);

        return new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            LineHeight = 19,
            Padding = new Thickness(2),
            Text = $"{FormatTooltipTimestamp(point.Timestamp)}\nActual usage  {ClampPercent(point.UsedPercent):0}%{comparison}"
        };
    }

    private static UIElement BuildForecastWindowTooltip(
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset? mostLikely)
    {
        return new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            LineHeight = 19,
            Padding = new Thickness(2),
            Text = BuildForecastWindowTooltipText(start, end, mostLikely)
        };
    }

    internal static string BuildForecastWindowTooltipText(
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset? mostLikely)
    {
        var likelyLine = mostLikely is DateTimeOffset likely
            ? $"\nMost likely  {FormatTooltipTimestamp(likely)}"
            : string.Empty;
        return $"Estimated reach limit{likelyLine}\nEarliest  {FormatTooltipTimestamp(start)}\nLatest  {FormatTooltipTimestamp(end)}";
    }

    internal static string FormatTooltipTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("ddd, MMM d · h:mm tt", CultureInfo.CurrentCulture);

    private static string BuildTooltipComparison(double difference, double referencePercent)
    {
        var variance = Math.Abs(difference) <= 1
            ? "in line"
            : difference > 0
                ? $"{difference:0.#} pp above"
                : $"{Math.Abs(difference):0.#} pp below";
        return $"\nEarlier forecast  {ClampPercent(referencePercent):0}% · {variance}";
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetTimeBounds(
        UsageTrendChartModel model,
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendPoint> projection,
        IReadOnlyList<UsageTrendPoint> sustainable,
        IReadOnlyList<UsageTrendBandPoint> range)
    {
        if (model.RangeEnd > model.RangeStart)
        {
            return (model.RangeStart, model.RangeEnd);
        }

        var timestamps = actual.Select(point => point.Timestamp)
            .Concat(projection.Select(point => point.Timestamp))
            .Concat(sustainable.Select(point => point.Timestamp))
            .Concat(range.Select(point => point.Timestamp))
            .ToList();

        if (timestamps.Count == 0)
        {
            var end = model.ResetAt == default ? DateTimeOffset.UtcNow.AddHours(1) : model.ResetAt;
            return (end.AddHours(-1), end);
        }

        var start = timestamps.Min();
        var endTime = timestamps.Max();
        if (model.ResetAt != default)
        {
            start = Min(start, model.ResetAt);
            endTime = Max(endTime, model.ResetAt);
        }

        if (endTime <= start)
        {
            endTime = start.AddHours(1);
        }

        return (start, endTime);
    }

    private static IReadOnlyList<UsageTrendPoint> GetUsablePoints(IReadOnlyList<UsageTrendPoint>? points) =>
        (points ?? []).Where(point => double.IsFinite(point.UsedPercent)).OrderBy(point => point.Timestamp).ToArray();

    private static IReadOnlyList<UsageTrendBandPoint> GetUsableBandPoints(IReadOnlyList<UsageTrendBandPoint>? points) =>
        (points ?? []).Where(point => double.IsFinite(point.LowerPercent) && double.IsFinite(point.UpperPercent)).OrderBy(point => point.Timestamp).ToArray();

    private static IReadOnlyList<UsageTrendGap> GetUsableGaps(IReadOnlyList<UsageTrendGap>? gaps) =>
        (gaps ?? [])
            .Where(gap => gap.EndedAt > gap.StartedAt)
            .OrderBy(gap => gap.StartedAt)
            .ToArray();

    internal static UsageTrendBandPoint? FindBandPointAt(
        IReadOnlyList<UsageTrendBandPoint> points,
        DateTimeOffset timestamp)
    {
        if (points.Count == 0 || timestamp < points[0].Timestamp || timestamp > points[^1].Timestamp)
        {
            return null;
        }

        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            if (timestamp == current.Timestamp || index == points.Count - 1)
            {
                return current with { Timestamp = timestamp };
            }

            var next = points[index + 1];
            if (timestamp > next.Timestamp)
            {
                continue;
            }

            var intervalMilliseconds = (next.Timestamp - current.Timestamp).TotalMilliseconds;
            if (intervalMilliseconds <= 0)
            {
                return current with { Timestamp = timestamp };
            }

            var progress = (timestamp - current.Timestamp).TotalMilliseconds / intervalMilliseconds;
            return new UsageTrendBandPoint(
                timestamp,
                current.LowerPercent + ((next.LowerPercent - current.LowerPercent) * progress),
                current.UpperPercent + ((next.UpperPercent - current.UpperPercent) * progress));
        }

        return null;
    }

    internal static UsageTrendPoint? FindPointAt(
        IReadOnlyList<UsageTrendPoint> points,
        DateTimeOffset timestamp)
    {
        if (points.Count == 0 || timestamp < points[0].Timestamp || timestamp > points[^1].Timestamp)
        {
            return null;
        }

        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            if (timestamp == current.Timestamp || index == points.Count - 1)
            {
                return current with { Timestamp = timestamp };
            }

            var next = points[index + 1];
            if (timestamp > next.Timestamp)
            {
                continue;
            }

            var intervalMilliseconds = (next.Timestamp - current.Timestamp).TotalMilliseconds;
            if (intervalMilliseconds <= 0)
            {
                return current with { Timestamp = timestamp };
            }

            var progress = (timestamp - current.Timestamp).TotalMilliseconds / intervalMilliseconds;
            return new UsageTrendPoint(
                timestamp,
                current.UsedPercent + ((next.UsedPercent - current.UsedPercent) * progress));
        }

        return null;
    }

    internal static string FormatLatestPointLabel(DateTimeOffset timestamp, DateTimeOffset evaluatedAt)
    {
        var age = evaluatedAt - timestamp;
        var prefix = age >= TimeSpan.FromMinutes(-1) && age <= TimeSpan.FromMinutes(5)
            ? "Now"
            : "Latest";
        return $"{prefix} · {FormatPointTime(timestamp)}";
    }

    internal static string FormatLatestUsageLabel(
        DateTimeOffset timestamp,
        DateTimeOffset evaluatedAt,
        double usedPercent)
    {
        var age = evaluatedAt - timestamp;
        var prefix = age >= TimeSpan.FromMinutes(-1) && age <= TimeSpan.FromMinutes(5)
            ? "Now"
            : "Latest";
        return $"{prefix} · {ClampPercent(usedPercent):0}%";
    }

    private static double ToX(Viewport viewport, DateTimeOffset timestamp) => MapTimelineTimestampToX(
        timestamp,
        viewport.Start,
        viewport.End,
        viewport.FocusStart,
        viewport.CompressUnmeasuredHistory,
        viewport.Left,
        viewport.Right);

    internal static UsageTrendFocus SelectFocusWindow(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<UsageTrendPoint> actual)
    {
        var showContextStrip = end > start && end - start > HourlyTickMaximumWindow;
        if (!showContextStrip || actual.Count == 0)
        {
            return new UsageTrendFocus(start, CompressUnmeasuredHistory: false, showContextStrip);
        }

        var firstRecorded = actual[0].Timestamp;
        if (firstRecorded <= start || firstRecorded >= end)
        {
            return new UsageTrendFocus(start, CompressUnmeasuredHistory: false, showContextStrip);
        }

        return new UsageTrendFocus(firstRecorded, CompressUnmeasuredHistory: true, showContextStrip);
    }

    internal static double MapTimelineTimestampToX(
        DateTimeOffset timestamp,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset focusStart,
        bool compressUnmeasuredHistory,
        double left,
        double right)
    {
        if (!compressUnmeasuredHistory || focusStart <= start || focusStart >= end)
        {
            return MapTimestampToX(timestamp, start, end, left, right);
        }

        var focusX = left + (Math.Max(0, right - left) * FocusPrefixRatio);
        return timestamp <= focusStart
            ? MapTimestampToX(timestamp, start, focusStart, left, focusX)
            : MapTimestampToX(timestamp, focusStart, end, focusX, right);
    }

    internal static double MapTimestampToX(
        DateTimeOffset timestamp,
        DateTimeOffset start,
        DateTimeOffset end,
        double left,
        double right)
    {
        var width = Math.Max(0, right - left);
        var total = (end - start).TotalMilliseconds;
        if (width <= 0 || total <= 0 || !double.IsFinite(total))
        {
            return left;
        }

        return left + Math.Clamp((timestamp - start).TotalMilliseconds / total, 0, 1) * width;
    }

    private static double ToY(Viewport viewport, double percent) =>
        viewport.Bottom - (ClampPercent(percent) / 100d) * (viewport.Bottom - viewport.Top);

    private static double ClampPercent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    internal static IReadOnlyList<UsageTrendTimeTick> BuildTimeTicks(
        DateTimeOffset start,
        DateTimeOffset end)
    {
        if (end <= start)
        {
            return [];
        }

        var ticks = new List<UsageTrendTimeTick>();
        if (end - start <= HourlyTickMaximumWindow)
        {
            AddHourlyTicks(ticks, start, end);
        }
        else
        {
            AddSixHourTicks(ticks, start, end);
        }

        return ticks;
    }

    internal static IReadOnlyList<UsageTrendTimeTick> BuildDailyContextTicks(
        DateTimeOffset start,
        DateTimeOffset end)
    {
        if (end <= start)
        {
            return [];
        }

        var ticks = new List<UsageTrendTimeTick>();
        var candidate = CeilingToDayBoundary(start);
        while (candidate <= end && ticks.Count < 16)
        {
            var local = candidate.ToLocalTime();
            ticks.Add(new UsageTrendTimeTick(
                candidate,
                "00:00",
                local.ToString("ddd MMM d", CultureInfo.CurrentCulture)));
            candidate = NextDayBoundary(candidate);
        }

        return ticks;
    }

    internal static UsageTrendTimeLabelDensity ResolveTimeLabelDensity(
        DateTimeOffset start,
        DateTimeOffset end,
        double left,
        double right)
    {
        var ticks = BuildTimeTicks(start, end);
        if (ticks.Count < 2 || right <= left)
        {
            return UsageTrendTimeLabelDensity.Full;
        }

        var minimumSpacing = double.PositiveInfinity;
        var previousX = MapTimestampToX(ticks[0].Timestamp, start, end, left, right);
        foreach (var tick in ticks.Skip(1))
        {
            var x = MapTimestampToX(tick.Timestamp, start, end, left, right);
            var spacing = x - previousX;
            if (spacing > 0.5)
            {
                minimumSpacing = Math.Min(minimumSpacing, spacing);
            }

            previousX = x;
        }

        if (!double.IsFinite(minimumSpacing) || minimumSpacing >= FullTimeLabelMinimumSpacing)
        {
            return UsageTrendTimeLabelDensity.Full;
        }

        return minimumSpacing >= CompactTimeLabelMinimumSpacing
            ? UsageTrendTimeLabelDensity.CompactHours
            : UsageTrendTimeLabelDensity.MidnightOnly;
    }

    internal static string? FormatTimeTickLabel(
        UsageTrendTimeTick tick,
        UsageTrendTimeLabelDensity density)
    {
        return density switch
        {
            UsageTrendTimeLabelDensity.Full => tick.TimeLabel,
            UsageTrendTimeLabelDensity.CompactHours => tick.TimeLabel[..2],
            UsageTrendTimeLabelDensity.MidnightOnly when tick.DateLabel is not null => tick.TimeLabel,
            _ => null
        };
    }

    internal static string FormatPointTime(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("ddd HH:mm", CultureInfo.CurrentCulture);

    private static void AddHourlyTicks(List<UsageTrendTimeTick> ticks, DateTimeOffset start, DateTimeOffset end)
    {
        var candidate = CeilingToHourBoundary(start);
        while (candidate <= end && ticks.Count < 64)
        {
            AddTimeTick(ticks, candidate, includeDateLabel: false);
            candidate = NextHourBoundary(candidate);
        }
    }

    private static void AddSixHourTicks(List<UsageTrendTimeTick> ticks, DateTimeOffset start, DateTimeOffset end)
    {
        var candidate = CeilingToSixHourBoundary(start);
        while (candidate <= end && ticks.Count < 64)
        {
            AddTimeTick(ticks, candidate);
            candidate = NextSixHourBoundary(candidate);
        }
    }

    private static void AddTimeTick(
        List<UsageTrendTimeTick> ticks,
        DateTimeOffset timestamp,
        bool includeDateLabel = true)
    {
        if (ticks.Count > 0 && ticks[^1].Timestamp == timestamp)
        {
            return;
        }

        var local = timestamp.ToLocalTime();
        ticks.Add(new UsageTrendTimeTick(
            timestamp,
            local.ToString("HH:mm", CultureInfo.CurrentCulture),
            includeDateLabel && local.Hour == 0 && local.Minute == 0
                ? local.ToString("ddd MMM d", CultureInfo.CurrentCulture)
                : null));
    }

    private static DateTimeOffset CeilingToHourBoundary(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var candidate = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
        if (candidate < local.DateTime)
        {
            candidate = candidate.AddHours(1);
        }

        return FromLocalClock(candidate);
    }

    private static DateTimeOffset NextHourBoundary(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var next = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified).AddHours(1);
        return FromLocalClock(next);
    }

    private static DateTimeOffset CeilingToSixHourBoundary(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var boundaryHour = (local.Hour / 6) * 6;
        var candidate = new DateTime(local.Year, local.Month, local.Day, boundaryHour, 0, 0, DateTimeKind.Unspecified);
        if (candidate < local.DateTime)
        {
            candidate = candidate.AddHours(6);
        }

        return FromLocalClock(candidate);
    }

    private static DateTimeOffset NextSixHourBoundary(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var next = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified).AddHours(6);
        return FromLocalClock(next);
    }

    private static DateTimeOffset CeilingToDayBoundary(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        var candidate = local.Date;
        if (candidate < local.DateTime)
        {
            candidate = candidate.AddDays(1);
        }

        return FromLocalClock(candidate);
    }

    private static DateTimeOffset NextDayBoundary(DateTimeOffset timestamp) =>
        FromLocalClock(timestamp.ToLocalTime().Date.AddDays(1));

    private static DateTimeOffset FromLocalClock(DateTime localClock)
    {
        var unspecified = DateTime.SpecifyKind(localClock, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    private Size MeasureText(string text, double fontSize)
    {
        var formatted = CreateFormattedText(text, fontSize, AxisBrush);
        return new Size(formatted.WidthIncludingTrailingWhitespace, formatted.Height);
    }

    private void DrawText(DrawingContext context, string text, Point origin, double fontSize, Brush brush) =>
        context.DrawText(CreateFormattedText(text, fontSize, brush), origin);

    private double GetTopLabelY(string text, double fontSize, Viewport viewport)
    {
        var height = MeasureText(text, fontSize).Height;
        return Math.Max(2, viewport.Top - height - 5);
    }

    private void DrawCenteredText(
        DrawingContext context,
        string text,
        double centerX,
        double y,
        double fontSize,
        Brush brush,
        Viewport viewport)
    {
        var size = MeasureText(text, fontSize);
        var x = Math.Clamp(
            centerX - (size.Width / 2),
            viewport.Left,
            Math.Max(viewport.Left, viewport.Right - size.Width));
        DrawText(context, text, new Point(x, y), fontSize, brush);
    }

    private FormattedText CreateFormattedText(string text, double fontSize, Brush brush) =>
        new(text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, ChartTypeface, fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double AlignToPixel(double coordinate, bool horizontal)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = horizontal ? dpi.DpiScaleX : dpi.DpiScaleY;
        return Math.Round(coordinate * scale) / scale;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }

    private static Pen CreateDashedPen(Brush brush, double thickness, DashStyle dashStyle)
    {
        var pen = new Pen(brush, thickness) { DashStyle = dashStyle };
        return Freeze(pen);
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first >= second ? first : second;

    internal sealed record UsageTrendTimeTick(DateTimeOffset Timestamp, string TimeLabel, string? DateLabel);

    internal sealed record UsageTrendFocus(
        DateTimeOffset FocusStart,
        bool CompressUnmeasuredHistory,
        bool ShowContextStrip);

    internal enum UsageTrendTimeLabelDensity
    {
        Full,
        CompactHours,
        MidnightOnly
    }

    private sealed class UsageTrendChartAutomationPeer(UsageTrendChart owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(UsageTrendChart);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }

    private readonly record struct Viewport(
        double Left,
        double Top,
        double Right,
        double Bottom,
        DateTimeOffset Start,
        DateTimeOffset End,
        DateTimeOffset FocusStart,
        bool CompressUnmeasuredHistory,
        bool ShowContextStrip);
}
