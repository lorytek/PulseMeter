namespace PulseMeter.Slices.UsageSignals.Models;

public enum LimitRunwayForecastState
{
    Learning,
    Stable,
    OnTrack,
    AtRisk,
    Exhausted
}

public enum LimitRunwayForecastConfidence
{
    Low,
    Medium,
    High
}

public sealed record LimitRunwayProjectionPoint(
    DateTimeOffset Timestamp,
    double ExpectedUsedPercent,
    double LowerUsedPercent,
    double UpperUsedPercent);

public sealed record LimitRunwayForecast(
    string BucketId,
    string LimitKey,
    string TrackLabel,
    string WindowLabel,
    int? WindowDurationMins,
    DateTimeOffset ResetsAtUtc,
    double UsedPercent,
    LimitRunwayForecastState State,
    DateTimeOffset? ExhaustsAtUtc,
    double? ProjectedRemainingAtResetPercent,
    double? PercentPerHour,
    TimeSpan? ObservationDuration,
    bool IsActionable,
    bool IsMock,
    LimitRunwayForecastConfidence Confidence = LimitRunwayForecastConfidence.Low,
    DateTimeOffset? EarliestExhaustsAtUtc = null,
    DateTimeOffset? LatestExhaustsAtUtc = null,
    int SampleCount = 1,
    double? ExhaustionProbabilityBeforeReset = null,
    IReadOnlyList<LimitRunwayProjectionPoint>? ProjectionPoints = null);
