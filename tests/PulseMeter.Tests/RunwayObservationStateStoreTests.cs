using PulseMeter.Slices.UsageSignals.Business;
using PulseMeter.Slices.UsageSignals.Models;

namespace PulseMeter.Tests;

public sealed class RunwayObservationStateStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsAndRecoversFromBackup()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "runway-observations.json");
        var store = new RunwayObservationStateStore(path);
        var state = new RunwayObservationState(
            RunwayObservationStateStore.CurrentSchemaVersion,
            [new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 42, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, StartsAfterMeasurementGap: true)],
            [new BaselineHourlyUsageRateSample("codex|10080", DateTimeOffset.UtcNow.AddHours(-2), 1.5, HourlyActivityEvidence.Both)],
            [new BaselineResetCutoffSample("codex|10080", DateTimeOffset.UtcNow.AddHours(-3))]);

        store.Save(state);
        File.WriteAllText(path, "{ invalid json");

        var loaded = store.Load();

        Assert.Equal(RunwayObservationLoadStatus.Loaded, loaded.Status);
        var loadedState = Assert.IsType<RunwayObservationState>(loaded.State);
        Assert.Equal(RunwayObservationStateStore.CurrentSchemaVersion, loadedState.SchemaVersion);
        var sample = Assert.IsType<RunwayObservationSample>(Assert.Single(loadedState.Samples!));
        Assert.Equal(42, sample.UsedPercent);
        Assert.True(sample.StartsAfterMeasurementGap);
        var baselineRates = Assert.IsAssignableFrom<IReadOnlyList<BaselineHourlyUsageRateSample?>>(loadedState.BaselineHourlyRates);
        var baseline = Assert.IsType<BaselineHourlyUsageRateSample>(Assert.Single(baselineRates));
        Assert.Equal(1.5, baseline.PercentPerHour);
        Assert.Equal(HourlyActivityEvidence.Both, baseline.ActivityEvidence);
        Assert.Single(loadedState.BaselineResetCutoffs!);
    }

    [Fact]
    public void Load_AcceptsSchemaV2RowsWithoutActivityClassification()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "runway-observations.json");
        File.WriteAllText(path,
            """
            {
              "schemaVersion": 2,
              "samples": [],
              "baselineHourlyRates": [
                { "bucketId": "codex|10080", "hourStartedAtUtc": "2026-07-21T08:00:00+00:00", "percentPerHour": 0 },
                { "bucketId": "codex|10080", "hourStartedAtUtc": "2026-07-21T09:00:00+00:00", "percentPerHour": 1.5 }
              ]
            }
            """);

        var loaded = new RunwayObservationStateStore(path).Load();

        Assert.Equal(RunwayObservationLoadStatus.Loaded, loaded.Status);
        Assert.Equal(2, loaded.State!.SchemaVersion);
        Assert.All(loaded.State.BaselineHourlyRates!, row => Assert.Equal(HourlyActivityEvidence.None, row!.ActivityEvidence));
    }

    [Fact]
    public void Load_AcceptsExistingStateWithoutMeasurementGapProperty()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "runway-observations.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "samples": [
                {
                  "bucketId": "codex|10080",
                  "limitKey": "codex",
                  "trackLabel": "General",
                  "windowLabel": "Weekly",
                  "forecastWindowLabel": "7-Day Usage",
                  "windowDurationMins": 10080,
                  "usedPercent": 74,
                  "resetsAtUtc": "2026-07-25T07:52:11+00:00",
                  "observedAtUtc": "2026-07-21T08:02:50+00:00"
                }
              ]
            }
            """);
        var store = new RunwayObservationStateStore(path);

        var loaded = store.Load();

        var sample = Assert.IsType<RunwayObservationSample>(Assert.Single(loaded.State!.Samples!));
        Assert.False(sample.StartsAfterMeasurementGap);
        Assert.Equal(1, loaded.State.SchemaVersion);
        Assert.Null(loaded.State.BaselineHourlyRates);
    }

    [Fact]
    public void Load_WhenPrimaryAndBackupAreCorruptAllowsFreshStateToReplaceThem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "runway-observations.json");
        File.WriteAllText(path, "{ corrupt primary");
        File.WriteAllText(path + ".bak", "{ corrupt backup");
        var store = new RunwayObservationStateStore(path);

        var corrupt = store.Load();

        Assert.Equal(RunwayObservationLoadStatus.Corrupt, corrupt.Status);
        var replacement = new RunwayObservationState(
            RunwayObservationStateStore.CurrentSchemaVersion,
            [new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 25, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow)]);
        Assert.True(store.Save(replacement));
        var loaded = store.Load();
        Assert.Equal(RunwayObservationLoadStatus.Loaded, loaded.Status);
        var sample = Assert.IsType<RunwayObservationSample>(Assert.Single(loaded.State!.Samples!));
        Assert.Equal(25, sample.UsedPercent);
    }
}
