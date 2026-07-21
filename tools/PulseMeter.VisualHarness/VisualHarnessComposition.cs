using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Composition;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.ResetCredits.Business;
using PulseMeter.Slices.UsageCollection.Business;
using PulseMeter.Slices.UsageSignals.Business;

namespace PulseMeter.VisualHarness;

public static class VisualHarnessComposition
{
    public static ServiceProvider BuildServiceProvider(
        VisualHarnessPaths paths,
        Action shutdown,
        VisualHarnessScenario scenario = VisualHarnessScenario.Healthy)
    {
        VisualHarnessWorkspace.CreateStateRoot(paths);

        var services = new ServiceCollection();

        services.AddSingleton(_ => new VisualHarnessUsageService(scenario));
        services.AddSingleton<IUsageService>(sp => sp.GetRequiredService<VisualHarnessUsageService>());
        services.AddSingleton<IMockUsageService>(sp => sp.GetRequiredService<VisualHarnessUsageService>());

        services.AddPulseMeterSlicesWithoutUsageSignals();
        services.AddSingleton<IRunwayObservationStateStore>(
            _ => new RunwayObservationStateStore(paths.RunwayObservationsPath));
        services.AddSingleton<IUsageSignalsTracker, UsageSignalsTracker>();

        services.AddSingleton<IPulseMeterAppSettingsStore>(
            _ => new PulseMeterAppSettingsStore(paths.AppSettingsPath));
        services.AddSingleton<IPulseMeterWindowStateStore>(
            _ => new PulseMeterWindowStateStore(paths.WindowStatePath));
        services.AddSingleton<IResetCreditStateStore>(
            _ => new ResetCreditStateStore(paths.ResetCreditStatePath));

        services.AddSingleton<VisualHarnessForegroundWindowService>();
        services.AddSingleton<IForegroundWindowService>(
            sp => sp.GetRequiredService<VisualHarnessForegroundWindowService>());
        services.AddSingleton<VisualHarnessIdleTimeProvider>();
        services.AddSingleton<IUserIdleTimeProvider>(
            sp => sp.GetRequiredService<VisualHarnessIdleTimeProvider>());
        services.AddSingleton<VisualHarnessClipboardService>();
        services.AddSingleton<IClipboardService>(
            sp => sp.GetRequiredService<VisualHarnessClipboardService>());
        services.AddSingleton<VisualHarnessTrayIconService>();
        services.AddSingleton<ITrayIconService>(
            sp => sp.GetRequiredService<VisualHarnessTrayIconService>());

        services.AddSingleton<IPulseMeterTimerFactory, DispatcherPulseMeterTimerFactory>();
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IPulseMeterWindowLifecycleCoordinator, PulseMeterWindowLifecycleCoordinator>();
        services.AddPulseMeterWindowCore(shutdown);

        return services.BuildServiceProvider();
    }
}
