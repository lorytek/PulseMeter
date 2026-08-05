using System.Windows;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;
using PulseMeter.Slices.NeedsAttention.UI;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfPoint = System.Windows.Point;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfSize = System.Windows.Size;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

using PulseMeter.Platform.Diagnostics;

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
    private const int SwShowNoActivate = 4;
    private const double WorkAreaPadding = 24;

    private PulseMeterWindowViewModel? _boundViewModel;
    private bool _isApplyingViewModelSize;
    private bool _isApplyingWindowPlacement;
    private bool _isProgrammaticSectionScroll;
    private bool _isClosingForShutdown;
    private DispatcherTimer? _expandCollapseFocusTimer;
    private HwndSource? _windowSource;

    public IPulseMeterWindowStateStore? WindowStateStore { get; set; }

    IntPtr IPulseMeterWindow.Handle => _windowSource?.Handle ?? IntPtr.Zero;

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
        Closing += OnClosing;
        LocationChanged += Window_LocationChanged;
        StateChanged += Window_StateChanged;
        Closed += OnClosed;
        Loaded += (_, _) =>
        {
            ApplyViewModelBounds();
            UpdateNavigationBottomSpacer();
            SaveWindowState();
        };
    }

    void IPulseMeterWindow.Invoke(Action action)
    {
        Dispatcher.Invoke(action);
    }

    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void ShowWithoutActivation()
    {
        if (!IsVisible)
        {
            var showActivated = ShowActivated;
            ShowActivated = false;
            try
            {
                Show();
            }
            finally
            {
                ShowActivated = showActivated;
            }
        }

        if (WindowState == WindowState.Minimized)
        {
            var handle = _windowSource?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SwShowNoActivate);
            }
        }
    }

    public void CloseForShutdown()
    {
        _isClosingForShutdown = true;
        Close();
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
            ScheduleExpandCollapseFocus(viewModel.IsExpanded);
        }
    }

    private void ScheduleExpandCollapseFocus(bool expectedExpanded)
    {
        _expandCollapseFocusTimer?.Stop();

        var timer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        var attemptsRemaining = 20;
        _expandCollapseFocusTimer = timer;
        timer.Tick += (_, _) =>
        {
            if (DataContext is not PulseMeterWindowViewModel currentViewModel
                || currentViewModel.IsExpanded != expectedExpanded)
            {
                CompleteFocusTransfer(timer);
                return;
            }

            var focusTransferred = expectedExpanded
                ? ExpandedHeaderControl.FocusExpandCollapseButton()
                : CompactDataBar.FocusExpandCollapseButton();
            attemptsRemaining--;
            if (focusTransferred || attemptsRemaining <= 0)
            {
                CompleteFocusTransfer(timer);
            }
        };
        timer.Start();
    }

    private void CompleteFocusTransfer(DispatcherTimer timer)
    {
        timer.Stop();
        if (ReferenceEquals(_expandCollapseFocusTimer, timer))
        {
            _expandCollapseFocusTimer = null;
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
        NavigateToSection(e.Section, restoreHiddenSection: false);
    }

    private void UsageTrendSection_SectionRequested(object? sender, NavigationSectionRequestedEventArgs e)
    {
        NavigateToSection(e.Section, restoreHiddenSection: false);
    }

    private void NeedsAttentionSection_ReviewRequested(object? sender, NeedsAttentionReviewRequestedEventArgs e)
    {
        NavigateToSection(GetNavigationSection(e.Target), restoreHiddenSection: true);
    }

    private void DailyUsageSection_ExpansionToggling(object? sender, EventArgs e)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _boundViewModel.NavigationRail.SelectSection(NavigationSection.DailyUsage);
        _isProgrammaticSectionScroll = true;
    }

    private static NavigationSection GetNavigationSection(NeedsAttentionReviewTarget target)
    {
        return target switch
        {
            NeedsAttentionReviewTarget.RunwayForecast => NavigationSection.RunwayForecast,
            NeedsAttentionReviewTarget.RateLimits => NavigationSection.RateLimits,
            NeedsAttentionReviewTarget.ResetCredits => NavigationSection.ResetCredits,
            NeedsAttentionReviewTarget.DailyUsage => NavigationSection.DailyUsage,
            NeedsAttentionReviewTarget.ProjectUsage => NavigationSection.ProjectUsage,
            _ => NavigationSection.Overview
        };
    }

    private void NavigateToSection(NavigationSection section, bool restoreHiddenSection)
    {
        if (_boundViewModel is null)
        {
            return;
        }

        if (restoreHiddenSection)
        {
            _boundViewModel.NavigationRail.RevealAndSelectSection(section);
        }

        if (section == NavigationSection.Overview)
        {
            ExpandedContentScrollViewer.ScrollToTop();
            return;
        }

        var target = GetSectionTarget(section);
        if (target is null || target.Visibility != Visibility.Visible)
        {
            _boundViewModel.NavigationRail.SelectSection(NavigationSection.Overview);
            ExpandedContentScrollViewer.ScrollToTop();
            return;
        }

        _isProgrammaticSectionScroll = true;
        try
        {
            UpdateNavigationBottomSpacer();
            ExpandedContentScrollViewer.UpdateLayout();

            var targetTop = target
                .TransformToAncestor(ExpandedContentScrollViewer)
                .Transform(new WpfPoint())
                .Y;
            var targetOffset = Math.Clamp(
                ExpandedContentScrollViewer.VerticalOffset + targetTop,
                0,
                ExpandedContentScrollViewer.ScrollableHeight);
            ExpandedContentScrollViewer.ScrollToVerticalOffset(targetOffset);
        }
        catch
        {
            _isProgrammaticSectionScroll = false;
            throw;
        }

        Dispatcher.BeginInvoke(
            new Action(() => _isProgrammaticSectionScroll = false),
            DispatcherPriority.ApplicationIdle);
    }

    private void PreserveSelectedSectionAfterDailyUsageLayoutChange()
    {
        if (_boundViewModel is null || !IsLoaded)
        {
            return;
        }

        var selectedSection = _boundViewModel.NavigationRail.SelectedSection;
        if (selectedSection == NavigationSection.Overview)
        {
            Dispatcher.BeginInvoke(new Action(UpdateNavigationBottomSpacer), DispatcherPriority.Loaded);
            return;
        }

        _isProgrammaticSectionScroll = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_boundViewModel?.NavigationRail.SelectedSection != selectedSection || !IsLoaded)
            {
                _isProgrammaticSectionScroll = false;
                return;
            }

            ExpandedContentScrollViewer.UpdateLayout();
            UpdateNavigationBottomSpacer();
            ExpandedContentScrollViewer.UpdateLayout();
            NavigateToSection(selectedSection, restoreHiddenSection: false);
        }), DispatcherPriority.Loaded);
    }

    private void ExpandedContentScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (_isProgrammaticSectionScroll || _boundViewModel is null)
        {
            return;
        }

        if (Math.Abs(e.ExtentHeightChange) > 0.01
            && _boundViewModel.NavigationRail.SelectedSection != NavigationSection.Overview)
        {
            PreserveSelectedSectionAfterDailyUsageLayoutChange();
            return;
        }

        var visibleSections = new[]
        {
            (NavigationSection.RateLimits, (FrameworkElement)RateLimitsSection),
            (NavigationSection.WeeklyPace, (FrameworkElement)WeeklyPaceSection),
            (NavigationSection.RunwayForecast, (FrameworkElement)UsageTrendSection),
            (NavigationSection.BlockPlanner, (FrameworkElement)BlockPlannerSection),
            (NavigationSection.ResetCredits, (FrameworkElement)ResetCreditsSection),
            (NavigationSection.AccountUsage, (FrameworkElement)AccountUsageSection),
            (NavigationSection.ProjectUsage, (FrameworkElement)ProjectUsageSection),
            (NavigationSection.BurnAnalysis, (FrameworkElement)BurnAnalysisSection),
            (NavigationSection.DailyUsage, (FrameworkElement)DailyUsageSection)
        }.Where(item => item.Item2.Visibility == Visibility.Visible).ToList();

        var sectionBounds = visibleSections
            .Select(item =>
            {
                var top = item.Item2.TransformToAncestor(ExpandedContentScrollViewer).Transform(new WpfPoint()).Y;
                return (Section: item.Item1, Top: top, Bottom: top + item.Item2.ActualHeight);
            })
            .ToArray();
        var current = SelectSectionForScroll(
            sectionBounds,
            ExpandedContentScrollViewer.VerticalOffset,
            ExpandedContentScrollViewer.ViewportHeight);

        _boundViewModel.NavigationRail.SelectSection(current);
    }

    internal static NavigationSection SelectSectionForScroll(
        IReadOnlyList<(NavigationSection Section, double Top, double Bottom)> sections,
        double verticalOffset,
        double viewportHeight)
    {
        if (verticalOffset <= 20 || sections.Count == 0 || !double.IsFinite(viewportHeight) || viewportHeight <= 0)
        {
            return NavigationSection.Overview;
        }

        const double alignedTopTolerance = 48;
        var alignedAtTop = sections
            .Where(item => double.IsFinite(item.Top)
                && double.IsFinite(item.Bottom)
                && item.Top >= -alignedTopTolerance
                && item.Top <= alignedTopTolerance
                && item.Bottom > 0)
            .OrderBy(item => Math.Abs(item.Top))
            .Select(item => (NavigationSection?)item.Section)
            .FirstOrDefault();
        if (alignedAtTop is NavigationSection alignedSection)
        {
            return alignedSection;
        }

        var probe = Math.Clamp(viewportHeight * 0.35, 20, Math.Max(20, viewportHeight - 1));
        var containing = sections
            .Where(item => double.IsFinite(item.Top)
                && double.IsFinite(item.Bottom)
                && item.Top <= probe
                && item.Bottom > probe)
            .Select(item => (NavigationSection?)item.Section)
            .LastOrDefault();
        if (containing is NavigationSection section)
        {
            return section;
        }

        return sections
            .Where(item => double.IsFinite(item.Top) && double.IsFinite(item.Bottom))
            .Select(item => (item.Section, VisibleHeight: Math.Max(0, Math.Min(item.Bottom, viewportHeight) - Math.Max(item.Top, 0))))
            .OrderByDescending(item => item.VisibleHeight)
            .Select(item => item.VisibleHeight > 0 ? item.Section : NavigationSection.Overview)
            .FirstOrDefault();
    }

    private FrameworkElement? GetSectionTarget(NavigationSection section)
    {
        return section switch
        {
            NavigationSection.RateLimits => RateLimitsSection,
            NavigationSection.RunwayForecast => UsageTrendSection,
            NavigationSection.BlockPlanner => BlockPlannerSection,
            NavigationSection.WeeklyPace => WeeklyPaceSection,
            NavigationSection.ResetCredits => ResetCreditsSection,
            NavigationSection.AccountUsage => AccountUsageSection,
            NavigationSection.ProjectUsage => ProjectUsageSection,
            NavigationSection.BurnAnalysis => BurnAnalysisSection,
            NavigationSection.DailyUsage => DailyUsageSection,
            _ => null
        };
    }

    private void UpdateNavigationBottomSpacer()
    {
        if (NavigationBottomSpacer is null
            || ExpandedContentScrollViewer is null
            || DailyUsageSection is null
            || ExpandedContentStackPanel is null)
        {
            return;
        }

        NavigationBottomSpacer.Height = CalculateNavigationBottomSpacerHeight(
            ExpandedContentScrollViewer.ViewportHeight,
            DailyUsageSection.ActualHeight,
            ExpandedContentStackPanel.Margin.Bottom);
    }

    internal static double CalculateNavigationBottomSpacerHeight(
        double viewportHeight,
        double lastSectionHeight,
        double contentBottomMargin)
    {
        if (!double.IsFinite(viewportHeight)
            || !double.IsFinite(lastSectionHeight)
            || !double.IsFinite(contentBottomMargin)
            || viewportHeight <= 0
            || lastSectionHeight < 0
            || contentBottomMargin < 0)
        {
            return 0;
        }

        return Math.Max(0, viewportHeight - lastSectionHeight - contentBottomMargin);
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
        ApplySavedViewModelPosition();
        ApplyViewModelBounds();
    }

    private void ApplySavedViewModelPosition()
    {
        if (DataContext is not PulseMeterWindowViewModel viewModel
            || viewModel.WindowLeft is not double left
            || viewModel.WindowTop is not double top)
        {
            return;
        }

        _isApplyingWindowPlacement = true;
        try
        {
            Left = left;
            Top = top;
        }
        finally
        {
            _isApplyingWindowPlacement = false;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _windowSource?.RemoveHook(WndProc);
        _windowSource = null;
        SaveWindowState();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingForShutdown || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        if (DataContext is PulseMeterWindowViewModel viewModel)
        {
            viewModel.MarkHiddenByUser();
        }

        Hide();
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
        UpdateNavigationBottomSpacer();

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

        if (_windowSource is not null)
        {
            ApplyViewModelBounds();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PulseMeterWindowViewModel.IsDailyUsageExpanded))
        {
            PreserveSelectedSectionAfterDailyUsageLayoutChange();
        }

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
            try
            {
                if (WindowStateStore?.Save(viewModel.CaptureWindowState()) is false)
                {
                    PrivacySafeDiagnostics.WriteInfo("window state could not be persisted; retrying later");
                }
            }
            catch (Exception exception)
            {
                PrivacySafeDiagnostics.WriteFailure("window state persistence failed", exception);
            }
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
