using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.DailyUsage.Business;

internal static class DailyUsageRegistration
{
    internal static IServiceCollection AddDailyUsageSlice(this IServiceCollection services)
    {
        services.AddSingleton<IDailyUsagePresenter, DailyUsagePresenter>();
        services.AddSingleton<DailyUsageSectionViewModel>();

        return services;
    }
}
