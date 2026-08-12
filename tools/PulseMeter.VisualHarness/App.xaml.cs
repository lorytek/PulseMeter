using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Startup;
using PulseMeter.Slices.SupportSnapshot.UI;
using PulseMeter.Slices.ProjectUsage.UI;

namespace PulseMeter.VisualHarness;

public partial class App : System.Windows.Application
{
    private PulseMeterApplication? _application;
    private int _shutdownRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = VisualHarnessWorkspace.LocateFromAny(
                Environment.GetEnvironmentVariable("PULSEMETER_REPO_ROOT"),
                Environment.CurrentDirectory,
                AppContext.BaseDirectory);
            var scenario = VisualHarnessScenarioParser.Parse(e.Args);
            var showSupportSnapshot = VisualHarnessScenarioParser.ShouldOpenSupportSnapshot(e.Args);
            var desktopProcessSnapshotScenario = VisualHarnessScenarioParser.ParseDesktopProcessSnapshot(e.Args);
            var projectLocationScenario = VisualHarnessScenarioParser.ParseProjectLocation(e.Args);
            ServiceProvider? serviceProvider = null;
            _application = new PulseMeterApplication(
                RequestShutdown,
                shutdown => serviceProvider = VisualHarnessComposition.BuildServiceProvider(paths, shutdown, scenario, desktopProcessSnapshotScenario, projectLocationScenario));
            await _application.StartAsync();

            await Dispatcher.InvokeAsync(() =>
            {
                if (Windows.OfType<Window>().SingleOrDefault() is { } window)
                {
                    window.IsVisibleChanged += Window_IsVisibleChanged;
                }

                if (showSupportSnapshot)
                {
                    _ = Dispatcher.BeginInvoke(() => ShowSupportSnapshot(serviceProvider!));
                }

                if (desktopProcessSnapshotScenario != DesktopProcessSnapshotVisualScenario.None)
                {
                    _ = Dispatcher.BeginInvoke(() => ShowDesktopProcessSnapshot(serviceProvider!, desktopProcessSnapshotScenario));
                }

                if (projectLocationScenario != ProjectLocationVisualScenario.None)
                {
                    _ = Dispatcher.BeginInvoke(() => ShowProjectLocation(serviceProvider!));
                }
            });
        }
        catch (Exception exception)
        {
            WriteFailure("startup", exception);
            MessageBox.Show(
                $"The visual harness could not start ({exception.GetBaseException().GetType().Name}). " +
                "Launch it from the PulseMeter worktree or set PULSEMETER_REPO_ROOT to that folder.",
                "PulseMeter Visual Harness",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
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
                WriteFailure("cleanup", exception);
            }
        }
        else if (stopTask is not null)
        {
            _ = stopTask.ContinueWith(
                task => WriteFailure("cleanup", task.Exception!),
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
        catch (Exception exception)
        {
            WriteFailure("shutdown", exception);
        }
        finally
        {
            Shutdown();
        }
    }

    private static void ShowSupportSnapshot(ServiceProvider serviceProvider)
    {
        try
        {
            serviceProvider.GetRequiredService<ISupportSnapshotPresenter>().ShowPreview();
        }
        catch (Exception exception)
        {
            WriteFailure("support snapshot preview", exception);
            MessageBox.Show(
                $"The support snapshot preview could not open ({exception.GetBaseException().GetType().Name}).",
                "PulseMeter Visual Harness",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void ShowDesktopProcessSnapshot(
        ServiceProvider serviceProvider,
        DesktopProcessSnapshotVisualScenario scenario)
    {
        try
        {
            serviceProvider.GetRequiredService<ICodexDesktopProcessSnapshotPresenter>()
                .ShowSnapshot(scenario is DesktopProcessSnapshotVisualScenario.Complete or DesktopProcessSnapshotVisualScenario.Partial);
        }
        catch (Exception exception)
        {
            WriteFailure("desktop process snapshot preview", exception);
            MessageBox.Show(
                $"The desktop process snapshot preview could not open ({exception.GetBaseException().GetType().Name}).",
                "PulseMeter Visual Harness",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void ShowProjectLocation(ServiceProvider serviceProvider)
    {
        try
        {
            serviceProvider.GetRequiredService<IProjectLocationPresenter>().ShowLocation(
                "Visual harness long project path",
                @"C:\VisualHarness\a-deliberately-long-observed-project-path\nested\worktree\that-is-not-opened-or-validated-by-the-preview\PulseMeter");
        }
        catch (Exception exception)
        {
            WriteFailure("project location preview", exception);
            MessageBox.Show(
                $"The project location preview could not open ({exception.GetBaseException().GetType().Name}).",
                "PulseMeter Visual Harness",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            RequestShutdown();
        }
    }

    private static void WriteFailure(string operation, Exception exception)
    {
        Debug.WriteLine($"PulseMeter visual harness {operation} failed ({exception.GetBaseException().GetType().Name}).");
    }
}
