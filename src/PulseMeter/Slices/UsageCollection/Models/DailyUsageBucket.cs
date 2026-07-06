namespace PulseMeter.Slices.UsageCollection.Models;

public sealed class DailyUsageBucket
{
    public string? StartDate { get; init; }

    public long? Tokens { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? TotalTokens => Tokens ?? ((InputTokens ?? 0) + (OutputTokens ?? 0));
}
