using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.ExpandedHeader.Business;

internal static class ExpandedHeaderRegistration
{
    internal static IServiceCollection AddExpandedHeaderSlice(this IServiceCollection services)
    {
        services.AddSingleton<ExpandedHeaderViewModel>();

        return services;
    }
}
