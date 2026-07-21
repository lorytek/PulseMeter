using System.IO;
using System.Text.Json;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.ResetCredits;

namespace PulseMeter.Slices.ResetCredits.Business;

public interface IResetCreditStateStore
{
    ResetCreditTrackerState? Load();

    void Save(ResetCreditTrackerState state);
}

public sealed class ResetCreditStateStore : IResetCreditStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public ResetCreditStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseMeter",
            "reset-credits.json");
    }

    public ResetCreditTrackerState? Load()
    {
        return AtomicJsonFileStore.Load<ResetCreditTrackerState>(_filePath, JsonOptions);
    }

    public void Save(ResetCreditTrackerState state)
    {
        AtomicJsonFileStore.Save(_filePath, state, JsonOptions);
    }
}
