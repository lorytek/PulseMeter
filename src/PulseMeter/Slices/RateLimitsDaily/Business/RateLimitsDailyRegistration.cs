using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.RateLimitsDaily.Business;

internal static class RateLimitsDailyRegistration
{
    internal static IServiceCollection AddRateLimitsDailySlice(this IServiceCollection services)
    {
        services.AddSingleton<IRateLimitsDailyPresenter, RateLimitsDailyPresenter>();
        services.AddSingleton<RateLimitsDailySectionViewModel>();

        return services;
    }
}
