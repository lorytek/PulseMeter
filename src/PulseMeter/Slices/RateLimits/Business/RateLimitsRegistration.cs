using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.RateLimits.Business;

internal static class RateLimitsRegistration
{
    internal static IServiceCollection AddRateLimitsSlice(this IServiceCollection services)
    {
        services.AddSingleton<IRateLimitsPresenter, RateLimitsPresenter>();
        services.AddSingleton<RateLimitsSectionViewModel>();

        return services;
    }
}
