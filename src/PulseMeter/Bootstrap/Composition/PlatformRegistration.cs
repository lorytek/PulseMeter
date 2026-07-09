using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.ResetCredits;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Platform.Threading;
using PulseMeter.Platform.Timing;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Bootstrap.Composition;

internal static class PlatformRegistration
{
    internal static IServiceCollection AddPulseMeterPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IPulseMeterAppSettingsStore, PulseMeterAppSettingsStore>();
        services.AddSingleton<IResetCreditStateStore, ResetCreditStateStore>();
        services.AddSingleton<IPulseMeterWindowStateStore, PulseMeterWindowStateStore>();
        services.AddSingleton<IForegroundWindowService, ForegroundWindowService>();
        services.AddSingleton<IUserIdleTimeProvider, UserIdleTimeProvider>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IPulseMeterTimerFactory, DispatcherPulseMeterTimerFactory>();
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IPulseMeterWindowLifecycleCoordinator, PulseMeterWindowLifecycleCoordinator>();

        return services;
    }
}
