namespace PulseMeter.Slices.DailyUsage.Models;

public sealed record DailyUsageDisplayRow(
    string DateText,
    string TokenText,
    string MedianComparisonText,
    bool HasMedianComparison,
    double BarPercentValue,
    double SparklineHeight,
    bool HasRecordedUsage = true)
{
    public string DayDotBrush => HasRecordedUsage ? "#1F73FF" : "#D1D5DB";

    public string AccessibleSummary => !HasRecordedUsage
        ? $"{DateText}. Usage not recorded."
        : HasMedianComparison
            ? $"{DateText}. {TokenText} tokens. {MedianComparisonText}."
            : $"{DateText}. {TokenText} tokens.";
}
