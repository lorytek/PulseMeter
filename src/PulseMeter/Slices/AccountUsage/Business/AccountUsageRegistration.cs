using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.AccountUsage.Business;

internal static class AccountUsageRegistration
{
    internal static IServiceCollection AddAccountUsageSlice(this IServiceCollection services)
    {
        services.AddSingleton<IAccountUsagePresenter, AccountUsagePresenter>();
        services.AddSingleton<AccountUsageSectionViewModel>();

        return services;
    }
}
