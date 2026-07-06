using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.AccountUsage;
using PulseMeter.Slices.DataBar;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.ExpandedHeader;
using PulseMeter.Slices.NavigationRail;
using PulseMeter.Slices.ProjectUsage;
using PulseMeter.Slices.RateLimits;
using PulseMeter.Slices.RateLimitsDaily;
using PulseMeter.Slices.ResetCredits;

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
        services.AddResetCreditsSlice();
        services.AddAccountUsageSlice();
        services.AddProjectUsageSlice();
        services.AddDailyUsageSlice();

        return services;
    }
}
