using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Slices.UsageCollection;
using System.Windows;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowLifecycleCoordinatorTests
{
    [Fact]
    public async Task StartAsync_WiresSnapshotRefreshTimersAndForegroundVisibility()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var window = new StubPulseMeterWindow();
        var settingsStore = new StubAppSettingsStore();
        var windowStateStore = new StubWindowStateStore();
        var timerFactory = new StubPulseMeterTimerFactory();
        var foreground = new StubForegroundWindowService();
        var tray = new StubTrayIconService();
        var dispatcher = new ImmediateUiDispatcher();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            window,
            tray,
            foreground,
            settingsStore,
            windowStateStore,
            timerFactory,
            dispatcher);

        await coordinator.StartAsync();

        Assert.True(window.ShowCalled);
        Assert.Equal(1, usageService.StartCallCount);
        Assert.Equal(1, usageService.GetSnapshotCallCount);
        Assert.Equal(3, timerFactory.Timers.Count);
        Assert.All(timerFactory.Timers, timer => Assert.True(timer.Started));

        usageService.RaiseSnapshot(new UsageSnapshot { Source = "AppServer", SyncStatus = SyncStatus.Live });
        Assert.Equal("Source: Live source", viewModel.SourceText);

        foreground.IsCodexForegroundResult = true;
        viewModel.MarkHiddenByUser();
        timerFactory.Timers[2].RaiseTick();
        Assert.Equal(1, window.ShowCount);

        viewModel.MarkShownByUser();
        window.IsVisible = false;
        timerFactory.Timers[2].RaiseTick();
        Assert.True(window.IsVisible);
    }

    [Fact]
    public async Task Stop_SavesStateStopsTimersAndDisposesTray()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var tray = new StubTrayIconService();
        var settingsStore = new StubAppSettingsStore();
        var windowStateStore = new StubWindowStateStore();
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            tray,
            new StubForegroundWindowService(),
            settingsStore,
            windowStateStore,
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        coordinator.Stop();

        Assert.True(tray.IsDisposed);
        Assert.NotNull(settingsStore.Saved);
        Assert.NotNull(windowStateStore.Saved);
        Assert.All(timerFactory.Timers, timer => Assert.False(timer.Started));
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public int StartCallCount { get; private set; }

        public int GetSnapshotCallCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            GetSnapshotCallCount++;
            return Task.FromResult(new UsageSnapshot
            {
                Source = "AppServer",
                SyncStatus = SyncStatus.Live,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            });
        }

        public void RaiseSnapshot(UsageSnapshot snapshot)
        {
            SnapshotUpdated?.Invoke(this, snapshot);
        }
    }

    private sealed class StubPulseMeterWindow : IPulseMeterWindow
    {
        public bool IsVisible { get; set; }

        public WindowState WindowState { get; set; }

        public bool ShowCalled { get; private set; }

        public int ShowCount { get; private set; }

        public void Invoke(Action action)
        {
            action();
        }

        public void Show()
        {
            IsVisible = true;
            ShowCalled = true;
            ShowCount++;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public bool Activate()
        {
            return true;
        }
    }

    private sealed class StubTrayIconService : ITrayIconService
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class StubForegroundWindowService : IForegroundWindowService
    {
        public bool IsCodexForegroundResult { get; set; }

        public bool IsCodexForeground()
        {
            return IsCodexForegroundResult;
        }
    }

    private sealed class StubAppSettingsStore : IPulseMeterAppSettingsStore
    {
        public PulseMeterAppSettings? Saved { get; private set; }

        public PulseMeterAppSettings? Load()
        {
            return null;
        }

        public void Save(PulseMeterAppSettings settings)
        {
            Saved = settings;
        }
    }

    private sealed class StubWindowStateStore : IPulseMeterWindowStateStore
    {
        public PulseMeterWindowState? Saved { get; private set; }

        public PulseMeterWindowState? Load()
        {
            return null;
        }

        public void Save(PulseMeterWindowState state)
        {
            Saved = state;
        }
    }

    private sealed class StubPulseMeterTimerFactory : IPulseMeterTimerFactory
    {
        public List<StubPulseMeterTimer> Timers { get; } = [];

        public IPulseMeterTimer Create(TimeSpan interval)
        {
            var timer = new StubPulseMeterTimer { Interval = interval };
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class StubPulseMeterTimer : IPulseMeterTimer
    {
        public event EventHandler? Tick;

        public TimeSpan Interval { get; set; }

        public bool Started { get; private set; }

        public void Start()
        {
            Started = true;
        }

        public void Stop()
        {
            Started = false;
        }

        public void RaiseTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public void Invoke(Action action)
        {
            action();
        }
    }
}
