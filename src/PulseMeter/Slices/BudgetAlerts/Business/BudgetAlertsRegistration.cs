using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.BudgetAlerts.Business;

internal static class BudgetAlertsRegistration
{
    internal static IServiceCollection AddBudgetAlertsSlice(this IServiceCollection services)
    {
        services.AddSingleton<IBudgetAlertTracker, BudgetAlertTracker>();

        return services;
    }
}
