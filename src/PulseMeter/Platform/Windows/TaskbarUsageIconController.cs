using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using PulseMeter.Slices.ExpandedHeader.UI;
using PulseMeter.Slices.PulseMeterWindow.UI;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace PulseMeter.Platform.Windows;

public sealed class TaskbarUsageIconController : IDisposable
{
    private readonly Window _window;
    private readonly ImageSource? _defaultIcon;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly PropertyChangedEventHandler _expandedHeaderPropertyChangedHandler;
    private readonly Dictionary<int, ImageSource> _usageIcons = new();
    private PulseMeterWindowViewModel? _viewModel;
    private TrayIconPresentation? _appliedPresentation;
    private bool _disposed;

    public TaskbarUsageIconController(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _defaultIcon = window.Icon;
        _window.TaskbarItemInfo ??= new TaskbarItemInfo();
        _viewModelPropertyChangedHandler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(PulseMeterWindowViewModel.TrayConfidenceState))
            {
                QueueApply();
            }
        };
        _expandedHeaderPropertyChangedHandler = (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(ExpandedHeaderViewModel.WeeklyUsageText))
            {
                QueueApply();
            }
        };
    }

    public void Bind(PulseMeterWindowViewModel? viewModel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_viewModel, viewModel))
        {
            QueueApply();
            return;
        }

        Unsubscribe();
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
            _viewModel.ExpandedHeader.PropertyChanged += _expandedHeaderPropertyChangedHandler;
        }

        _appliedPresentation = null;
        QueueApply();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unsubscribe();
        _usageIcons.Clear();
    }

    private void QueueApply()
    {
        if (_disposed)
        {
            return;
        }

        if (_window.Dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        _ = _window.Dispatcher.BeginInvoke(
            DispatcherPriority.DataBind,
            new Action(Apply));
    }

    private void Apply()
    {
        if (_disposed)
        {
            return;
        }

        var presentation = _viewModel is null
            ? TrayIconPresentation.Create(TrayConfidenceState.Starting, null)
            : TrayIconPresentation.Create(
                _viewModel.TrayConfidenceState,
                GetWeeklyRemainingPercent(_viewModel));
        if (_appliedPresentation == presentation)
        {
            return;
        }

        _window.Icon = presentation.RemainingPercent is int remainingPercent
            ? GetUsageIcon(remainingPercent)
            : _defaultIcon;
        if (_window.TaskbarItemInfo is not null)
        {
            _window.TaskbarItemInfo.Description = TrayConfidenceBeacon.Tooltip(presentation);
        }

        _appliedPresentation = presentation;
    }

    private ImageSource GetUsageIcon(int remainingPercent)
    {
        if (!_usageIcons.TryGetValue(remainingPercent, out var icon))
        {
            icon = TaskbarUsageBadgeRenderer.Create(remainingPercent);
            _usageIcons.Add(remainingPercent, icon);
        }

        return icon;
    }

    private static double? GetWeeklyRemainingPercent(PulseMeterWindowViewModel viewModel)
    {
        var weekly = viewModel.CompactQuotaRows.FirstOrDefault(row => row.IsWeekly);
        return weekly is not null
               && weekly.RemainingPercentText.Contains('%')
               && double.IsFinite(weekly.RemainingPercentValue)
            ? weekly.RemainingPercentValue
            : null;
    }

    private void Unsubscribe()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.ExpandedHeader.PropertyChanged -= _expandedHeaderPropertyChangedHandler;
        _viewModel = null;
    }
}

public static class TaskbarUsageBadgeRenderer
{
    private const int PixelSize = 64;

    public static ImageSource Create(int remainingPercent)
    {
        var boundedPercent = Math.Clamp(remainingPercent, 0, 100);
        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            var color = TrayConfidenceBeacon.UsageBadgeColor(boundedPercent);
            var background = new SolidColorBrush(WpfColor.FromRgb(color.R, color.G, color.B));
            var outline = new WpfPen(new SolidColorBrush(WpfColor.FromArgb(72, 15, 23, 42)), 2);
            drawingContext.DrawRoundedRectangle(
                background,
                outline,
                new Rect(2, 2, PixelSize - 4, PixelSize - 4),
                12,
                12);

            var fontSize = boundedPercent >= 100 ? 23d : 31d;
            var text = new FormattedText(
                boundedPercent.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new WpfFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                fontSize,
                WpfBrushes.White,
                pixelsPerDip: 1);
            drawingContext.DrawText(
                text,
                new WpfPoint(
                    (PixelSize - text.Width) / 2,
                    (PixelSize - text.Height) / 2 - 1));
        }

        var bitmap = new RenderTargetBitmap(
            PixelSize,
            PixelSize,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);
        bitmap.Freeze();
        return bitmap;
    }
}
