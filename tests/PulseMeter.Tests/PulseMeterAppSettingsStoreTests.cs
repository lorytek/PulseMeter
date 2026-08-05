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
            IsNavigationPanelExpanded: false,
            AutoShowWhenCodexFocused: false,
            AutoHideWhenFocusLeaves: true);

        store.Save(settings);

        var json = File.ReadAllText(path);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(45, loaded.AutoSyncSeconds);
        Assert.True(loaded.IsAlwaysOnTop);
        Assert.Equal("codex_bengalfox", loaded.SelectedRateLimitKey);
        Assert.False(loaded.IsNavigationPanelExpanded);
        Assert.False(loaded.AutoShowWhenCodexFocused);
        Assert.True(loaded.AutoHideWhenFocusLeaves);
        Assert.DoesNotContain("budgetAlerts", json);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Load_LegacySettingsUseFocusAutomationDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"autoSyncSeconds\": 45 }");

        var loaded = new PulseMeterAppSettingsStore(path).Load();

        Assert.NotNull(loaded);
        Assert.True(loaded.AutoShowWhenCodexFocused);
        Assert.False(loaded.AutoHideWhenFocusLeaves);
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
        var mutexHolder = Task.Factory.StartNew(
            () =>
            {
                using var mutex = new Mutex(initiallyOwned: false, AtomicJsonFileStore.GetMutexName(path));
                Assert.True(mutex.WaitOne());
                acquired.Set();
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                mutex.ReleaseMutex();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        Assert.True(store.Save(new PulseMeterAppSettings(AutoSyncSeconds: 30)));

        elapsed.Stop();
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(100));
        await mutexHolder.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(store.Load());
    }

    [Fact]
    public async Task Save_ReturnsFalseAfterTheTwoSecondMutexTimeout()
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
            var startedAt = DateTime.UtcNow;
            var saved = await Task.Run(() => store.Save(new PulseMeterAppSettings(AutoSyncSeconds: 30)))
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(saved);
            Assert.True(DateTime.UtcNow - startedAt >= TimeSpan.FromSeconds(2));
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
        Assert.True(loaded.DashboardVisibility.BlockPlanner);
    }

    [Fact]
    public void RecoveryWatches_RoundTripAndRemainOptionalForLegacyJson()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var reset = new DateTimeOffset(2026, 7, 22, 13, 0, 0, TimeSpan.Zero);
        var store = new PulseMeterAppSettingsStore(path);
        store.Save(new PulseMeterAppSettings(RecoveryWatches:
        [
            new RecoveryWatchSettings("codex", 300, 60, reset),
            new RecoveryWatchSettings("codex", 10_080, 240, reset.AddDays(3))
        ]));

        var loaded = store.Load();
        Assert.Equal(2, loaded?.RecoveryWatches?.Count);
        Assert.Equal(60, loaded?.RecoveryWatches?[0].BlockDurationMinutes);

        File.WriteAllText(path, "{ \"autoSyncSeconds\": 45 }");
        var legacy = store.Load();
        Assert.Equal(45, legacy?.AutoSyncSeconds);
        Assert.Null(legacy?.RecoveryWatches);
    }

    [Fact]
    public void Load_PreservesNullRecoveryWatchEntriesForRestoreValidation()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            {
              "recoveryWatches": [
                null,
                {
                  "limitKey": "codex",
                  "windowDurationMins": 300,
                  "blockDurationMinutes": 60,
                  "resetAtUtc": "2026-07-22T13:00:00+00:00"
                }
              ]
            }
            """);
        var store = new PulseMeterAppSettingsStore(path);

        var loaded = store.Load();

        Assert.Equal(2, loaded?.RecoveryWatches?.Count);
        Assert.Null(loaded?.RecoveryWatches?[0]);
        Assert.NotNull(loaded?.RecoveryWatches?[1]);
    }

    private sealed class CircularValue
    {
        public CircularValue? Next { get; set; }
    }
}
