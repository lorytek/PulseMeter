using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.RunwayForecast.Business;

internal static class RunwayForecastRegistration
{
    internal static IServiceCollection AddRunwayForecastSlice(this IServiceCollection services)
    {
        services.AddSingleton<IRunwayForecastPresenter, RunwayForecastPresenter>();
        services.AddSingleton<RunwayForecastSectionViewModel>();

        return services;
    }
}
