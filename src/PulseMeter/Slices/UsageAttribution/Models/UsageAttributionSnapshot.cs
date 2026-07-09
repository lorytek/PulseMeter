namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed class UsageAttributionSnapshot
{
    public static UsageAttributionSnapshot Empty { get; } = new();

    public IReadOnlyList<UsageAttributionSessionRow> Sessions { get; init; } = Array.Empty<UsageAttributionSessionRow>();

    public IReadOnlyList<UsageAttributionBurnEvent> BurnEvents { get; init; } = Array.Empty<UsageAttributionBurnEvent>();

    public long AccountWindowTokens { get; init; }

    public long RawLocalTokens { get; init; }

    public long EstimatedAttributedTokens { get; init; }

    public int WindowDays { get; init; } = 30;

    public DateTimeOffset? LastUpdatedUtc { get; init; }

    public string EvidenceText { get; init; } = "Estimated from local chats, scaled to account usage";

    public bool HasAttribution => Sessions.Count > 0 || BurnEvents.Count > 0;
}
