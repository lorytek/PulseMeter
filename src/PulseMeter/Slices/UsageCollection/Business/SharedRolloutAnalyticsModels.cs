namespace PulseMeter.Slices.UsageCollection.Business;

public sealed record SharedRolloutTokenSummary(
    DateTimeOffset TimestampUtc,
    long TotalTokens,
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens,
    long? CumulativeTotalTokens);

public sealed record SharedRolloutSessionSummary(
    string ThreadId,
    string Cwd,
    DateTimeOffset? UpdatedAtUtc,
    string Title,
    IReadOnlyList<SharedRolloutTokenSummary> TokenSummaries);
