using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.UsageTrend.Business;

internal static class UsageTrendRegistration
{
    internal static IServiceCollection AddUsageTrendSlice(this IServiceCollection services)
    {
        services.AddSingleton<IUsageTrendPresenter, UsageTrendPresenter>();
        services.AddSingleton<UsageTrendSectionViewModel>();

        return services;
    }
}
