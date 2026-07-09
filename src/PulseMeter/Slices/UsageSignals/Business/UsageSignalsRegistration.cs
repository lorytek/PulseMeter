using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.UsageSignals.Business;

internal static class UsageSignalsRegistration
{
    internal static IServiceCollection AddUsageSignalsSlice(this IServiceCollection services)
    {
        services.AddSingleton<IUsageSignalsTracker, UsageSignalsTracker>();

        return services;
    }
}
