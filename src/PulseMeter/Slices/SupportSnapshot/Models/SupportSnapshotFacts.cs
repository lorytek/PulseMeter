namespace PulseMeter.Slices.SupportSnapshot.Models;

public enum SupportReaderState { NotObserved, Live, Stale, Unavailable, Mock }

/// <summary>Allowlisted local-reader facts only; this record intentionally has no raw snapshot or string data.</summary>
public sealed record SupportSnapshotFacts(
    SupportReaderState ReaderState,
    DateTimeOffset? LastUpdatedUtc,
    bool HasRateLimitData,
    bool HasAccountUsageData,
    bool HasLocalProjectHistory)
{
    public static SupportSnapshotFacts NotObserved { get; } = new(
        SupportReaderState.NotObserved,
        null,
        false,
        false,
        false);
}
