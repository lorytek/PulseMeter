using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageSignals.Business;
using System.Windows;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowLifecycleCoordinatorTests
{
    [Fact]
    public async Task ShowAndActivate_ReopensAWindowHiddenByTheUser()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var window = new StubPulseMeterWindow();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            window,
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());
        await coordinator.StartAsync();
        viewModel.MarkHiddenByUser();
        window.Hide();

        coordinator.ShowAndActivate();

        Assert.False(viewModel.IsHiddenByUser);
        Assert.True(window.IsVisible);
        Assert.Equal(1, window.ShowAndActivateCount);
    }

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

        foreground.State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: true);
        viewModel.MarkHiddenByUser();
        timerFactory.Timers[2].RaiseTick();
        Assert.Equal(1, window.ShowCount);

        viewModel.MarkShownByUser();
        window.IsVisible = false;
        timerFactory.Timers[2].RaiseTick();
        Assert.True(window.IsVisible);
    }

    [Fact]
    public async Task ForegroundTick_CollapsesExpandedWindowWhenCodexIsOnSameMonitor()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var window = new StubPulseMeterWindow();
        var foreground = new StubForegroundWindowService
        {
            State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: true)
        };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            window,
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.ToggleExpanded();

        timerFactory.Timers[2].RaiseTick();

        Assert.False(viewModel.IsExpanded);
        Assert.True(window.IsVisible);
    }

    [Fact]
    public async Task ForegroundTick_KeepsExpandedWindowWhenCodexIsOnDifferentMonitor()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var foreground = new StubForegroundWindowService
        {
            State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false)
        };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.ToggleExpanded();

        timerFactory.Timers[2].RaiseTick();

        Assert.True(viewModel.IsExpanded);
    }

    [Fact]
    public async Task Stop_SavesStateStopsTimersAndDisposesTray()
    {
        var usageService = new StubUsageService();
        var usageSignalsTracker = new RecordingUsageSignalsTracker();
        var viewModel = new PulseMeterWindowViewModel(
            usageService,
            TimeSpan.FromSeconds(90),
            usageSignalsTracker: usageSignalsTracker);
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
        viewModel.NavigationRail.IsProjectUsageVisible = false;
        viewModel.NavigationRail.IsUsageAttributionVisible = false;
        coordinator.Stop();

        Assert.True(tray.IsDisposed);
        Assert.NotNull(settingsStore.Saved);
        Assert.NotNull(settingsStore.Saved!.DashboardVisibility);
        Assert.False(settingsStore.Saved.DashboardVisibility!.ProjectUsage);
        Assert.False(settingsStore.Saved.DashboardVisibility.BurnAnalysis);
        Assert.NotNull(windowStateStore.Saved);
        Assert.All(timerFactory.Timers, timer => Assert.False(timer.Started));
        Assert.Equal(1, usageSignalsTracker.FlushCount);
    }

    [Fact]
    public async Task AppSettingsSaveFailure_IsRetriedOnTheNextClockTickWithoutThrowingFromTheUiEvent()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var settingsStore = new StubAppSettingsStore { SaveResult = false };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.IsAlwaysOnTop = true;

        Assert.Equal(1, settingsStore.SaveCount);
        Assert.Null(settingsStore.Saved);

        settingsStore.SaveResult = true;
        timerFactory.Timers[0].RaiseTick();

        Assert.Equal(2, settingsStore.SaveCount);
        Assert.True(settingsStore.Saved?.IsAlwaysOnTop);
    }

    [Fact]
    public async Task Stop_SurfacesFinalPersistenceFailureAfterCleaningUp()
    {
        var usageService = new StubUsageService();
        var timerFactory = new StubPulseMeterTimerFactory();
        var tray = new StubTrayIconService();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            new StubPulseMeterWindow(),
            tray,
            new StubForegroundWindowService(),
            new StubAppSettingsStore { SaveResult = false },
            new StubWindowStateStore { SaveResult = false },
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();

        var exception = Assert.Throws<IOException>(coordinator.Stop);

        Assert.Equal("PulseMeter could not persist app settings during shutdown.", exception.Message);
        Assert.All(timerFactory.Timers, timer => Assert.False(timer.Started));
        Assert.True(tray.IsDisposed);
        Assert.Equal(0, usageService.SnapshotSubscriberCount);
    }

    [Fact]
    public async Task Stop_QueuedTimerTicksDoNotRefreshOrCheckForeground()
    {
        var usageService = new StubUsageService();
        var foreground = new StubForegroundWindowService();
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        coordinator.Stop();
        var snapshotCallsAfterStop = usageService.GetSnapshotCallCount;
        var foregroundCallsAfterStop = foreground.GetForegroundStateCallCount;

        timerFactory.Timers[0].RaiseTick();
        timerFactory.Timers[1].RaiseTick();
        timerFactory.Timers[2].RaiseTick();

        Assert.Equal(snapshotCallsAfterStop, usageService.GetSnapshotCallCount);
        Assert.Equal(foregroundCallsAfterStop, foreground.GetForegroundStateCallCount);
    }

    [Fact]
    public async Task Stop_CancelsInFlightAutomaticRefreshWithoutShowingFailure()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        usageService.SnapshotFactory = async cancellationToken =>
        {
            refreshStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult();
                }
            }

            return new UsageSnapshot();
        };

        timerFactory.Timers[1].RaiseTick();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Stop();
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var completionDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (viewModel.IsRefreshing && DateTimeOffset.UtcNow < completionDeadline)
        {
            await Task.Delay(10);
        }

        Assert.False(viewModel.IsRefreshing);
        Assert.NotEqual("Sync failed. Try again.", viewModel.SyncFeedbackText);
        Assert.False(viewModel.HasActionableSyncIssue);
    }

    [Fact]
    public async Task Stop_CompletesTeardownWhenFlushAndPersistenceFail()
    {
        var usageService = new StubUsageService();
        var timerFactory = new StubPulseMeterTimerFactory();
        var tray = new StubTrayIconService();
        var settingsStore = new StubAppSettingsStore { SaveException = new InvalidOperationException("settings failed") };
        var windowStateStore = new StubWindowStateStore { SaveException = new InvalidOperationException("window state failed") };
        var viewModel = new PulseMeterWindowViewModel(
            usageService,
            usageSignalsTracker: new ThrowingUsageSignalsTracker());
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

        var exception = Assert.Throws<InvalidOperationException>(coordinator.Stop);

        Assert.Equal("usage history failed", exception.Message);
        Assert.All(timerFactory.Timers, timer => Assert.False(timer.Started));
        Assert.True(tray.IsDisposed);
        Assert.Equal(1, settingsStore.SaveCount);
        Assert.Equal(1, windowStateStore.SaveCount);
        Assert.Equal(0, usageService.SnapshotSubscriberCount);
    }

    [Fact]
    public async Task StartAsync_FailureRollsBackPartialStartupAndPermitsRetry()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var usageService = new StubUsageService { StartException = startupFailure };
        var window = new StubPulseMeterWindow();
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            window,
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync());

        Assert.Same(startupFailure, exception);
        Assert.False(window.IsVisible);
        Assert.All(timerFactory.Timers, timer => Assert.False(timer.Started));
        Assert.Equal(0, usageService.SnapshotSubscriberCount);

        usageService.StartException = null;
        await coordinator.StartAsync();

        Assert.Equal(2, usageService.StartCallCount);
        Assert.Equal(6, timerFactory.Timers.Count);
        Assert.All(timerFactory.Timers.Skip(3), timer => Assert.True(timer.Started));
    }

    [Fact]
    public async Task ForegroundTick_RestoresMinimizedVisibleWindowWhenCodexReturns()
    {
        var usageService = new StubUsageService();
        var window = new StubPulseMeterWindow
        {
            IsVisible = true,
            WindowState = WindowState.Minimized
        };
        var foreground = new StubForegroundWindowService
        {
            State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false)
        };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            window,
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        window.WindowState = WindowState.Minimized;
        timerFactory.Timers[2].RaiseTick();

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(1, window.ShowWithoutActivationCount);
        Assert.Equal(0, window.ShowAndActivateCount);
    }

    [Fact]
    public async Task ForegroundTransitions_RestoreWithoutActivationEachTimeCodexReturns()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService)
        {
            AutoHideWhenFocusLeaves = true
        };
        var window = new StubPulseMeterWindow();
        var foreground = new StubForegroundWindowService();
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            window,
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        window.Hide();
        foreground.State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false);

        timerFactory.Timers[2].RaiseTick();

        foreground.State = new CodexForegroundState(IsCodexForeground: false, IsOnSameMonitor: false);
        timerFactory.Timers[2].RaiseTick();

        foreground.State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false);
        timerFactory.Timers[2].RaiseTick();

        Assert.Equal(2, window.ShowWithoutActivationCount);
        Assert.Equal(0, window.ShowAndActivateCount);
        Assert.True(window.IsVisible);
    }

    [Fact]
    public async Task StartAsync_MarshalsInitialRefreshAfterAsynchronousServiceStartup()
    {
        var startupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var usageService = new StubUsageService { StartTask = startupCompletion.Task };
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var dispatcher = new RecordingUiDispatcher();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            dispatcher);

        var startup = coordinator.StartAsync();
        Assert.False(startup.IsCompleted);

        startupCompletion.SetResult();
        await startup;

        Assert.Equal(1, dispatcher.InvokeCount);
        Assert.Equal(1, usageService.GetSnapshotCallCount);
    }

    [Fact]
    public async Task ForegroundTick_DoesNotActivateAlreadyVisibleNormalWindow()
    {
        var usageService = new StubUsageService();
        var window = new StubPulseMeterWindow
        {
            IsVisible = true,
            WindowState = WindowState.Normal
        };
        var foreground = new StubForegroundWindowService
        {
            State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false)
        };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            window,
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        timerFactory.Timers[2].RaiseTick();

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(0, window.ShowAndActivateCount);
    }

    [Fact]
    public async Task ForegroundTimerFailure_DoesNotEscapeTheDispatcher_AndTheNextTickRecovers()
    {
        var usageService = new StubUsageService();
        var window = new StubPulseMeterWindow();
        var foreground = new StubForegroundWindowService
        {
            Failure = new InvalidOperationException("foreground probe failed")
        };
        var timerFactory = new StubPulseMeterTimerFactory();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            new PulseMeterWindowViewModel(usageService),
            window,
            new StubTrayIconService(),
            foreground,
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            timerFactory,
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();

        var failure = Record.Exception(() => timerFactory.Timers[2].RaiseTick());

        Assert.Null(failure);
        Assert.Equal(1, foreground.GetForegroundStateCallCount);

        foreground.Failure = null;
        foreground.State = new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false);
        window.Hide();
        timerFactory.Timers[2].RaiseTick();

        Assert.Equal(2, foreground.GetForegroundStateCallCount);
        Assert.True(window.IsVisible);
    }

    [Fact]
    public async Task SelectedRateLimitTrack_IsSavedOnlyWhenItsStableKeyChanges()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var settingsStore = new StubAppSettingsStore();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());
        var snapshot = new UsageSnapshot
        {
            Buckets =
            [
                new RateLimitBucket { LimitId = "codex", LimitName = "General", GroupLabel = "General", WindowLabel = "5h" },
                new RateLimitBucket { LimitId = "codex_bengalfox", LimitName = "GPT-5.3-Spark", GroupLabel = "GPT-5.3-Spark", WindowLabel = "5h" }
            ]
        };

        await coordinator.StartAsync();
        usageService.RaiseSnapshot(snapshot);
        var savesAfterInitialSelection = settingsStore.SaveCount;

        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.Key == "codex_bengalfox");

        Assert.Equal(savesAfterInitialSelection + 1, settingsStore.SaveCount);
        Assert.Equal("codex_bengalfox", settingsStore.Saved?.SelectedRateLimitKey);

        usageService.RaiseSnapshot(snapshot);

        Assert.Equal(savesAfterInitialSelection + 1, settingsStore.SaveCount);
    }

    [Fact]
    public async Task AlwaysOnTopChange_IsSavedThroughTheSettingsLifecycle()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var settingsStore = new StubAppSettingsStore();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.IsAlwaysOnTop = true;

        Assert.True(settingsStore.Saved?.IsAlwaysOnTop);
    }

    [Fact]
    public async Task FocusAutomationChanges_AreSavedThroughTheSettingsLifecycle()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var settingsStore = new StubAppSettingsStore();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.AutoShowWhenCodexFocused = false;
        viewModel.AutoHideWhenFocusLeaves = true;

        Assert.False(settingsStore.Saved?.AutoShowWhenCodexFocused);
        Assert.True(settingsStore.Saved?.AutoHideWhenFocusLeaves);
    }

    [Fact]
    public async Task NavigationPanelStateChange_IsSavedThroughTheSettingsLifecycle()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var settingsStore = new StubAppSettingsStore();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        viewModel.NavigationRail.ToggleNavigationPanel();

        Assert.False(viewModel.IsNavigationPanelExpanded);
        Assert.Equal(false, settingsStore.Saved?.IsNavigationPanelExpanded);
    }

    [Fact]
    public async Task Stop_DispatchesTeardownAndLeavesAsyncUsageDisposalToTheProvider()
    {
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService, TimeSpan.FromSeconds(90));
        var dispatcher = new RecordingUiDispatcher();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            new StubTrayIconService(),
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            dispatcher);

        await coordinator.StartAsync();
        var dispatcherCallsBeforeStop = dispatcher.InvokeCount;
        coordinator.Stop();

        Assert.Equal(dispatcherCallsBeforeStop + 1, dispatcher.InvokeCount);
        Assert.Equal(0, usageService.DisposeAsyncCount);
    }

    [Fact]
    public async Task SnapshotUpdated_DoesNotShowTrayNotificationForAutomaticBudgetSignal()
    {
        var now = DateTimeOffset.Now;
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(
            usageService,
            TimeSpan.FromSeconds(90));
        var tray = new StubTrayIconService();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            tray,
            new StubForegroundWindowService(),
            new StubAppSettingsStore(),
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            new ImmediateUiDispatcher());

        await coordinator.StartAsync();
        usageService.RaiseSnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Source = "AppServer",
            LastUpdatedUtc = now,
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    Label = "5h Window",
                    WindowLabel = "5h",
                    WindowDurationMins = 300,
                    UsedPercent = 96,
                    ResetsAtUtc = now.AddHours(2),
                    ResetsAtUnixSeconds = now.AddHours(2).ToUnixTimeSeconds()
                }
            ]
        });

        Assert.Empty(tray.Notifications);
        Assert.Contains(viewModel.NeedsAttention.NeedsAttentionItems, item => item.Title == "5h budget is critical");
    }

    [Fact]
    public async Task RecoveryWatchChange_IsPersistedAndCompletionForwardsOneTrayNotification()
    {
        var now = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(3);
        var usageService = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(usageService);
        var tray = new StubTrayIconService();
        var settingsStore = new StubAppSettingsStore();
        var dispatcher = new RecordingUiDispatcher();
        var coordinator = new PulseMeterWindowLifecycleCoordinator(
            usageService,
            viewModel,
            new StubPulseMeterWindow(),
            tray,
            new StubForegroundWindowService(),
            settingsStore,
            new StubWindowStateStore(),
            new StubPulseMeterTimerFactory(),
            dispatcher);
        var trend = new LimitUsageTrend(
            "codex|300", "codex", "General", "5h", 300, reset,
            [new LimitUsagePoint(now.AddMinutes(-20), 70), new LimitUsagePoint(now, 86)],
            IsMock: false);
        var atRisk = new LimitRunwayForecast(
            "codex|300", "codex", "General", "5h", 300, reset, 86,
            LimitRunwayForecastState.AtRisk, now.AddMinutes(45), 0, 5,
            TimeSpan.FromMinutes(40), IsActionable: true, IsMock: false,
            Confidence: LimitRunwayForecastConfidence.Medium, SampleCount: 5)
        {
            EarliestExhaustsAtUtc = now.AddMinutes(30),
            LatestExhaustsAtUtc = now.AddMinutes(45)
        };

        await coordinator.StartAsync();
        var dispatcherCallsBeforeRecovery = dispatcher.InvokeCount;
        viewModel.UsageTrend.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [trend],
            RunwayForecasts = [atRisk]
        }, "codex", now);
        viewModel.UsageTrend.SelectedBlockDurationMinutes = 60;
        viewModel.UsageTrend.ToggleRecoveryWatchCommand.Execute(null);

        Assert.Single(settingsStore.Saved?.RecoveryWatches ?? []);

        viewModel.UsageTrend.ApplySignals(new UsageSignalsSnapshot
        {
            UsageTrends = [trend],
            RunwayForecasts = [atRisk with
            {
                State = LimitRunwayForecastState.OnTrack,
                ExhaustsAtUtc = null,
                EarliestExhaustsAtUtc = null,
                LatestExhaustsAtUtc = null
            }]
        }, "codex", now.AddMinutes(5));
        viewModel.UsageTrend.Refresh(now.AddMinutes(10));

        Assert.Single(tray.RecoveryNotifications);
        Assert.Equal("Ready to code again", tray.RecoveryNotifications[0].Title);
        Assert.Equal(dispatcherCallsBeforeRecovery + 1, dispatcher.InvokeCount);
        coordinator.Stop();
        Assert.Empty(settingsStore.Saved?.RecoveryWatches ?? []);
    }

    private sealed class StubUsageService : IUsageService, IAsyncDisposable
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public int StartCallCount { get; private set; }

        public int GetSnapshotCallCount { get; private set; }

        public int DisposeAsyncCount { get; private set; }

        public Exception? StartException { get; set; }

        public Task? StartTask { get; set; }

        public Func<CancellationToken, Task<UsageSnapshot>>? SnapshotFactory { get; set; }

        public int SnapshotSubscriberCount => SnapshotUpdated?.GetInvocationList().Length ?? 0;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            if (StartException is not null)
            {
                return Task.FromException(StartException);
            }

            return StartTask ?? Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            GetSnapshotCallCount++;
            if (SnapshotFactory is not null)
            {
                return SnapshotFactory(cancellationToken);
            }

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

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingUsageSignalsTracker : IUsageSignalsTracker
    {
        public int FlushCount { get; private set; }

        public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
        {
            return UsageSignalsSnapshot.Empty;
        }

        public void DismissIdleDrain()
        {
        }

        public void Flush()
        {
            FlushCount++;
        }
    }

    private sealed class ThrowingUsageSignalsTracker : IUsageSignalsTracker
    {
        public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
        {
            return UsageSignalsSnapshot.Empty;
        }

        public void DismissIdleDrain()
        {
        }

        public void Flush()
        {
            throw new InvalidOperationException("usage history failed");
        }
    }

    private sealed class StubPulseMeterWindow : IPulseMeterWindow
    {
        public IntPtr Handle { get; } = new(123);

        public bool IsVisible { get; set; }

        public WindowState WindowState { get; set; }

        public bool ShowCalled { get; private set; }

        public int ShowCount { get; private set; }

        public int ShowAndActivateCount { get; private set; }

        public int ShowWithoutActivationCount { get; private set; }

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

        public void ShowAndActivate()
        {
            ShowAndActivateCount++;
            if (!IsVisible)
            {
                Show();
            }

            WindowState = WindowState.Normal;
        }

        public void ShowWithoutActivation()
        {
            ShowWithoutActivationCount++;
            if (!IsVisible)
            {
                Show();
            }

            WindowState = WindowState.Normal;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void CloseForShutdown()
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
        public List<(string Title, string Message, BudgetAlertLevel Level)> Notifications { get; } = [];

        public List<(string Title, string Message)> RecoveryNotifications { get; } = [];

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void ShowNotification(string title, string message)
        {
            RecoveryNotifications.Add((title, message));
        }
    }

    private sealed class StubForegroundWindowService : IForegroundWindowService
    {
        public CodexForegroundState State { get; set; }

        public Exception? Failure { get; set; }

        public int GetForegroundStateCallCount { get; private set; }

        public CodexForegroundState GetCodexForegroundState(IntPtr referenceWindowHandle)
        {
            GetForegroundStateCallCount++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return State;
        }
    }

    private sealed class StubAppSettingsStore : IPulseMeterAppSettingsStore
    {
        public PulseMeterAppSettings? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public Exception? SaveException { get; set; }

        public PulseMeterAppSettings? Load()
        {
            return null;
        }

        public bool Save(PulseMeterAppSettings settings)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            if (!SaveResult)
            {
                return false;
            }

            Saved = settings;
            return true;
        }

        public bool SaveResult { get; set; } = true;
    }

    private sealed class StubWindowStateStore : IPulseMeterWindowStateStore
    {
        public PulseMeterWindowState? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public Exception? SaveException { get; set; }

        public PulseMeterWindowState? Load()
        {
            return null;
        }

        public bool Save(PulseMeterWindowState state)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            Saved = state;
            return SaveResult;
        }

        public bool SaveResult { get; set; } = true;
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

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }
    }
}
