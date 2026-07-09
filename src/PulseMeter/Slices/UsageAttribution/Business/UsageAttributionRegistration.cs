using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.UsageAttribution.Business;

internal static class UsageAttributionRegistration
{
    internal static IServiceCollection AddUsageAttributionSlice(this IServiceCollection services)
    {
        services.AddSingleton<IUsageAttributionPresenter, UsageAttributionPresenter>();
        services.AddSingleton<UsageAttributionSectionViewModel>();

        return services;
    }
}
