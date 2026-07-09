namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed record UsageAttributionSessionRow(
    string DisplayName,
    string? ThreadId,
    string ProjectDisplayName,
    string ProjectPath,
    long RawLocalTokens,
    long EstimatedTokens,
    double SharePercent,
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningTokens,
    DateTimeOffset? ThreadUpdatedAtUtc,
    DateTimeOffset? LastEventAtUtc);
