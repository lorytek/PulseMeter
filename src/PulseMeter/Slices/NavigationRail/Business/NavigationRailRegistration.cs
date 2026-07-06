using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.NavigationRail.Business;

internal static class NavigationRailRegistration
{
    internal static IServiceCollection AddNavigationRailSlice(this IServiceCollection services)
    {
        services.AddSingleton<NavigationRailViewModel>();

        return services;
    }
}
