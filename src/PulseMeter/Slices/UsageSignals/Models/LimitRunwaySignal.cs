namespace PulseMeter.Slices.UsageSignals.Models;

public sealed record LimitRunwaySignal(
    string BucketId,
    string LimitKey,
    string WindowLabel,
    int? WindowDurationMins,
    DateTimeOffset ResetsAtUtc,
    DateTimeOffset ExhaustsAtUtc,
    TimeSpan TimeToExhaustion,
    string HintText,
    string Title,
    string Detail,
    string AccentBrush);
