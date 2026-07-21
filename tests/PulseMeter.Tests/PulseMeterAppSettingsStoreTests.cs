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
            IsAlwaysOnTop: true,
            SelectedRateLimitKey: "codex_bengalfox",
            IsNavigationPanelExpanded: false);

        store.Save(settings);

        var json = File.ReadAllText(path);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(45, loaded.AutoSyncSeconds);
        Assert.True(loaded.IsAlwaysOnTop);
        Assert.Equal("codex_bengalfox", loaded.SelectedRateLimitKey);
        Assert.False(loaded.IsNavigationPanelExpanded);
        Assert.DoesNotContain("budgetAlerts", json);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Load_RecoversFromBackupWhenPrimaryIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new PulseMeterAppSettingsStore(path);
        store.Save(new PulseMeterAppSettings(AutoSyncSeconds: 45, IsAlwaysOnTop: true));
        File.WriteAllText(path, "{ not valid json");

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(45, loaded.AutoSyncSeconds);
        Assert.True(loaded.IsAlwaysOnTop);
    }

    [Fact]
    public async Task Save_WaitsForAnotherThreadToReleaseThePathMutex()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new PulseMeterAppSettingsStore(path);
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var mutexHolder = Task.Factory.StartNew(
            () =>
            {
                using var mutex = new Mutex(initiallyOwned: false, AtomicJsonFileStore.GetMutexName(path));
                Assert.True(mutex.WaitOne());
                acquired.Set();
                release.Wait();
                mutex.ReleaseMutex();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var saveTask = Task.Run(() =>
            {
                started.TrySetResult();
                store.Save(new PulseMeterAppSettings(AutoSyncSeconds: 30));
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            Assert.False(saveTask.IsCompleted);

            release.Set();
            await mutexHolder.WaitAsync(TimeSpan.FromSeconds(5));
            await saveTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(store.Load());
        }
        finally
        {
            release.Set();
            await mutexHolder.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void Load_ScavengesStaleTemporaryFilesButKeepsFreshOnes()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new PulseMeterAppSettingsStore(path);
        store.Save(new PulseMeterAppSettings());
        var directory = Path.GetDirectoryName(path)!;
        var staleTemporaryPath = Path.Combine(directory, ".settings.json.stale.tmp");
        var freshTemporaryPath = Path.Combine(directory, ".settings.json.fresh.tmp");
        File.WriteAllText(staleTemporaryPath, "stale");
        File.SetLastWriteTimeUtc(staleTemporaryPath, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(freshTemporaryPath, "fresh");

        _ = store.Load();

        Assert.False(File.Exists(staleTemporaryPath));
        Assert.True(File.Exists(freshTemporaryPath));
    }

    [Fact]
    public void AtomicStore_DoesNotSwallowInvalidPathsOrSerializerFailures()
    {
        Assert.Throws<ArgumentException>(() => AtomicJsonFileStore.Save("\0", new object(), new System.Text.Json.JsonSerializerOptions()));

        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var circular = new CircularValue();
        circular.Next = circular;

        Assert.Throws<System.Text.Json.JsonException>(() => AtomicJsonFileStore.Save(path, circular, new System.Text.Json.JsonSerializerOptions()));
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

    private sealed class CircularValue
    {
        public CircularValue? Next { get; set; }
    }
}
