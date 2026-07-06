namespace PulseMeter.Slices.UsageCollection.Models;

public sealed class ThreadUsageSnapshot
{
    public string? ThreadId { get; init; }

    public string? ThreadName { get; init; }

    public double? ContextUsedPercent { get; init; }

    public double? ContextLeftPercent { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? TotalTokens { get; init; }

    public DateTimeOffset? LastUpdatedUtc { get; init; }

    public bool IsExactCurrentDesktopThread { get; init; }

    public string DisplayName => !string.IsNullOrWhiteSpace(ThreadName)
        ? ThreadName!
        : !string.IsNullOrWhiteSpace(ThreadId)
            ? ThreadId!
            : "unknown thread";
}
