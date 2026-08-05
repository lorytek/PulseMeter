using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void MenuCheckmarksFollowViewModelChangesAndMenuClicksUpdateTheViewModel()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            TrayIconService? tray = null;
            try
            {
                var window = new ImmediatePulseMeterWindow();
                var usageService = new StubUsageService();
                var viewModel = new PulseMeterWindowViewModel(usageService);
                var shutdownCount = 0;
                tray = new TrayIconService(window, viewModel, () => shutdownCount++);

                var show = FindMenuItem(tray, "Show PulseMeter");
                var hide = FindMenuItem(tray, "Hide PulseMeter");
                var refresh = FindMenuItem(tray, "Refresh");
                var mockMode = FindMenuItem(tray, "Mock Mode");
                var autoShow = FindMenuItem(tray, "Auto-show when monitored app focused");
                var autoHide = FindMenuItem(tray, "Auto-hide when focus leaves");
                var alwaysOnTop = FindMenuItem(tray, "Always on top");
                var exit = FindMenuItem(tray, "Exit");

                var initialMockMode = viewModel.UseMockMode;
                var initialAutoShow = viewModel.AutoShowWhenCodexFocused;
                var initialAutoHide = viewModel.AutoHideWhenFocusLeaves;
                var initialAlwaysOnTop = viewModel.IsAlwaysOnTop;

                Assert.Equal(initialMockMode, IsChecked(mockMode));
                Assert.Equal(initialAutoShow, IsChecked(autoShow));
                Assert.Equal(initialAutoHide, IsChecked(autoHide));
                Assert.Equal(initialAlwaysOnTop, IsChecked(alwaysOnTop));

                viewModel.UseMockMode = !initialMockMode;
                viewModel.AutoShowWhenCodexFocused = !initialAutoShow;
                viewModel.AutoHideWhenFocusLeaves = !initialAutoHide;
                viewModel.IsAlwaysOnTop = !initialAlwaysOnTop;

                Assert.Equal(!initialMockMode, IsChecked(mockMode));
                Assert.Equal(!initialAutoShow, IsChecked(autoShow));
                Assert.Equal(!initialAutoHide, IsChecked(autoHide));
                Assert.Equal(!initialAlwaysOnTop, IsChecked(alwaysOnTop));

                PerformClick(mockMode);
                PerformClick(autoShow);
                PerformClick(autoHide);
                PerformClick(alwaysOnTop);

                Assert.Equal(initialMockMode, viewModel.UseMockMode);
                Assert.Equal(initialAutoShow, viewModel.AutoShowWhenCodexFocused);
                Assert.Equal(initialAutoHide, viewModel.AutoHideWhenFocusLeaves);
                Assert.Equal(initialAlwaysOnTop, viewModel.IsAlwaysOnTop);
                Assert.True(window.InvokeCount >= 8);

                viewModel.MarkHiddenByUser();
                PerformClick(show);
                Assert.False(viewModel.IsHiddenByUser);
                Assert.Equal(1, window.ShowAndActivateCount);

                PerformClick(hide);
                Assert.True(viewModel.IsHiddenByUser);
                Assert.Equal(1, window.HideCount);

                var snapshotCallsBeforeRefresh = usageService.GetSnapshotCallCount;
                PerformClick(refresh);
                Assert.True(SpinWait.SpinUntil(
                    () => usageService.GetSnapshotCallCount > snapshotCallsBeforeRefresh,
                    TimeSpan.FromSeconds(2)));

                tray.ShowNotification("Usage warning", "Testing the live tray notification path.");
                PerformClick(exit);
                Assert.Equal(1, window.CloseForShutdownCount);
                Assert.Equal(1, shutdownCount);
                tray.ShowNotification("Ignored", "Disposed tray notifications must be ignored.");
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                tray?.Dispose();
                tray?.Dispose();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The tray icon synchronization test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    private static object FindMenuItem(TrayIconService tray, string text)
    {
        var menuField = typeof(TrayIconService).GetField("_contextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        var menu = Assert.IsAssignableFrom<object>(menuField?.GetValue(tray));
        var items = Assert.IsAssignableFrom<IEnumerable>(menu.GetType().GetProperty("Items")?.GetValue(menu));

        return Assert.Single(
            items.Cast<object>(),
            item => string.Equals(
                item.GetType().GetProperty("Text")?.GetValue(item) as string,
                text,
                StringComparison.Ordinal));
    }

    private static bool IsChecked(object menuItem) =>
        Assert.IsType<bool>(menuItem.GetType().GetProperty("Checked")?.GetValue(menuItem));

    private static void PerformClick(object menuItem) =>
        menuItem.GetType().GetMethod("PerformClick")!.Invoke(menuItem, null);

    private sealed class ImmediatePulseMeterWindow : IPulseMeterWindow
    {
        public int InvokeCount { get; private set; }

        public int ShowAndActivateCount { get; private set; }

        public int HideCount { get; private set; }

        public int CloseForShutdownCount { get; private set; }

        public IntPtr Handle => IntPtr.Zero;

        public bool IsVisible { get; private set; }

        public System.Windows.WindowState WindowState { get; set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }

        public void Show() => IsVisible = true;

        public void ShowWithoutActivation() => IsVisible = true;

        public void ShowAndActivate()
        {
            ShowAndActivateCount++;
            IsVisible = true;
        }

        public void Hide()
        {
            HideCount++;
            IsVisible = false;
        }

        public void CloseForShutdown()
        {
            CloseForShutdownCount++;
            IsVisible = false;
        }

        public bool Activate() => true;
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public bool UseMockMode { get; set; }

        public int GetSnapshotCallCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            GetSnapshotCallCount++;
            return Task.FromResult(new UsageSnapshot());
        }
    }
}
