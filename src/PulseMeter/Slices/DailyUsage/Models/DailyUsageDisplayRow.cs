namespace PulseMeter.Slices.DailyUsage.Models;

public sealed record DailyUsageDisplayRow(
    string DateText,
    string TokenText,
    string MedianComparisonText,
    bool HasMedianComparison,
    double BarPercentValue,
    double SparklineHeight);
