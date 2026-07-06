using PulseMeter.Slices.UsageCollection;

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
        return _tracker.Refresh(nowUtc);
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
            _stateStore?.Save(_tracker.CaptureState());
        }

        return credits;
    }

    public string HeaderText(UsageSnapshot snapshot)
    {
        return snapshot.ResetCreditsAvailable switch
        {
            null => "Reset credits unavailable",
            0 => "No reset credits available",
            1 => "1 reset credit available",
            var credits => $"{credits} reset credits available"
        };
    }

    public string AvailableText(UsageSnapshot snapshot)
    {
        return snapshot.ResetCreditsAvailable is int credits
            ? $"{credits:N0} available"
            : "Unavailable";
    }
}
