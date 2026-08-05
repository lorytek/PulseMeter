using System.IO;
using System.Text.Json;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.ResetCredits;

namespace PulseMeter.Slices.ResetCredits.Business;

public interface IResetCreditStateStore
{
    ResetCreditTrackerState? Load();

    bool Save(ResetCreditTrackerState state);
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
        return AtomicJsonFileStore.Load<ResetCreditTrackerState>(_filePath, JsonOptions, IsValidState);
    }

    public bool Save(ResetCreditTrackerState state)
    {
        return AtomicJsonFileStore.Save(_filePath, state, JsonOptions);
    }

    private static bool IsValidState(ResetCreditTrackerState state)
    {
        return state.NextCreditNumber is >= 1 and <= ResetCreditTracker.MaximumPersistedCreditNumber
            && state.Credits is { Count: <= ResetCreditTracker.MaximumPersistedCredits }
            && state.Credits.All(credit => credit is { Number: > 0 } and { Number: <= ResetCreditTracker.MaximumPersistedCreditNumber });
    }
}
