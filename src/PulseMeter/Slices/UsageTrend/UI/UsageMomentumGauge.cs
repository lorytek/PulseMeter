using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace PulseMeter.Slices.UsageTrend.UI;

/// <summary>A compact speedometer-style indicator for usage pace versus its median baseline.</summary>
public sealed class UsageMomentumGauge : FrameworkElement
{
    private static readonly Brush Green = FrozenBrush("#22C55E");
    private static readonly Brush Neutral = FrozenBrush("#CBD5E1");
    private static readonly Brush Amber = FrozenBrush("#F59E0B");
    private static readonly Brush Red = FrozenBrush("#DC2626");
    private static readonly Brush Needle = FrozenBrush("#1D4ED8");
    private static readonly Brush Tick = FrozenBrush("#64748B");

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(UsageMomentumGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(
            double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, 168) : 168,
            60);

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight * 0.88);
        var radius = Math.Min(ActualWidth * 0.43, ActualHeight * 0.78);
        DrawArc(context, center, radius, 200, 242, Green);
        DrawArc(context, center, radius, 242, 300, Neutral);
        DrawArc(context, center, radius, 300, 328, Amber);
        DrawArc(context, center, radius, 328, 340, Red);

        DrawTick(context, center, radius, 200);
        DrawTick(context, center, radius, 270);
        DrawTick(context, center, radius, 340);

        var needleAngle = 270 + (Math.Clamp(double.IsFinite(Value) ? Value : 0, -1, 1) * 65);
        var needleEnd = PointOnCircle(center, radius * 0.72, needleAngle);
        var needlePen = new Pen(Needle, 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        needlePen.Freeze();
        context.DrawLine(needlePen, center, needleEnd);
        context.DrawEllipse(Needle, null, center, 3.5, 3.5);
    }

    private static void DrawArc(
        DrawingContext context,
        Point center,
        double radius,
        double startAngle,
        double endAngle,
        Brush brush)
    {
        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            drawing.BeginFigure(PointOnCircle(center, radius, startAngle), false, false);
            drawing.ArcTo(
                PointOnCircle(center, radius, endAngle),
                new Size(radius, radius),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise,
                true,
                false);
        }

        geometry.Freeze();
        var pen = new Pen(brush, 5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawTick(DrawingContext context, Point center, double radius, double angle)
    {
        var pen = new Pen(Tick, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        context.DrawLine(
            pen,
            PointOnCircle(center, radius - 1, angle),
            PointOnCircle(center, radius - 7, angle));
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }

    private static SolidColorBrush FrozenBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
