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
    bool ShowCompactSeparator,
    string BucketId = "",
    string LimitKey = "",
    string ResetTimeText = "",
    string ResetCountdownText = "",
    string StatusText = "On pace",
    string StatusBrush = "#16A34A",
    string RowIconGlyph = "\uE823",
    string PaceText = "Within current pace",
    string PaceBrush = "#16A34A",
    string PaceIconGlyph = "\uE73E",
    bool HasRunwayForecast = false,
    string RowTitleText = "Usage",
    string CriticalRingArcData = "M 56 13",
    bool HasCriticalRingArc = false,
    bool IsPaceDetailVisible = true,
    double RingKnobLeft = 52.5,
    double RingKnobTop = 9.5,
    double RingKnobHaloLeft = 50,
    double RingKnobHaloTop = 7)
{
    public string CompactAccessibleSummary
    {
        get
        {
            var resetDetail = string.Join(
                " ",
                new[] { ResetTimeText, ResetCountdownText }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            var parts = new[] { UsageLimitLabel, RemainingPercentText, resetDetail, StatusText }
                .Where(value => !string.IsNullOrWhiteSpace(value));

            return string.Join(". ", parts) + ".";
        }
    }
}
