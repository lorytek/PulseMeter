namespace PulseMeter.Slices.UsageSignals.Models;

public sealed record LimitUsagePoint(
    DateTimeOffset ObservedAtUtc,
    double UsedPercent);

public sealed record LimitUsageGap(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc);

/// <summary>The privacy-safe evidence that qualifies one measured hour for usage momentum.</summary>
public enum HourlyActivityEvidence
{
    None,
    LocalEvent,
    QuotaMovement,
    Both
}

public enum LocalActivityCoverage
{
    Available,
    Partial,
    Unavailable
}

/// <summary>A completed, gap-free hour retained for a privacy-safe pace baseline.</summary>
public sealed record LimitHourlyUsageRate(
    DateTimeOffset HourStartedAtUtc,
    double PercentPerHour,
    HourlyActivityEvidence ActivityEvidence = HourlyActivityEvidence.QuotaMovement);

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

    public IReadOnlyList<LimitHourlyUsageRate> BaselineHourlyRates { get; init; } = [];

    public HourlyActivityEvidence CurrentHourActivityEvidence { get; init; } = HourlyActivityEvidence.QuotaMovement;

    public LocalActivityCoverage ActivityCoverage { get; init; } = LocalActivityCoverage.Unavailable;

    public IReadOnlyList<DateTimeOffset> LocalActivityUtcHours { get; init; } = [];
}
