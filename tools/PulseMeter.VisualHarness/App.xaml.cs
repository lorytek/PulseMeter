using System.Diagnostics;
using System.Windows;
using PulseMeter.Bootstrap.Startup;

namespace PulseMeter.VisualHarness;

public partial class App : System.Windows.Application
{
    private PulseMeterApplication? _application;
    private int _shutdownRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = VisualHarnessWorkspace.LocateFrom(AppContext.BaseDirectory);
        var scenario = VisualHarnessScenarioParser.Parse(e.Args);
        _application = new PulseMeterApplication(
            RequestShutdown,
            shutdown => VisualHarnessComposition.BuildServiceProvider(paths, shutdown, scenario));
        await _application.StartAsync();

        if (Windows.OfType<Window>().SingleOrDefault() is { } window)
        {
            window.IsVisibleChanged += Window_IsVisibleChanged;
        }
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
                Debug.WriteLine($"PulseMeter visual harness cleanup failed: {exception}");
            }
        }
        else if (stopTask is not null)
        {
            _ = stopTask.ContinueWith(
                task => Debug.WriteLine($"PulseMeter visual harness cleanup failed: {task.Exception}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        base.OnExit(e);
    }

    private async void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        try
        {
            if (_application is not null)
            {
                await _application.StopAsync();
            }
        }
        finally
        {
            Shutdown();
        }
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            RequestShutdown();
        }
    }
}
