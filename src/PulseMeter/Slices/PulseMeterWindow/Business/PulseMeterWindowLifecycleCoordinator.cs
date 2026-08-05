using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageTrend.UI;

using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

public interface IPulseMeterWindowLifecycleCoordinator
{
    Task StartAsync(CancellationToken cancellationToken = default);

    void ShowAndActivate();

    void Stop();
}

public sealed class PulseMeterWindowLifecycleCoordinator : IPulseMeterWindowLifecycleCoordinator
{
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ForegroundInterval = TimeSpan.FromSeconds(1);

    private readonly IUsageService _usageService;
    private readonly PulseMeterWindowViewModel _viewModel;
    private readonly IPulseMeterWindow _pulseMeterWindow;
    private readonly ITrayIconService _trayIconService;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly IPulseMeterAppSettingsStore _appSettingsStore;
    private readonly IPulseMeterWindowStateStore _windowStateStore;
    private readonly IPulseMeterTimerFactory _timerFactory;
    private readonly IUiDispatcher _dispatcher;
    private IPulseMeterTimer? _clockTimer;
    private IPulseMeterTimer? _foregroundTimer;
    private IPulseMeterTimer? _refreshTimer;
    private EventHandler? _clockTimerTickHandler;
    private EventHandler? _foregroundTimerTickHandler;
    private PulseMeterAppSettings? _pendingAppSettings;
    private EventHandler? _refreshTimerTickHandler;
    private CancellationTokenSource? _refreshCancellation;
    private bool _started;
    private bool _stopped;

