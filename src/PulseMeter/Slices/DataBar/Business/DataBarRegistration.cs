using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.DataBar.Business;

internal static class DataBarRegistration
{
    internal static IServiceCollection AddDataBarSlice(this IServiceCollection services)
    {
        services.AddSingleton<DataBarViewModel>();

        return services;
    }
}
