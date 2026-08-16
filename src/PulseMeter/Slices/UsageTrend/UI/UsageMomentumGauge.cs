using System.Windows;
using System.Windows.Automation.Peers;
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
    private const double LiveMotionPeriodSeconds = 2.0;
    private const double LiveMotionAmplitude = 0.06;
    private static readonly TimeSpan MinimumFrameInterval = TimeSpan.FromMilliseconds(30);
    private static readonly Brush Green = FrozenBrush("#22C55E");
    private static readonly Brush Neutral = FrozenBrush("#CBD5E1");
    private static readonly Brush Amber = FrozenBrush("#F59E0B");
    private static readonly Brush Red = FrozenBrush("#DC2626");
    private static readonly Brush Needle = FrozenBrush("#1D4ED8");
    private static readonly Brush Tick = FrozenBrush("#64748B");
    private bool _isRendering;
    private bool _isSystemParametersSubscribed;
    private bool _hasDisplayValue;
    private double _displayValue;
    private double _animationSeconds;
    private TimeSpan? _lastRenderingTime;

    public UsageMomentumGauge()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(UsageMomentumGauge),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnAnimationPropertyChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty IsLearningProperty = DependencyProperty.Register(
        nameof(IsLearning),
        typeof(bool),
        typeof(UsageMomentumGauge),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnAnimationPropertyChanged));

    public bool IsLearning
    {
        get => (bool)GetValue(IsLearningProperty);
        set => SetValue(IsLearningProperty, value);
    }

    public static readonly DependencyProperty BaselineProgressProperty = DependencyProperty.Register(
        nameof(BaselineProgress),
        typeof(double),
        typeof(UsageMomentumGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double BaselineProgress
    {
        get => (double)GetValue(BaselineProgressProperty);
        set => SetValue(BaselineProgressProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(
            double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, 168) : 168,
            60);

    protected override AutomationPeer OnCreateAutomationPeer() => new UsageMomentumGaugeAutomationPeer(this);

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight * 0.88);
        var radius = Math.Min(ActualWidth * 0.43, ActualHeight * 0.78);

        if (IsLearning)
        {
            DrawArc(context, center, radius, 200, 340, Neutral);
            DrawTick(context, center, radius, 200);
            DrawTick(context, center, radius, 270);
            DrawTick(context, center, radius, 340);
            return;
        }

        DrawArc(context, center, radius, 200, 242, Green);
        DrawArc(context, center, radius, 242, 300, Neutral);
        DrawArc(context, center, radius, 300, 328, Amber);
        DrawArc(context, center, radius, 328, 340, Red);

        DrawTick(context, center, radius, 200);
        DrawTick(context, center, radius, 270);
        DrawTick(context, center, radius, 340);

        var targetValue = NormalizeValue(Value);
        var renderedValue = _isRendering
            ? CalculateLiveNeedleValue(_hasDisplayValue ? _displayValue : targetValue, _animationSeconds)
            : targetValue;
        var needleAngle = 270 + (renderedValue * 65);
        var needleEnd = PointOnCircle(center, radius * 0.72, needleAngle);
        var needlePen = new Pen(Needle, 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        needlePen.Freeze();
        context.DrawLine(needlePen, center, needleEnd);

        if (_isRendering)
        {
            var pulse = 0.5 + (0.5 * Math.Sin((_animationSeconds / LiveMotionPeriodSeconds) * Math.PI * 2));
            var pulseBrush = new SolidColorBrush(Color.FromArgb((byte)(34 + (pulse * 32)), 29, 78, 216));
            pulseBrush.Freeze();
            var pulsePen = new Pen(pulseBrush, 1.25);
            pulsePen.Freeze();
            var pulseRadius = 5.25 + (pulse * 1.5);
            context.DrawEllipse(null, pulsePen, center, pulseRadius, pulseRadius);
        }

        context.DrawEllipse(Needle, null, center, 3.5, 3.5);
    }

    internal static double CalculateLiveNeedleValue(double targetValue, double elapsedSeconds)
    {
        var target = NormalizeValue(targetValue);
        var elapsed = double.IsFinite(elapsedSeconds) ? elapsedSeconds : 0;
        var motion = Math.Sin((elapsed / LiveMotionPeriodSeconds) * Math.PI * 2) * LiveMotionAmplitude;
        return Math.Clamp(target + motion, -1, 1);
    }

    private static double NormalizeValue(double value) =>
        Math.Clamp(double.IsFinite(value) ? value : 0, -1, 1);

    private static void OnAnimationPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var gauge = (UsageMomentumGauge)dependencyObject;
        gauge.UpdateAnimationSubscription();
        gauge.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!_isSystemParametersSubscribed)
        {
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
            _isSystemParametersSubscribed = true;
        }

        if (!_hasDisplayValue)
        {
            _displayValue = NormalizeValue(Value);
            _hasDisplayValue = true;
        }

        UpdateAnimationSubscription();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        StopRendering();
        if (_isSystemParametersSubscribed)
        {
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            _isSystemParametersSubscribed = false;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args) =>
        UpdateAnimationSubscription();

    private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName.Equals(nameof(SystemParameters.ClientAreaAnimation), StringComparison.Ordinal))
        {
            UpdateAnimationSubscription();
        }
    }

    private void UpdateAnimationSubscription()
    {
        var shouldRender = IsLoaded
            && IsVisible
            && !IsLearning
            && SystemParameters.ClientAreaAnimation;

        if (shouldRender == _isRendering)
        {
            return;
        }

        if (shouldRender)
        {
            _displayValue = _hasDisplayValue ? _displayValue : NormalizeValue(Value);
            _hasDisplayValue = true;
            _lastRenderingTime = null;
            CompositionTarget.Rendering += OnRendering;
            _isRendering = true;
        }
        else
        {
            StopRendering();
            _displayValue = NormalizeValue(Value);
            _hasDisplayValue = true;
            InvalidateVisual();
        }
    }

    private void StopRendering()
    {
        if (_isRendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isRendering = false;
        }

        _lastRenderingTime = null;
    }

    private void OnRendering(object? sender, EventArgs args)
    {
        if (args is not RenderingEventArgs renderingArgs)
        {
            return;
        }

        if (_lastRenderingTime is null)
        {
            _lastRenderingTime = renderingArgs.RenderingTime;
            return;
        }

        var elapsed = renderingArgs.RenderingTime - _lastRenderingTime.Value;
        if (elapsed < MinimumFrameInterval)
        {
            return;
        }

        _lastRenderingTime = renderingArgs.RenderingTime;
        var seconds = Math.Clamp(elapsed.TotalSeconds, 0, 0.1);
        var target = NormalizeValue(Value);
        var easing = 1 - Math.Exp(-8 * seconds);
        _displayValue += (target - _displayValue) * easing;
        if (Math.Abs(target - _displayValue) < 0.0005)
        {
            _displayValue = target;
        }

        _animationSeconds = (_animationSeconds + seconds) % LiveMotionPeriodSeconds;
        InvalidateVisual();
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

    private sealed class UsageMomentumGaugeAutomationPeer(UsageMomentumGauge owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override string GetClassNameCore() => nameof(UsageMomentumGauge);

        protected override bool IsControlElementCore() => IsAvailableToAutomation();

        protected override bool IsContentElementCore() => IsAvailableToAutomation();

        private bool IsAvailableToAutomation()
        {
            var owner = (UsageMomentumGauge)Owner;
            return owner.Visibility == Visibility.Visible
                && (owner.IsVisible || PresentationSource.FromVisual(owner) is null);
        }
    }
}
