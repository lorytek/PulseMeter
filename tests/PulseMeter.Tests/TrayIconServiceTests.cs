using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.SupportSnapshot.UI;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

[Collection(UsageTrendWpfCollection.Name)]
public sealed class TrayIconServiceTests
{
    [Fact]
    public void ConfidenceBeacon_KeepsNormalAppIconWhileUpdatingShortTooltips()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            TrayIconService? tray = null;
            try
            {
                var window = new ImmediatePulseMeterWindow();
                var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
                tray = new TrayIconService(
                    window,
                    viewModel,
                    () => { },
                    new CountingQuickAccessController(),
                    new CountingSupportSnapshotPresenter());

                var appIcon = TrayIcon(tray);
                Assert.InRange(TrayText(tray).Length, 1, 63);

                var live = new UsageSnapshot
                {
                    SyncStatus = SyncStatus.Live,
                    LastUpdatedUtc = DateTimeOffset.UtcNow
                };
                viewModel.ApplySnapshot(live);
                Assert.Same(appIcon, TrayIcon(tray));
                Assert.InRange(TrayText(tray).Length, 1, 63);

                viewModel.ApplySnapshot(live);
                viewModel.RefreshClock();
                Assert.Same(appIcon, TrayIcon(tray));
                Assert.InRange(TrayText(tray).Length, 1, 63);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                tray?.Dispose();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TestTimeouts.UiThread), "The tray beacon deduplication test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void LiveWeeklyUsage_UpdatesTooltipButKeepsNormalAppIcon()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            TrayIconService? tray = null;
            try
            {
                var window = new ImmediatePulseMeterWindow();
                var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
                tray = new TrayIconService(
                    window,
                    viewModel,
                    () => { },
                    new CountingQuickAccessController(),
                    new CountingSupportSnapshotPresenter());

                var appIcon = TrayIcon(tray);
                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 28));
                Assert.Equal("PulseMeter — Weekly 72% left", TrayText(tray));
                Assert.Same(appIcon, TrayIcon(tray));

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 28.4));
                Assert.Equal("PulseMeter — Weekly 72% left", TrayText(tray));
                Assert.Same(appIcon, TrayIcon(tray));

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 29));
                Assert.Equal("PulseMeter — Weekly 71% left", TrayText(tray));
                Assert.Same(appIcon, TrayIcon(tray));

                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 29, SyncStatus.Stale));
                Assert.Equal("PulseMeter — Stale", TrayText(tray));
                Assert.Same(appIcon, TrayIcon(tray));

                tray.Dispose();
                viewModel.ApplySnapshot(LiveWeeklySnapshot(usedPercent: 30));
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                tray?.Dispose();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TestTimeouts.UiThread), "The tray percentage icon test did not finish.");
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

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
                var quickAccess = new CountingQuickAccessController();
                var supportSnapshot = new CountingSupportSnapshotPresenter();
                var desktopProcessSnapshot = new CountingDesktopProcessSnapshotPresenter();
                var shutdownCount = 0;
                tray = new TrayIconService(
                    window,
                    viewModel,
                    () => shutdownCount++,
                    quickAccess,
                    supportSnapshot,
                    desktopProcessSnapshot);
                Assert.Equal("PulseMeter — Starting", TrayText(tray));
                viewModel.ApplySnapshot(new UsageSnapshot
                {
                    SyncStatus = SyncStatus.Live,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                    Source = "C:\\private\\customer api-key=poison",
                    StatusMessage = "customer secret"
                });
                Assert.Equal("PulseMeter — Live", TrayText(tray));
                viewModel.ApplySnapshot(new UsageSnapshot
                {
                    SyncStatus = SyncStatus.Unavailable,
                    Source = "poison",
                    StatusMessage = "api-key=secret"
                });
                Assert.Equal("PulseMeter — Unavailable", TrayText(tray));
                viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Mocked, Source = "poison" });
                Assert.Equal("PulseMeter — Mock", TrayText(tray));

                var show = FindMenuItem(tray, "Show PulseMeter");
                var hide = FindMenuItem(tray, "Hide PulseMeter");
                var quickAccessNow = FindMenuItem(tray, "Quick access PulseMeter");
                var refresh = FindMenuItem(tray, "Refresh");
                var supportSnapshotItem = FindMenuItem(tray, "Support snapshot…");
                var desktopProcessSnapshotItem = FindMenuItem(tray, "Desktop process snapshot…");
                var mockMode = FindMenuItem(tray, "Mock Mode");
                var autoShow = FindMenuItem(tray, "Auto-show when monitored app focused");
                var autoHide = FindMenuItem(tray, "Auto-hide when focus leaves");
                var alwaysOnTop = FindMenuItem(tray, "Always on top");
                var quickAccessHotkey = FindMenuItem(tray, "System-wide quick access shortcut (Ctrl+Alt+Shift+P)");
                var exit = FindMenuItem(tray, "Exit");

                var initialMockMode = viewModel.UseMockMode;
                var initialAutoShow = viewModel.AutoShowWhenCodexFocused;
                var initialAutoHide = viewModel.AutoHideWhenFocusLeaves;
                var initialAlwaysOnTop = viewModel.IsAlwaysOnTop;

                Assert.Equal(initialMockMode, IsChecked(mockMode));
                Assert.Equal(initialAutoShow, IsChecked(autoShow));
                Assert.Equal(initialAutoHide, IsChecked(autoHide));
                Assert.Equal(initialAlwaysOnTop, IsChecked(alwaysOnTop));
                Assert.False(IsChecked(quickAccessHotkey));

                viewModel.UseMockMode = !initialMockMode;
                viewModel.AutoShowWhenCodexFocused = !initialAutoShow;
                viewModel.AutoHideWhenFocusLeaves = !initialAutoHide;
                viewModel.IsAlwaysOnTop = !initialAlwaysOnTop;

                Assert.Equal(!initialMockMode, IsChecked(mockMode));
                Assert.Equal(!initialAutoShow, IsChecked(autoShow));
                Assert.Equal(!initialAutoHide, IsChecked(autoHide));
                Assert.Equal(!initialAlwaysOnTop, IsChecked(alwaysOnTop));

                PerformClick(quickAccessNow);
                Assert.Equal(1, quickAccess.ToggleCount);

                PerformClick(quickAccessHotkey);
                Assert.True(viewModel.IsQuickAccessHotkeyRequested);
                Assert.True(IsChecked(quickAccessHotkey));

                var snapshotCallsBeforePreview = usageService.GetSnapshotCallCount;
                PerformClick(supportSnapshotItem);
                Assert.Equal(1, supportSnapshot.ShowCount);
                Assert.Equal(snapshotCallsBeforePreview, usageService.GetSnapshotCallCount);

                PerformClick(desktopProcessSnapshotItem);
                Assert.Equal(1, desktopProcessSnapshot.ShowCount);
                Assert.Equal(snapshotCallsBeforePreview, usageService.GetSnapshotCallCount);

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

        Assert.True(thread.Join(TestTimeouts.UiThread), "The tray icon synchronization test did not finish.");
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

    private static string TrayText(TrayIconService tray)
    {
        var notifyField = typeof(TrayIconService).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        var notify = Assert.IsAssignableFrom<object>(notifyField?.GetValue(tray));
        return Assert.IsType<string>(notify.GetType().GetProperty("Text")?.GetValue(notify));
    }

    private static object TrayIcon(TrayIconService tray)
    {
        var notifyField = typeof(TrayIconService).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        var notify = Assert.IsAssignableFrom<object>(notifyField?.GetValue(tray));
        return Assert.IsAssignableFrom<object>(notify.GetType().GetProperty("Icon")?.GetValue(notify));
    }

    private static UsageSnapshot LiveWeeklySnapshot(
        double usedPercent,
        SyncStatus syncStatus = SyncStatus.Live) =>
        new()
        {
            SyncStatus = syncStatus,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    LimitName = "General",
                    WindowDurationMins = 10_080,
                    WindowLabel = "7d",
                    UsedPercent = usedPercent
                }
            ]
        };

    private sealed class CountingQuickAccessController : IQuickAccessWindowController
    {
        public int ToggleCount { get; private set; }

        public void ToggleQuickAccess() => ToggleCount++;
    }

    private sealed class CountingSupportSnapshotPresenter : ISupportSnapshotPresenter
    {
        public int ShowCount { get; private set; }

        public void ShowPreview() => ShowCount++;
    }

    private sealed class CountingDesktopProcessSnapshotPresenter : ICodexDesktopProcessSnapshotPresenter
    {
        public int ShowCount { get; private set; }

        public void ShowSnapshot(bool measureOnOpen = false)
        {
            Assert.False(measureOnOpen);
            ShowCount++;
        }
    }
}