    public PulseMeterWindowLifecycleCoordinator(
        IUsageService usageService,
        PulseMeterWindowViewModel viewModel,
        IPulseMeterWindow pulseMeterWindow,
        ITrayIconService trayIconService,
        IForegroundWindowService foregroundWindowService,
        IPulseMeterAppSettingsStore appSettingsStore,
        IPulseMeterWindowStateStore windowStateStore,
        IPulseMeterTimerFactory timerFactory,
        IUiDispatcher dispatcher)
    {
        _usageService = usageService;
        _viewModel = viewModel;
        _pulseMeterWindow = pulseMeterWindow;
        _trayIconService = trayIconService;
        _foregroundWindowService = foregroundWindowService;
        _appSettingsStore = appSettingsStore;
        _windowStateStore = windowStateStore;
        _timerFactory = timerFactory;
        _dispatcher = dispatcher;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var refreshCancellation = _refreshCancellation;
        try
        {
            SubscribeEventHandlers();
            _pulseMeterWindow.Show();
            StartTimers();

            await _usageService.StartAsync(refreshCancellation.Token).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() => _viewModel.RefreshAsync(refreshCancellation.Token)).ConfigureAwait(false);
        }
        catch
        {
            RollbackFailedStart();
            throw;
        }
    }

    public void Stop()
    {
        _dispatcher.Invoke(StopCore);
    }

    public void ShowAndActivate()
    {
        if (!IsRunning)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (!IsRunning)
            {
                return;
            }

            _viewModel.MarkShownByUser();
            _pulseMeterWindow.ShowAndActivate();
        });
    }

    private void StopCore()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        Exception? firstFailure = null;

        CaptureFailure(ref firstFailure, CancelRefreshes);
        CaptureFailure(ref firstFailure, StopTimers);
        CaptureFailure(ref firstFailure, UnsubscribeEventHandlers);
        CaptureFailure(ref firstFailure, _viewModel.FlushUsageHistory);
        CaptureFailure(ref firstFailure, PersistAppSettingsForShutdown);
        CaptureFailure(ref firstFailure, PersistWindowStateForShutdown);
        CaptureFailure(ref firstFailure, _trayIconService.Dispose);

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private void StartTimers()
    {
        _clockTimer = _timerFactory.Create(ClockInterval);
        _clockTimerTickHandler = OnClockTimerTick;
        _clockTimer.Tick += _clockTimerTickHandler;
        _clockTimer.Start();

        _refreshTimer = _timerFactory.Create(_viewModel.AutoSyncInterval);
        _refreshTimerTickHandler = OnRefreshTimerTick;
        _refreshTimer.Tick += _refreshTimerTickHandler;
        _refreshTimer.Start();

        _foregroundTimer = _timerFactory.Create(ForegroundInterval);
        _foregroundTimerTickHandler = OnForegroundTimerTick;
        _foregroundTimer.Tick += _foregroundTimerTickHandler;
        _foregroundTimer.Start();
    }

    private void SubscribeEventHandlers()
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.UsageTrend.RecoveryWatchesChanged += OnRecoveryWatchesChanged;
        _viewModel.UsageTrend.RecoveryWatchCompleted += OnRecoveryWatchCompleted;
        _usageService.SnapshotUpdated += OnSnapshotUpdated;
    }

    private void UnsubscribeEventHandlers()
    {
        _usageService.SnapshotUpdated -= OnSnapshotUpdated;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.UsageTrend.RecoveryWatchesChanged -= OnRecoveryWatchesChanged;
        _viewModel.UsageTrend.RecoveryWatchCompleted -= OnRecoveryWatchCompleted;
    }

    private void StopTimers()
    {
        Exception? firstFailure = null;

        CaptureFailure(ref firstFailure, () => StopTimer(_clockTimer, _clockTimerTickHandler));
        CaptureFailure(ref firstFailure, () => StopTimer(_refreshTimer, _refreshTimerTickHandler));
        CaptureFailure(ref firstFailure, () => StopTimer(_foregroundTimer, _foregroundTimerTickHandler));

        _clockTimer = null;
        _refreshTimer = null;
        _foregroundTimer = null;
        _clockTimerTickHandler = null;
        _refreshTimerTickHandler = null;
        _foregroundTimerTickHandler = null;

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private static void StopTimer(IPulseMeterTimer? timer, EventHandler? tickHandler)
    {
        if (timer is null)
        {
            return;
        }

        Exception? firstFailure = null;

        CaptureFailure(ref firstFailure, timer.Stop);
        if (tickHandler is not null)
        {
            CaptureFailure(ref firstFailure, () => timer.Tick -= tickHandler);
        }

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private void RollbackFailedStart()
    {
        _started = false;

        TryRollback(CancelRefreshes);
        TryRollback(StopTimers);
        TryRollback(UnsubscribeEventHandlers);
        TryRollback(_pulseMeterWindow.Hide);
    }

    private static void CaptureFailure(ref Exception? firstFailure, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (firstFailure is null)
        {
            firstFailure = exception;
        }
        catch (Exception)
        {
        }
    }

    private static void TryRollback(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("startup rollback failed", exception);
        }
    }

    private bool IsRunning => _started && !_stopped;

    private void CancelRefreshes()
    {
        _refreshCancellation?.Cancel();
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _viewModel.RefreshClock();
            RetryPendingAppSettings();
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("clock timer update failed", exception);
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        var cancellation = _refreshCancellation;
        if (IsRunning && cancellation is not null)
        {
            _ = RefreshFromTimerAsync(cancellation.Token);
        }
    }

    private async Task RefreshFromTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsRunning)
            {
                await _viewModel.RefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("automatic refresh failed", exception);
        }
    }

    private void OnForegroundTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            UpdateForegroundVisibility();
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("foreground visibility update failed", exception);
        }
    }

    private void OnSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        if (!IsRunning)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (IsRunning)
            {
                _viewModel.ApplySnapshot(snapshot);
            }
        });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PulseMeterWindowViewModel.AutoSyncSeconds)
            or nameof(PulseMeterWindowViewModel.SelectedLimitKey)
            or nameof(PulseMeterWindowViewModel.IsAlwaysOnTop)
            or nameof(PulseMeterWindowViewModel.AutoShowWhenCodexFocused)
            or nameof(PulseMeterWindowViewModel.AutoHideWhenFocusLeaves)
            or nameof(PulseMeterWindowViewModel.IsNavigationPanelExpanded)
            or nameof(PulseMeterWindowViewModel.IsRateLimitsVisible)
            or nameof(PulseMeterWindowViewModel.IsRateLimitsDailyVisible)
            or nameof(PulseMeterWindowViewModel.IsRunwayForecastVisible)
            or nameof(PulseMeterWindowViewModel.IsBlockPlannerVisible)
            or nameof(PulseMeterWindowViewModel.IsResetCreditsVisible)
            or nameof(PulseMeterWindowViewModel.IsAccountUsageVisible)
            or nameof(PulseMeterWindowViewModel.IsProjectUsageVisible)
            or nameof(PulseMeterWindowViewModel.IsUsageAttributionVisible)
            or nameof(PulseMeterWindowViewModel.IsDailyUsageVisible))
        {
            QueueAppSettingsSave();
        }

        if (e.PropertyName is nameof(PulseMeterWindowViewModel.AutoSyncInterval) or nameof(PulseMeterWindowViewModel.AutoSyncSeconds)
            && _refreshTimer is not null)
        {
            _refreshTimer.Interval = _viewModel.AutoSyncInterval;
        }
    }

    private void OnRecoveryWatchesChanged(object? sender, EventArgs e)
    {
        QueueAppSettingsSave();
    }

    private void OnRecoveryWatchCompleted(object? sender, UsageTrendRecoveryCompletedEventArgs e)
    {
        _dispatcher.Invoke(() => _trayIconService.ShowNotification(e.Title, e.Message));
    }

    private void QueueAppSettingsSave()
    {
        _pendingAppSettings = CaptureAppSettings(_viewModel);
        RetryPendingAppSettings();
    }

    private void RetryPendingAppSettings()
    {
        if (_pendingAppSettings is not PulseMeterAppSettings settings)
        {
            return;
        }

        try
        {
            if (_appSettingsStore.Save(settings))
            {
                _pendingAppSettings = null;
            }
            else
            {
                PrivacySafeDiagnostics.WriteInfo("app settings could not be persisted; retrying on next clock tick");
            }
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("app settings persistence failed", exception);
        }
    }

    private void PersistAppSettingsForShutdown()
    {
        var settings = CaptureAppSettings(_viewModel);
        if (!_appSettingsStore.Save(settings))
        {
            PrivacySafeDiagnostics.WriteInfo("app settings could not be persisted during shutdown");
            throw new IOException("PulseMeter could not persist app settings during shutdown.");
        }

        _pendingAppSettings = null;
    }

    private void PersistWindowStateForShutdown()
    {
        if (!_windowStateStore.Save(_viewModel.CaptureWindowState()))
        {
            PrivacySafeDiagnostics.WriteInfo("window state could not be persisted during shutdown");
            throw new IOException("PulseMeter could not persist window state during shutdown.");
        }
    }

    private static PulseMeterAppSettings CaptureAppSettings(PulseMeterWindowViewModel viewModel)
    {
        return new PulseMeterAppSettings(
            viewModel.AutoSyncSeconds,
            viewModel.IsAlwaysOnTop,
            viewModel.NavigationRail.CaptureVisibility(),
            viewModel.SelectedLimitKey,
            viewModel.IsNavigationPanelExpanded,
            viewModel.UsageTrend.CaptureRecoveryWatches(),
            viewModel.AutoShowWhenCodexFocused,
            viewModel.AutoHideWhenFocusLeaves);
    }

    private void UpdateForegroundVisibility()
    {
        if (!IsRunning)
        {
            return;
        }

        if (!_viewModel.AutoShowWhenCodexFocused || _viewModel.IsHiddenByUser)
        {
            return;
        }

        var foregroundState = _foregroundWindowService.GetCodexForegroundState(_pulseMeterWindow.Handle);
        if (foregroundState.IsCodexForeground)
        {
            if (foregroundState.IsOnSameMonitor && _viewModel.IsExpanded)
            {
                _viewModel.Collapse();
            }

            if (!_pulseMeterWindow.IsVisible || _pulseMeterWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _pulseMeterWindow.ShowWithoutActivation();
            }

            return;
        }

        if (_viewModel.AutoHideWhenFocusLeaves && _pulseMeterWindow.IsVisible)
        {
            _pulseMeterWindow.Hide();
        }
    }
}
