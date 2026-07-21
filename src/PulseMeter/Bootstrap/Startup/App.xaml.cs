using System.Diagnostics;
using System.Windows;

namespace PulseMeter.Bootstrap.Startup;

public partial class App : System.Windows.Application
{
    private PulseMeterApplication? _application;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _application = new PulseMeterApplication(RequestShutdown);
        await _application.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var stopTask = _application?.StopAsync();
        if (stopTask is { IsCompleted: true })
        {
            try
            {
                stopTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"PulseMeter exit cleanup failed: {exception}");
            }
        }
        else if (stopTask is not null)
        {
            _ = stopTask.ContinueWith(
                task => Debug.WriteLine($"PulseMeter exit cleanup failed: {task.Exception}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        base.OnExit(e);
    }

    private async void RequestShutdown()
    {
        try
        {
            await RequestShutdownAsync();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"PulseMeter shutdown failed: {exception}");
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
}
