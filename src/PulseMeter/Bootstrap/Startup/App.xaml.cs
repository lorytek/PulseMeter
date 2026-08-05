using System.Diagnostics;
using System.Windows;

using PulseMeter.Platform.Diagnostics;
using PulseMeter.Platform.Windows;

namespace PulseMeter.Bootstrap.Startup;

public partial class App : System.Windows.Application
{
    private PulseMeterApplication? _application;
    private readonly CancellationTokenSource _startupCancellation = new();
    private PulseMeterSingleInstanceCoordinator? _singleInstance;
    private int _shutdownRequested;
    private int _activationPending;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = PulseMeterSingleInstanceCoordinator.Acquire();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalPrimary();
            Shutdown();
            return;
        }

        _singleInstance.StartListening(RequestPrimaryActivation);

        _application = new PulseMeterApplication(RequestShutdown);
        var startupFailure = await TryStartAsync(_application, _startupCancellation.Token);
        if (startupFailure is null)
        {
            ShowPendingPrimaryActivation();
            return;
        }

        PrivacySafeDiagnostics.WriteFailure("startup failed", startupFailure);
        System.Windows.MessageBox.Show(
            "PulseMeter could not start. Please restart the app. If it keeps happening, install the latest version.",
            "PulseMeter",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        RequestShutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _startupCancellation.Cancel();

        try
        {
            _application?.PrepareForProcessExit();
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("critical exit cleanup failed", exception);
        }

        if (_application is not null)
        {
            try
            {
                CompletePendingStopForExit(_application);
            }
            catch (Exception exception)
            {
                PrivacySafeDiagnostics.WriteFailure("exit cleanup failed", exception);
            }
        }

        _startupCancellation.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void RequestPrimaryActivation()
    {
        Interlocked.Exchange(ref _activationPending, 1);
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(ShowPendingPrimaryActivation);
    }

    private void ShowPendingPrimaryActivation()
    {
        if (Volatile.Read(ref _activationPending) == 0)
        {
            return;
        }

        try
        {
            if (_application?.TryShowMainWindow() == true)
            {
                Interlocked.Exchange(ref _activationPending, 0);
            }
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("secondary launch activation failed", exception);
        }
    }

    private async void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        try
        {
            await RequestShutdownAsync();
        }
        catch (Exception exception)
        {
            PrivacySafeDiagnostics.WriteFailure("shutdown failed", exception);
        }
        finally
        {
            Shutdown();
        }
    }

    private async Task RequestShutdownAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
        }
    }

    internal static async Task<Exception?> TryStartAsync(
        PulseMeterApplication application,
        CancellationToken cancellationToken)
    {
        try
        {
            await application.StartAsync(cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    internal static void CompletePendingStopForExit(PulseMeterApplication application)
    {
        // OnExit cannot be awaited. Start the asynchronous disposal away from the
        // WPF synchronization context, then keep the process alive until it finishes.
        Task.Run(application.StopAsync).GetAwaiter().GetResult();
    }
}
