using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.AccountUsage;
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
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageSignals;
using PulseMeter.Slices.UsageTrend;

namespace PulseMeter.Bootstrap.Composition;

internal static class PulseMeterWindowRegistration
{
    private static readonly TimeSpan DefaultAutoRefreshInterval = TimeSpan.FromSeconds(90);

    internal static IServiceCollection AddPulseMeterWindow(this IServiceCollection services, Action shutdown)
    {
        services.AddPulseMeterWindowCore(shutdown);
        services.AddSingleton<ITrayIconService, TrayIconService>();

        return services;
    }

    internal static IServiceCollection AddPulseMeterWindowCore(this IServiceCollection services, Action shutdown)
    {
        services.AddSingleton(shutdown);
        services.AddSingleton(sp =>
        {
            var appSettingsStore = sp.GetRequiredService<IPulseMeterAppSettingsStore>();
            var windowStateStore = sp.GetRequiredService<IPulseMeterWindowStateStore>();
            var appSettings = appSettingsStore.Load();
            var autoSyncSeconds = appSettings?.AutoSyncSeconds ?? (int)DefaultAutoRefreshInterval.TotalSeconds;
            var navigationRail = sp.GetRequiredService<NavigationRailViewModel>();
            navigationRail.ApplyVisibility(appSettings?.DashboardVisibility);
            navigationRail.ApplyPanelState(appSettings?.IsNavigationPanelExpanded ?? true);

            return new PulseMeterWindowViewModel(
                sp.GetRequiredService<IUsageService>(),
                TimeSpan.FromSeconds(autoSyncSeconds),
                sp.GetRequiredService<IResetCreditStateStore>(),
                windowState: windowStateStore.Load(),
                isAlwaysOnTop: appSettings?.IsAlwaysOnTop ?? false,
                dataBar: sp.GetRequiredService<DataBarViewModel>(),
                expandedHeader: sp.GetRequiredService<ExpandedHeaderViewModel>(),
                navigationRail: navigationRail,
                rateLimits: sp.GetRequiredService<RateLimitsSectionViewModel>(),
                usageTrend: sp.GetRequiredService<UsageTrendSectionViewModel>(),
                rateLimitsDaily: sp.GetRequiredService<RateLimitsDailySectionViewModel>(),
                runwayForecast: sp.GetRequiredService<RunwayForecastSectionViewModel>(),
                needsAttention: sp.GetRequiredService<NeedsAttentionSectionViewModel>(),
                accountUsage: sp.GetRequiredService<AccountUsageSectionViewModel>(),
                resetCreditsSection: sp.GetRequiredService<ResetCreditsSectionViewModel>(),
                projectUsage: sp.GetRequiredService<ProjectUsageSectionViewModel>(),
                usageAttribution: sp.GetRequiredService<UsageAttributionSectionViewModel>(),
                dailyUsage: sp.GetRequiredService<DailyUsageSectionViewModel>(),
                usageSignalsTracker: sp.GetRequiredService<IUsageSignalsTracker>(),
                budgetAlertTracker: sp.GetRequiredService<IBudgetAlertTracker>(),
                selectedLimitKey: appSettings?.SelectedRateLimitKey);
        });

        services.AddSingleton(sp => new PulseMeterWindow
        {
            DataContext = sp.GetRequiredService<PulseMeterWindowViewModel>(),
            WindowStateStore = sp.GetRequiredService<IPulseMeterWindowStateStore>()
        });
        services.AddSingleton<IPulseMeterWindow>(sp => sp.GetRequiredService<PulseMeterWindow>());

        return services;
    }
}
