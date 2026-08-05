using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

[Collection(UsageTrendWpfCollection.Name)]
public sealed class PulseMeterWindowVisibilityLifecycleTests
{
    [Fact]
    public void NormalClose_HidesSingletonWindow_AndTrayShowCanRestoreIt()
    {
        RunOnStaThread(() =>
        {
            var window = new PulseMeterWindow();
            window.Show();
            window.WindowState = WindowState.Minimized;

            window.Close();

            Assert.False(window.IsVisible);
            Assert.NotEqual(IntPtr.Zero, ((IPulseMeterWindow)window).Handle);

            window.ShowAndActivate();

            Assert.True(window.IsVisible);
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.CloseForShutdown();
        });
    }

    [Fact]
    public void CloseForShutdown_ClosesWindow_AndClearsNativeWindowSource()
    {
        RunOnStaThread(() =>
        {
            var window = new PulseMeterWindow();
            window.Show();

            Assert.NotEqual(IntPtr.Zero, ((IPulseMeterWindow)window).Handle);

            window.CloseForShutdown();

            Assert.False(window.IsVisible);
            Assert.Equal(IntPtr.Zero, ((IPulseMeterWindow)window).Handle);
        });
    }

    [Fact]
    public void ShowWithoutActivation_ShowsAndRestoresWithoutBecomingActive()
    {
        RunOnStaThread(() =>
        {
            var owner = new Window();
            var window = new PulseMeterWindow();
            owner.Show();
            owner.Activate();

            window.ShowWithoutActivation();

            Assert.True(window.IsVisible);
            Assert.False(window.IsActive);

            window.WindowState = WindowState.Minimized;
            window.ShowWithoutActivation();

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.False(window.IsActive);

            window.CloseForShutdown();
            owner.Close();
        });
    }

    [Fact]
    public void CodingRunwayNavigation_MovesTheRequestedSectionToTheTopOfTheViewport()
    {
        RunOnStaThread(() =>
        {
            var window = new PulseMeterWindow
            {
                DataContext = new PulseMeterWindowViewModel(new MockCodexUsageService())
            };
            window.Show();
            window.UpdateLayout();

            var scrollViewer = Assert.IsType<ScrollViewer>(window.FindName("ExpandedContentScrollViewer"));
            var runwaySection = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("UsageTrendSection"));
            var runwayButton = Assert.Single(
                FindVisualDescendants<Button>(window),
                button => AutomationProperties.GetName(button) == "Go to coding runway");

            runwayButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            window.UpdateLayout();

            var runwayTop = runwaySection
                .TransformToAncestor(scrollViewer)
                .Transform(new Point())
                .Y;
            Assert.True(scrollViewer.VerticalOffset > 0, "Coding runway navigation did not move the dashboard.");
            Assert.InRange(runwayTop, -1, 1);

            window.CloseForShutdown();
        });
    }

    [Fact]
    public void CollapsingDailyUsage_KeepsDailyUsageSelectedAndAligned()
    {
        RunOnStaThread(() =>
        {
            var viewModel = new PulseMeterWindowViewModel(new MockCodexUsageService());
            viewModel.IsDailyUsageVisible = true;
            var today = new DateOnly(2026, 7, 22);
            viewModel.DailyUsage.ApplyBuckets(
                Enumerable.Range(0, 7)
                    .Select(index => new DailyUsageBucket
                    {
                        StartDate = today.AddDays(-index).ToString("yyyy-MM-dd"),
                        Tokens = (index + 1) * 100_000_000L
                    })
                    .ToArray(),
                today);
            var window = new PulseMeterWindow
            {
                DataContext = viewModel
            };
            window.Show();
            window.UpdateLayout();

            var scrollViewer = Assert.IsType<ScrollViewer>(window.FindName("ExpandedContentScrollViewer"));
            var dailyUsageSection = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("DailyUsageSection"));
            var dailyUsageButton = Assert.Single(
                FindVisualDescendants<Button>(window),
                button => AutomationProperties.GetName(button) == "Go to daily usage");

            dailyUsageButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(NavigationSection.DailyUsage, viewModel.NavigationRail.SelectedSection);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            Assert.True(
                viewModel.NavigationRail.SelectedSection == NavigationSection.DailyUsage,
                $"Daily usage navigation changed to {viewModel.NavigationRail.SelectedSection}; offset={scrollViewer.VerticalOffset:0.#}; scrollable={scrollViewer.ScrollableHeight:0.#}; top={dailyUsageSection.TransformToAncestor(scrollViewer).Transform(new Point()).Y:0.#}.");
            Assert.InRange(
                dailyUsageSection.TransformToAncestor(scrollViewer).Transform(new Point()).Y,
                -1,
                1);

            var collapseButton = Assert.Single(
                FindVisualDescendants<Button>(window),
                button => AutomationProperties.GetName(button) == "Collapse daily usage for the 7-day view");
            viewModel.NavigationRail.SelectSection(NavigationSection.ProjectUsage);
            collapseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            PumpDispatcher(TimeSpan.FromMilliseconds(350));
            window.UpdateLayout();

            Assert.False(viewModel.IsDailyUsageExpanded);
            Assert.Equal(NavigationSection.DailyUsage, viewModel.NavigationRail.SelectedSection);
            Assert.InRange(
                dailyUsageSection.TransformToAncestor(scrollViewer).Transform(new Point()).Y,
                -1,
                1);

            var expandButton = Assert.Single(
                FindVisualDescendants<Button>(window),
                button => AutomationProperties.GetName(button) == "Expand daily usage for the 7-day view");
            viewModel.NavigationRail.SelectSection(NavigationSection.BurnAnalysis);
            expandButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            PumpDispatcher(TimeSpan.FromMilliseconds(350));
            window.UpdateLayout();

            Assert.True(viewModel.IsDailyUsageExpanded);
            Assert.Equal(NavigationSection.DailyUsage, viewModel.NavigationRail.SelectedSection);
            Assert.InRange(
                dailyUsageSection.TransformToAncestor(scrollViewer).Transform(new Point()).Y,
                -1,
                1);

            window.CloseForShutdown();
        });
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
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

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA window lifecycle test did not finish.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
