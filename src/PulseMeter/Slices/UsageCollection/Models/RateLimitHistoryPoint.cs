namespace PulseMeter.Slices.UsageCollection.Models;

public sealed record RateLimitHistoryPoint(
    string LimitKey,
    int WindowDurationMins,
    double UsedPercent,
    DateTimeOffset ResetsAtUtc,
    DateTimeOffset ObservedAtUtc);
