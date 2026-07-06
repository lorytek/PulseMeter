using System.Windows;

namespace PulseMeter.Bootstrap.Startup;

public partial class App : System.Windows.Application
{
    private PulseMeterApplication? _application;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _application = new PulseMeterApplication(Shutdown);
        await _application.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _application?.Stop();

        base.OnExit(e);
    }
}
