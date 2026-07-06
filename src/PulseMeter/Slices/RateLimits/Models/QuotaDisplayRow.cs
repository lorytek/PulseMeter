namespace PulseMeter.Slices.RateLimits.Models;

public sealed record QuotaDisplayRow(
    string Label,
    string UsageLimitLabel,
    string RemainingPercentText,
    string ResetDisplayText,
    double RemainingPercentValue,
    string CompactRemainingPercentText,
    string RingArcData,
    string RingBrush,
    string RingPercentText,
    string RingSubtitleText,
    bool IsWeekly,
    bool ShowCompactSeparator);
