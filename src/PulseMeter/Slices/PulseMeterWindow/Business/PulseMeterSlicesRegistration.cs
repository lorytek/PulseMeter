using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.AccountUsage;
using PulseMeter.Slices.BudgetAlerts;
using PulseMeter.Slices.DataBar;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.ExpandedHeader;
using PulseMeter.Slices.NavigationRail;
using PulseMeter.Slices.NeedsAttention;
using PulseMeter.Slices.ProjectUsage;
using PulseMeter.Slices.RateLimits;
using PulseMeter.Slices.RateLimitsDaily;
using PulseMeter.Slices.ResetCredits;
using PulseMeter.Slices.UsageAttribution;
using PulseMeter.Slices.UsageSignals;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

internal static class PulseMeterSlicesRegistration
{
    internal static IServiceCollection AddPulseMeterSlices(this IServiceCollection services)
    {
        services.AddDataBarSlice();
        services.AddExpandedHeaderSlice();
        services.AddNavigationRailSlice();
        services.AddRateLimitsSlice();
        services.AddRateLimitsDailySlice();
        services.AddNeedsAttentionSlice();
        services.AddBudgetAlertsSlice();
        services.AddResetCreditsSlice();
        services.AddAccountUsageSlice();
        services.AddProjectUsageSlice();
        services.AddUsageAttributionSlice();
        services.AddDailyUsageSlice();
        services.AddUsageSignalsSlice();

        return services;
    }
}
