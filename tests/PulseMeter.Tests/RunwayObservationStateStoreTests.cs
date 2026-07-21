using PulseMeter.Slices.UsageSignals.Business;

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
            [new RunwayObservationSample("codex|300", "codex", "General", "5h Window", "5h", 300, 42, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, StartsAfterMeasurementGap: true)]);

        store.Save(state);
        File.WriteAllText(path, "{ invalid json");

        var loaded = store.Load();

        Assert.Equal(RunwayObservationLoadStatus.Loaded, loaded.Status);
        var loadedState = Assert.IsType<RunwayObservationState>(loaded.State);
        Assert.Equal(RunwayObservationStateStore.CurrentSchemaVersion, loadedState.SchemaVersion);
        var sample = Assert.IsType<RunwayObservationSample>(Assert.Single(loadedState.Samples!));
        Assert.Equal(42, sample.UsedPercent);
        Assert.True(sample.StartsAfterMeasurementGap);
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
    }
}
