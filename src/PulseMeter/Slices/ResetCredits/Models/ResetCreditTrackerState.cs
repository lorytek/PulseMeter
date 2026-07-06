namespace PulseMeter.Slices.ResetCredits.Models;

public sealed record ResetCreditTrackerState(
    bool HasObservedAvailableCount,
    int NextCreditNumber,
    IReadOnlyList<ResetCreditState> Credits);

public sealed record ResetCreditState(int Number, DateTimeOffset? ExpiresAtUtc, bool HasExactExpiry = false);
