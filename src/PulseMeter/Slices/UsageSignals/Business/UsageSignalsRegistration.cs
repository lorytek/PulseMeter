using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.UsageSignals.Business;

internal static class UsageSignalsRegistration
{
    internal static IServiceCollection AddUsageSignalsSlice(this IServiceCollection services)
    {
        services.AddSingleton<IRunwayObservationStateStore, RunwayObservationStateStore>();
        services.AddSingleton<IUsageSignalsTracker, UsageSignalsTracker>();
        services.AddSingleton<IMomentumBaselineController>(provider =>
            (UsageSignalsTracker)provider.GetRequiredService<IUsageSignalsTracker>());

        return services;
    }
}
