using PulseMeter.Platform.Persistence;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowStateStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsExpandedWindowSizeAndPosition()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"), "window-state.json");
        var store = new PulseMeterWindowStateStore(path);
        var state = new PulseMeterWindowState(IsExpanded: true, Width: 760, Height: 640, Left: 32, Top: 48);

        store.Save(state);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.True(loaded.IsExpanded);
        Assert.Equal(760, loaded.Width);
        Assert.Equal(640, loaded.Height);
        Assert.Equal(32, loaded.Left);
        Assert.Equal(48, loaded.Top);
    }
}
