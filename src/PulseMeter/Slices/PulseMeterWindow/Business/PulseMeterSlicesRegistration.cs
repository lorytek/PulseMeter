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
using PulseMeter.Slices.RunwayForecast;
using PulseMeter.Slices.UsageAttribution;
using PulseMeter.Slices.UsageSignals;
using PulseMeter.Slices.UsageTrend;

namespace PulseMeter.Slices.PulseMeterWindow.Business;

internal static class PulseMeterSlicesRegistration
{
    internal static IServiceCollection AddPulseMeterSlices(this IServiceCollection services)
    {
        services.AddPulseMeterSlicesWithoutUsageSignals();
        services.AddUsageSignalsSlice();

        return services;
    }

    internal static IServiceCollection AddPulseMeterSlicesWithoutUsageSignals(this IServiceCollection services)
    {
        services.AddDataBarSlice();
        services.AddExpandedHeaderSlice();
        services.AddNavigationRailSlice();
        services.AddRateLimitsSlice();
        services.AddUsageTrendSlice();
        services.AddRateLimitsDailySlice();
        services.AddRunwayForecastSlice();
        services.AddNeedsAttentionSlice();
        services.AddBudgetAlertsSlice();
        services.AddResetCreditsSlice();
        services.AddAccountUsageSlice();
        services.AddProjectUsageSlice();
        services.AddUsageAttributionSlice();
        services.AddDailyUsageSlice();

        return services;
    }
}
