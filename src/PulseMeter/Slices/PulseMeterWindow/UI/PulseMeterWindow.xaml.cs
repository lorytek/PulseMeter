using System.Windows;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfPoint = System.Windows.Point;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfSize = System.Windows.Size;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace PulseMeter.Slices.PulseMeterWindow.UI;

public partial class PulseMeterWindow : System.Windows.Window, IPulseMeterWindow
{
    private const int WmSize = 0x0005;
    private const int WmNcHitTest = 0x0084;
    private const int WmSysCommand = 0x0112;
    private const int SizeMaximized = 2;
    private const int SysCommandMask = 0xFFF0;
    private const int ScMaximize = 0xF030;
    private const int SwRestore = 9;
    private const double WorkAreaPadding = 24;

    private PulseMeterWindowViewModel? _boundViewModel;
    private bool _isApplyingViewModelSize;
    private bool _isApplyingWindowPlacement;
    private bool _isProgrammaticSectionScroll;
    private HwndSource? _windowSource;

    public IPulseMeterWindowStateStore? WindowStateStore { get; set; }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public PulseMeterWindow()
    {
        InitializeComponent();
        WindowSurface.AddHandler(
            MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Surface_MouseLeftButtonDown),
            handledEventsToo: true);
        DataContextChanged += OnDataContextChanged;
        SourceInitialized += OnSourceInitialized;
        LocationChanged += Window_LocationChanged;
        StateChanged += Window_StateChanged;
        Closed += OnClosed;
        Loaded += (_, _) =>
        {
            ApplyViewModelBounds();
            SaveWindowState();
        };
    }

    void IPulseMeterWindow.Invoke(Action action)
    {
        Dispatcher.Invoke(action);
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed
            || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            RestoreExpandedWindowToNormalSize();
            e.Handled = true;
            return;
        }

        e.Handled = true;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RestoreExpandedWindowToNormalSize()
    {
        if (DataContext is not PulseMeterWindowViewModel { IsExpanded: true } viewModel)
        {
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        viewModel.RestoreExpandedWindowToNormalSize();
        SaveWindowState();
    }

    private void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PulseMeterWindowViewModel viewModel)
        {
            viewModel.ToggleExpanded();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PulseMeterWindowViewModel viewModel)
        {
            viewModel.MarkHiddenByUser();
        }

        Hide();
    }

    private void NavigationRail_SectionRequested(object? sender, NavigationSectionRequestedEventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        if (e.Section == NavigationSection.Overview)
        {
            ExpandedContentScrollViewer.ScrollToTop();
            return;
        }

        var target = GetSectionTarget(e.Section);
        if (target is null || target.Visibility != Visibility.Visible)
        {
            _boundViewModel.NavigationRail.SelectSection(NavigationSection.Overview);
            ExpandedContentScrollViewer.ScrollToTop();
            return;
        }

        _isProgrammaticSectionScroll = true;
        var targetTop = target
            .TransformToAncestor(ExpandedContentScrollViewer)
            .Transform(new WpfPoint())
            .Y;
        var targetOffset = Math.Clamp(
            ExpandedContentScrollViewer.VerticalOffset + targetTop,
            0,
            ExpandedContentScrollViewer.ScrollableHeight);
        ExpandedContentScrollViewer.ScrollToVerticalOffset(targetOffset);
        Dispatcher.BeginInvoke(new Action(() => _isProgrammaticSectionScroll = false));
    }

    private void ExpandedContentScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (_isProgrammaticSectionScroll || _boundViewModel is null)
        {
            return;
        }

        var visibleSections = new[]
        {
            (NavigationSection.RateLimits, (FrameworkElement)RateLimitsSection),
            (NavigationSection.WeeklyPace, (FrameworkElement)WeeklyPaceSection),
            (NavigationSection.ResetCredits, (FrameworkElement)ResetCreditsSection),
            (NavigationSection.AccountUsage, (FrameworkElement)AccountUsageSection),
            (NavigationSection.ProjectUsage, (FrameworkElement)ProjectUsageSection),
            (NavigationSection.BurnAnalysis, (FrameworkElement)BurnAnalysisSection),
            (NavigationSection.DailyUsage, (FrameworkElement)DailyUsageSection)
        }.Where(item => item.Item2.Visibility == Visibility.Visible).ToList();

        var viewportTop = 20d;
        var current = visibleSections
            .Where(item => item.Item2.TransformToAncestor(ExpandedContentScrollViewer).Transform(new WpfPoint()).Y <= viewportTop)
            .Select(item => item.Item1)
            .LastOrDefault();

        _boundViewModel.NavigationRail.SelectSection(current == default ? NavigationSection.Overview : current);
    }

    private FrameworkElement? GetSectionTarget(NavigationSection section)
    {
        return section switch
        {
            NavigationSection.RateLimits => RateLimitsSection,
            NavigationSection.WeeklyPace => WeeklyPaceSection,
            NavigationSection.ResetCredits => ResetCreditsSection,
            NavigationSection.AccountUsage => AccountUsageSection,
            NavigationSection.ProjectUsage => ProjectUsageSection,
            NavigationSection.BurnAnalysis => BurnAnalysisSection,
            NavigationSection.DailyUsage => DailyUsageSection,
            _ => null
        };
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfSelector or WpfComboBoxItem or WpfButtonBase or WpfTextBoxBase or WpfScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = (HwndSource?)PresentationSource.FromVisual(this);
        _windowSource?.AddHook(WndProc);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _windowSource?.RemoveHook(WndProc);
        SaveWindowState();
    }

    private void MoveToTopRight(double width, double height, Rect workArea)
    {
        var position = PulseMeterWindowPlacementCalculator.Clamp(
            workArea.Right - width - WorkAreaPadding,
            workArea.Top + WorkAreaPadding,
            width,
            height,
            workArea,
            WorkAreaPadding);
        Left = position.Left;
        Top = position.Top;
    }

    private void ApplyWindowPosition(PulseMeterWindowViewModel viewModel, WpfSize fittedSize, Rect workArea)
    {
        _isApplyingWindowPlacement = true;
        try
        {
            if (viewModel.WindowLeft is double left && viewModel.WindowTop is double top)
            {
                var clamped = ClampWindowPosition(left, top, fittedSize.Width, fittedSize.Height, workArea);
                Left = clamped.Left;
                Top = clamped.Top;
            }
            else
            {
                MoveToTopRight(fittedSize.Width, fittedSize.Height, workArea);
            }
        }
        finally
        {
            _isApplyingWindowPlacement = false;
        }
    }

    private static (double Left, double Top) ClampWindowPosition(
        double left,
        double top,
        double width,
        double height,
        Rect workArea)
    {
        var clamped = PulseMeterWindowPlacementCalculator.Clamp(
            left,
            top,
            width,
            height,
            workArea,
            WorkAreaPadding);
        return (clamped.Left, clamped.Top);
    }

    private static WpfSize GetFittedWindowSize(PulseMeterWindowViewModel viewModel, Rect workArea)
    {
        return PulseMeterWindowPlacementCalculator.FitSize(
            viewModel.WindowWidth,
            viewModel.WindowHeight,
            workArea,
            WorkAreaPadding);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (CanRememberWindowPlacement() && DataContext is PulseMeterWindowViewModel viewModel)
        {
            if (viewModel.IsExpanded && !viewModel.HasWindowPosition)
            {
                viewModel.RememberWindowPosition(Left, Top);
            }

            viewModel.RememberWindowSize(ActualWidth, ActualHeight);
            viewModel.UpdateExpandedLayoutScale(ActualWidth, ActualHeight);
        }

        if (IsLoaded && WindowState == WindowState.Normal)
        {
            SaveWindowState();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || !CanRememberWindowPlacement())
        {
            return;
        }

        if (DataContext is PulseMeterWindowViewModel viewModel)
        {
            viewModel.RememberWindowPosition(Left, Top);
            SaveWindowState();
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreMaximizedWindowToViewModelSize();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _boundViewModel = e.NewValue as PulseMeterWindowViewModel;
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyViewModelBounds();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PulseMeterWindowViewModel.IsExpanded)
            or nameof(PulseMeterWindowViewModel.WindowHeight)
            or nameof(PulseMeterWindowViewModel.WindowMinHeight)
            or nameof(PulseMeterWindowViewModel.WindowWidth)
            or nameof(PulseMeterWindowViewModel.WindowMinWidth))
        {
            ApplyViewModelBounds();

            SaveWindowState();
        }
    }

    private void ApplyViewModelBounds()
    {
        if (DataContext is not PulseMeterWindowViewModel viewModel)
        {
            return;
        }

        var workArea = WindowMonitorWorkArea.GetFor(this);
        var fittedSize = GetFittedWindowSize(viewModel, workArea);
        ApplyViewModelSize(viewModel, fittedSize);
        viewModel.UpdateExpandedLayoutScale(ActualWidth, ActualHeight);
        ApplyWindowPosition(viewModel, fittedSize, workArea);
    }

    private void ApplyViewModelSize(PulseMeterWindowViewModel viewModel, WpfSize fittedSize)
    {
        _isApplyingViewModelSize = true;
        try
        {
            if (WindowState != WindowState.Normal)
            {
                WindowState = WindowState.Normal;
            }

            MinWidth = Math.Min(viewModel.WindowMinWidth, fittedSize.Width);
            MinHeight = Math.Min(viewModel.WindowMinHeight, fittedSize.Height);
            ResizeMode = System.Windows.ResizeMode.CanResize;
            Width = fittedSize.Width;
            Height = fittedSize.Height;
        }
        finally
        {
            _isApplyingViewModelSize = false;
        }
    }

    private bool CanRememberWindowPlacement()
    {
        return WindowState == WindowState.Normal
            && !_isApplyingViewModelSize
            && !_isApplyingWindowPlacement;
    }

    private void SaveWindowState()
    {
        if (DataContext is PulseMeterWindowViewModel viewModel)
        {
            WindowStateStore?.Save(viewModel.CaptureWindowState());
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && (wParam.ToInt64() & SysCommandMask) == ScMaximize)
        {
            RestoreMaximizedWindowToViewModelSize();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WmSize && wParam.ToInt32() == SizeMaximized)
        {
            RestoreMaximizedWindowToViewModelSize();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg != WmNcHitTest || !CanResizeFromWindowBorder())
        {
            return IntPtr.Zero;
        }

        var resizeHit = WindowResizeHitTester.GetResizeHitTest(
            PointFromScreen(GetScreenPoint(lParam)),
            ActualWidth,
            ActualHeight);
        if (resizeHit is not int hitTest)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private bool CanResizeFromWindowBorder()
    {
        return ResizeMode is System.Windows.ResizeMode.CanResize;
    }

    private void RestoreMaximizedWindowToViewModelSize()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SwRestore);
            }

            ApplyViewModelBounds();
            SaveWindowState();
        }));
    }

    private static WpfPoint GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new WpfPoint(x, y);
    }
}
