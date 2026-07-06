using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.ResetCredits.Business;

internal static class ResetCreditsRegistration
{
    internal static IServiceCollection AddResetCreditsSlice(this IServiceCollection services)
    {
        services.AddSingleton<IResetCreditsPresenter, ResetCreditsPresenter>();
        services.AddSingleton<ResetCreditsSectionViewModel>();

        return services;
    }
}
