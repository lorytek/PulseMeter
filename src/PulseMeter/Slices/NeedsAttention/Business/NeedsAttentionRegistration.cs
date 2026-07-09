using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.NeedsAttention.Business;

internal static class NeedsAttentionRegistration
{
    internal static IServiceCollection AddNeedsAttentionSlice(this IServiceCollection services)
    {
        services.AddSingleton<INeedsAttentionPresenter, NeedsAttentionPresenter>();
        services.AddSingleton<NeedsAttentionSectionViewModel>();

        return services;
    }
}
