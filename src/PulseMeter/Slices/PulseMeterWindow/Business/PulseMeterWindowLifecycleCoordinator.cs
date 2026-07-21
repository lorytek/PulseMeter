using System.ComponentModel;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

public interface IPulseMeterWindowLifecycleCoordinator
{
    Task StartAsync(CancellationToken cancellationToken = default);

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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _usageService.SnapshotUpdated += OnSnapshotUpdated;

        _pulseMeterWindow.Show();
        StartTimers();

        await _usageService.StartAsync(cancellationToken).ConfigureAwait(false);
        await _viewModel.RefreshAsync().ConfigureAwait(false);
    }

    public void Stop()
    {
        _dispatcher.Invoke(StopCore);
    }

    private void StopCore()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _clockTimer?.Stop();
        _foregroundTimer?.Stop();
        _refreshTimer?.Stop();

        _viewModel.FlushUsageHistory();

        _usageService.SnapshotUpdated -= OnSnapshotUpdated;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _appSettingsStore.Save(CaptureAppSettings(_viewModel));
        _windowStateStore.Save(_viewModel.CaptureWindowState());
        _trayIconService.Dispose();
    }

    private void StartTimers()
    {
        _clockTimer = _timerFactory.Create(ClockInterval);
        _clockTimer.Tick += (_, _) => _viewModel.RefreshClock();
        _clockTimer.Start();

        _refreshTimer = _timerFactory.Create(_viewModel.AutoSyncInterval);
        _refreshTimer.Tick += (_, _) => _ = _viewModel.RefreshAsync();
        _refreshTimer.Start();

        _foregroundTimer = _timerFactory.Create(ForegroundInterval);
        _foregroundTimer.Tick += (_, _) => UpdateForegroundVisibility();
        _foregroundTimer.Start();
    }

    private void OnSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        _dispatcher.Invoke(() => _viewModel.ApplySnapshot(snapshot));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PulseMeterWindowViewModel.AutoSyncSeconds)
            or nameof(PulseMeterWindowViewModel.SelectedLimitKey)
            or nameof(PulseMeterWindowViewModel.IsAlwaysOnTop)
            or nameof(PulseMeterWindowViewModel.IsNavigationPanelExpanded)
            or nameof(PulseMeterWindowViewModel.IsRateLimitsVisible)
            or nameof(PulseMeterWindowViewModel.IsRateLimitsDailyVisible)
            or nameof(PulseMeterWindowViewModel.IsRunwayForecastVisible)
            or nameof(PulseMeterWindowViewModel.IsResetCreditsVisible)
            or nameof(PulseMeterWindowViewModel.IsAccountUsageVisible)
            or nameof(PulseMeterWindowViewModel.IsProjectUsageVisible)
            or nameof(PulseMeterWindowViewModel.IsUsageAttributionVisible)
            or nameof(PulseMeterWindowViewModel.IsDailyUsageVisible))
        {
            _appSettingsStore.Save(CaptureAppSettings(_viewModel));
        }

        if (e.PropertyName is nameof(PulseMeterWindowViewModel.AutoSyncInterval) or nameof(PulseMeterWindowViewModel.AutoSyncSeconds)
            && _refreshTimer is not null)
        {
            _refreshTimer.Interval = _viewModel.AutoSyncInterval;
        }
    }

    private static PulseMeterAppSettings CaptureAppSettings(PulseMeterWindowViewModel viewModel)
    {
        return new PulseMeterAppSettings(
            viewModel.AutoSyncSeconds,
            viewModel.IsAlwaysOnTop,
            viewModel.NavigationRail.CaptureVisibility(),
            viewModel.SelectedLimitKey,
            viewModel.IsNavigationPanelExpanded);
    }

    private void UpdateForegroundVisibility()
    {
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

            if (!_pulseMeterWindow.IsVisible)
            {
                _pulseMeterWindow.Show();
            }

            return;
        }

        if (_viewModel.AutoHideWhenFocusLeaves && _pulseMeterWindow.IsVisible)
        {
            _pulseMeterWindow.Hide();
        }
    }
}
