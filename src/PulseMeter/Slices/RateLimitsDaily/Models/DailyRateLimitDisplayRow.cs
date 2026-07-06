namespace PulseMeter.Slices.RateLimitsDaily.Models;

public sealed record DailyRateLimitDisplayRow(
    string Label,
    string LabelBrush,
    string RemainingPercentText,
    double RemainingPercentValue,
    string RingBrush,
    string RingArcData);
