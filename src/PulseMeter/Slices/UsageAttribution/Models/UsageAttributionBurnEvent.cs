namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed record UsageAttributionBurnEvent(
    string SessionDisplayName,
    string? ThreadId,
    string ProjectDisplayName,
    string ProjectPath,
    DateTimeOffset TimestampUtc,
    long RawLocalTokens,
    long EstimatedTokens,
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens);
