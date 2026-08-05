using PulseMeter.Slices.UsageCollection;

using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Slices.ResetCredits.Business;

public interface IResetCreditsPresenter
{
    IReadOnlyList<ResetCreditListItem> Refresh(DateTimeOffset nowUtc);

    IReadOnlyList<ResetCreditListItem> Update(UsageSnapshot snapshot, DateTimeOffset nowUtc, bool shouldPersist);

    string HeaderText(UsageSnapshot snapshot);

    string AvailableText(UsageSnapshot snapshot);
}

public sealed class ResetCreditsPresenter : IResetCreditsPresenter
{
    private readonly IResetCreditStateStore? _stateStore;
    private readonly ResetCreditTracker _tracker;

    public ResetCreditsPresenter(IResetCreditStateStore? stateStore = null)
    {
        _stateStore = stateStore;
        _tracker = new ResetCreditTracker(stateStore?.Load());
    }

    public IReadOnlyList<ResetCreditListItem> Refresh(DateTimeOffset nowUtc)
    {
        var previousCount = _tracker.KnownAvailableCount;
        var credits = _tracker.Refresh(nowUtc);
        if (previousCount != _tracker.KnownAvailableCount
            && _stateStore?.Save(_tracker.CaptureState()) is false)
        {
            PrivacySafeDiagnostics.WriteInfo("expired reset-credit state could not be persisted; refreshing from service");
        }

        return credits;
    }

    public IReadOnlyList<ResetCreditListItem> Update(UsageSnapshot snapshot, DateTimeOffset nowUtc, bool shouldPersist)
    {
        var credits = _tracker.Update(
            snapshot.ResetCreditsAvailable,
            snapshot.ResetCreditsExpiresAtUtc,
            snapshot.ResetCredits,
            nowUtc);

        if (shouldPersist)
        {
            if (_stateStore?.Save(_tracker.CaptureState()) is false)
            {
                PrivacySafeDiagnostics.WriteInfo("reset-credit state could not be persisted; refreshing from service");
            }
        }

        return credits;
    }

    public string HeaderText(UsageSnapshot snapshot)
    {
        if (snapshot.ResetCreditsAvailable is null
            && _tracker.KnownAvailableCount is int lastKnownCount)
        {
            return lastKnownCount switch
            {
                0 => "Last known: no reset credits available",
                1 => "Last known: 1 reset credit available",
                _ => $"Last known: {lastKnownCount} reset credits available"
            };
        }

        var availableCount = _tracker.KnownAvailableCount ?? snapshot.ResetCreditsAvailable;
        return availableCount switch
        {
            null => "Reset credits unavailable",
            0 => "No reset credits available",
            1 => "1 reset credit available",
            var credits => $"{credits} reset credits available"
        };
    }

    public string AvailableText(UsageSnapshot snapshot)
    {
        if (snapshot.ResetCreditsAvailable is null
            && _tracker.KnownAvailableCount is int lastKnownCount)
        {
            return $"{lastKnownCount:N0} last known";
        }

        var availableCount = _tracker.KnownAvailableCount ?? snapshot.ResetCreditsAvailable;
        return availableCount is int credits
            ? $"{credits:N0} available"
            : "Unavailable";
    }
}
