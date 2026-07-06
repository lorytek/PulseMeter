using System.Globalization;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.AccountUsage.Business;

internal static class AccountUsageDisplayBuilder
{
    public static string SummaryText(UsageSnapshot snapshot)
    {
        var parts = new List<string>();

        if (snapshot.LifetimeTokens is long lifetime)
        {
            parts.Add($"Lifetime {MeterDisplayFormatter.FormatTokens(lifetime)}");
        }

        if (snapshot.PeakDailyTokens is long peak)
        {
            parts.Add($"Peak day {MeterDisplayFormatter.FormatTokens(peak)}");
        }

        if (snapshot.CurrentStreakDays is int currentStreak)
        {
            parts.Add($"Streak {currentStreak}d");
        }

        return parts.Count == 0 ? "Usage summary unavailable." : string.Join(" - ", parts);
    }

    public static string DailyFreshnessWarningText(UsageSnapshot snapshot)
    {
        return snapshot.DailyBuckets.Count == 0
            ? "Daily totals unavailable."
            : "Daily total delayed";
    }

    public static string FreshnessWarningText(bool hasAccountSummary)
    {
        return hasAccountSummary
            ? "Today's usage is not available yet."
            : "Account summary unavailable.";
    }

    public static string TodayUsageText(UsageSnapshot snapshot, DateOnly today)
    {
        var value = TodayUsageValueText(snapshot, today);
        return value == "Unavailable"
            ? "Today used tokens: unavailable"
            : $"Today used tokens: {value}";
    }

    public static string TodayUsageMetricValueText(UsageSnapshot snapshot, DateOnly today)
    {
        return GetTodayTokens(snapshot, today) is long todayTokens
            ? MeterDisplayFormatter.FormatTokens(todayTokens)
            : "--";
    }

    public static string TodayUsageValueText(UsageSnapshot snapshot, DateOnly today)
    {
        return GetTodayTokens(snapshot, today) is long todayTokens
            ? $"{MeterDisplayFormatter.FormatTokens(todayTokens)} tokens"
            : "Unavailable";
    }

    public static string LifetimeUsageValueText(UsageSnapshot snapshot)
    {
        return MeterDisplayFormatter.FormatMetricTokens(snapshot.LifetimeTokens);
    }

    public static string PeakUsageValueText(UsageSnapshot snapshot)
    {
        return MeterDisplayFormatter.FormatMetricTokens(snapshot.PeakDailyTokens);
    }

    public static string StreakDaysValueText(UsageSnapshot snapshot)
    {
        return snapshot.CurrentStreakDays?.ToString("N0", CultureInfo.InvariantCulture) ?? "--";
    }

    public static string LifetimeUsageCaptionText(UsageSnapshot snapshot)
    {
        return snapshot.LifetimeTokens is null ? "Lifetime unavailable" : "All time total";
    }

    public static string PeakUsageCaptionText(UsageSnapshot snapshot)
    {
        if (snapshot.PeakDailyTokens is not long peak)
        {
            return "Peak unavailable";
        }

        var peakDate = snapshot.DailyBuckets
            .Where(bucket => (bucket.TotalTokens ?? 0) == peak)
            .Select(bucket => bucket.StartDate)
            .FirstOrDefault();

        if (DateOnly.TryParse(peakDate, out var date))
        {
            return date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        }

        return "Peak day total";
    }

    public static string StreakCaptionText(UsageSnapshot snapshot)
    {
        return snapshot.CurrentStreakDays is null ? "Streak unavailable" : "Current streak";
    }

    public static double TodayMedianDailyPercentValue(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today)
    {
        return DailyUsageDisplayBuilder.CalculateTodayMedianPercentValue(GetTodayTokens(snapshot, today), medianBaseline);
    }

    public static string TodayMedianDailyPercentText(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today)
    {
        return DailyUsageDisplayBuilder.FormatTodayMedianPercentText(GetTodayTokens(snapshot, today), medianBaseline);
    }

    public static bool HasAccountSummary(UsageSnapshot snapshot)
    {
        return snapshot.LifetimeTokens is not null
            || snapshot.PeakDailyTokens is not null
            || snapshot.CurrentStreakDays is not null;
    }

    public static long? GetTodayTokens(UsageSnapshot snapshot, DateOnly today)
    {
        return DailyUsageDisplayBuilder.GetTodayTokens(snapshot, today);
    }
}
