using PulseMeter.Platform.Persistence;

namespace PulseMeter.Tests;

public sealed class PulseMeterAppSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsActiveAppSettingsOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new PulseMeterAppSettingsStore(path);
        var settings = new PulseMeterAppSettings(
            AutoSyncSeconds: 45,
            IsAlwaysOnTop: true);

        store.Save(settings);

        var json = File.ReadAllText(path);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(45, loaded.AutoSyncSeconds);
        Assert.True(loaded.IsAlwaysOnTop);
        Assert.DoesNotContain("budgetAlerts", json);
    }

    [Fact]
    public void Load_IgnoresLegacyBudgetSettingsFromOlderSettingsJson()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            {
              "autoSyncSeconds": 60,
              "isAlwaysOnTop": true,
              "budgetAlerts": {
                "dailyTokenBudget": 2500000,
                "warningPercent": 70,
                "criticalPercent": 92
              }
            }
            """);
        var store = new PulseMeterAppSettingsStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(60, loaded.AutoSyncSeconds);
        Assert.True(loaded.IsAlwaysOnTop);
    }

    [Fact]
    public void Load_DefaultsNewRunwayForecastVisibilityForLegacyDashboardSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            {
              "autoSyncSeconds": 90,
              "dashboardVisibility": {
                "rateLimits": true,
                "weeklyPace": true,
                "resetCredits": true,
                "accountUsage": true,
                "projectUsage": true,
                "usageExplorer": true,
                "burnAnalysis": true,
                "dailyUsage": true
              }
            }
            """);
        var store = new PulseMeterAppSettingsStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded?.DashboardVisibility);
        Assert.True(loaded.DashboardVisibility.RunwayForecast);
    }
}
