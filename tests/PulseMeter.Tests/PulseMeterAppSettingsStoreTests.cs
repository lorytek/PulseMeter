using PulseMeter.Platform.Persistence;

namespace PulseMeter.Tests;

public sealed class PulseMeterAppSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsAutoSyncSeconds()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new PulseMeterAppSettingsStore(path);
        var settings = new PulseMeterAppSettings(AutoSyncSeconds: 45);

        store.Save(settings);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(45, loaded.AutoSyncSeconds);
    }
}
