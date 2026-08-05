using System.IO;
using System.Text.Json;

namespace PulseMeter.Platform.Persistence;

public sealed record PulseMeterWindowState(
    bool IsExpanded,
    double Width,
    double Height,
    double? Left = null,
    double? Top = null);

public interface IPulseMeterWindowStateStore
{
    PulseMeterWindowState? Load();

    bool Save(PulseMeterWindowState state);
}

public sealed class PulseMeterWindowStateStore : IPulseMeterWindowStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public PulseMeterWindowStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseMeter",
            "window-state.json");
    }

    public PulseMeterWindowState? Load()
    {
        return AtomicJsonFileStore.Load<PulseMeterWindowState>(_filePath, JsonOptions);
    }

    public bool Save(PulseMeterWindowState state)
    {
        return AtomicJsonFileStore.Save(_filePath, state, JsonOptions);
    }
}
