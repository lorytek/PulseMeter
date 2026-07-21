namespace PulseMeter.Slices.UsageSignals.Models;

public sealed record LimitUsagePoint(
    DateTimeOffset ObservedAtUtc,
    double UsedPercent);

public sealed record LimitUsageGap(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc);

public sealed record LimitUsageTrend(
    string BucketId,
    string LimitKey,
    string TrackLabel,
    string WindowLabel,
    int? WindowDurationMins,
    DateTimeOffset ResetsAtUtc,
    IReadOnlyList<LimitUsagePoint> Points,
    bool IsMock)
{
    public IReadOnlyList<LimitUsageGap> MeasurementGaps { get; init; } = [];
}
