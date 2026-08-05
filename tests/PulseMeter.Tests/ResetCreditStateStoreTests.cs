using PulseMeter.Slices.ResetCredits;

namespace PulseMeter.Tests;

public sealed class ResetCreditStateStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsDetectedCreditExpiry()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "reset-credits.json");
        var store = new ResetCreditStateStore(path);
        var state = new ResetCreditTrackerState(
            HasObservedAvailableCount: true,
            NextCreditNumber: 4,
            Credits:
            [
                new ResetCreditState(1, null),
                new ResetCreditState(2, null),
                new ResetCreditState(3, new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero))
            ]);

        store.Save(state);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(4, loaded.NextCreditNumber);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero), loaded.Credits[2].ExpiresAtUtc);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"hasObservedAvailableCount\": true, \"nextCreditNumber\": 2, \"credits\": null }")]
    [InlineData("{ \"hasObservedAvailableCount\": true, \"nextCreditNumber\": 2, \"credits\": [null] }")]
    public void Load_FallsBackToBackupWhenPrimaryStateIsStructurallyInvalid(string invalidPrimaryJson)
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "reset-credits.json");
        var store = new ResetCreditStateStore(path);
        var state = new ResetCreditTrackerState(true, 2, [new ResetCreditState(1, null)]);
        Assert.True(store.Save(state));
        File.WriteAllText(path, invalidPrimaryJson);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.True(loaded.HasObservedAvailableCount);
        Assert.Equal(2, loaded.NextCreditNumber);
        Assert.Equal(1, Assert.Single(loaded.Credits).Number);
    }
}
