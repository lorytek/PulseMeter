namespace PulseMeter.Slices.UsageSignals.Models;

public sealed record IdleDrainIncident(
    string BucketId,
    string LimitKey,
    string WindowLabel,
    int? WindowDurationMins,
    DateTimeOffset ResetsAtUtc,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    double BeforeUsedPercent,
    double AfterUsedPercent,
    TimeSpan ObservationDuration,
    TimeSpan IdleDuration,
    string SummaryText,
    string DetailText,
    string DiagnosticText,
    string AccentBrush);
