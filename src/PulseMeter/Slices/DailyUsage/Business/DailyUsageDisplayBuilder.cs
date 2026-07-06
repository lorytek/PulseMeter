using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.DailyUsage.Business;

public sealed record DailyUsageMedianBaseline(
    double MedianTokens,
    int CompletedDayCount,
    DateOnly StartDate,
    int WindowDays);

public sealed record DailyUsageDisplayResult(
    IReadOnlyList<DailyUsageDisplayRow> Rows,
    DailyUsageMedianBaseline? MedianBaseline);

internal static class DailyUsageDisplayBuilder
{
    private const int DailyUsageDisplayDays = 7;
    private const int DailyUsageMedianWindowDays = 30;

    public static DailyUsageDisplayResult BuildRows(IReadOnlyList<DailyUsageBucket> buckets, DateOnly today)
    {
        var datedBuckets = buckets
            .Select(bucket => new
            {
                Bucket = bucket,
                Date = DateOnly.TryParse(bucket.StartDate, out var date) ? date : DateOnly.MinValue
            })
            .ToList();

        var rows = new List<DailyUsageAggregateRow>();
        var medianRows = new List<DailyUsageAggregateRow>();
        if (datedBuckets.Count > 0)
        {
            foreach (var offset in Enumerable.Range(0, DailyUsageDisplayDays))
            {
                var date = today.AddDays(-offset);
                var tokens = datedBuckets
                    .Where(row => row.Date == date)
                    .Sum(row => TotalTokens(row.Bucket));
                rows.Add(new DailyUsageAggregateRow(date, tokens));
            }

            foreach (var offset in Enumerable.Range(0, DailyUsageMedianWindowDays))
            {
                var date = today.AddDays(-offset);
                var tokens = datedBuckets
                    .Where(row => row.Date == date)
                    .Sum(row => TotalTokens(row.Bucket));
                medianRows.Add(new DailyUsageAggregateRow(date, tokens));
            }
        }

        var medianBaseline = GetRecentDailyTokenMedian(medianRows, today);
        var maxTokens = rows.Count == 0 ? 0 : rows.Max(row => row.Tokens);
        var displayRows = rows
            .Select(row =>
            {
                var totalTokens = row.Tokens;
                var percent = maxTokens > 0 ? Math.Clamp(totalTokens / (double)maxTokens * 100, 0, 100) : 0;
                var sparklineHeight = percent <= 0 ? 4 : Math.Clamp(6 + (percent / 100 * 22), 6, 28);

                return new DailyUsageDisplayRow(
                    FormatDailyUsageDateText(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), today),
                    MeterDisplayFormatter.FormatTokens(totalTokens),
                    FormatMedianComparisonText(totalTokens, medianBaseline),
                    medianBaseline is not null,
                    percent,
                    sparklineHeight);
            })
            .ToList();

        return new DailyUsageDisplayResult(displayRows, medianBaseline);
    }

    public static string FormatMedianSummaryText(DailyUsageMedianBaseline? baseline)
    {
        if (baseline is null)
        {
            return string.Empty;
        }

        var medianText = MeterDisplayFormatter.FormatTokens((long)Math.Round(baseline.MedianTokens));
        return $"{baseline.WindowDays}-day median per day: {medianText}";
    }

    public static double CalculateTodayMedianPercentValue(long? todayTokens, DailyUsageMedianBaseline? baseline)
    {
        if (todayTokens is not long tokens
            || baseline is null
            || baseline.MedianTokens <= 0)
        {
            return 0;
        }

        return Math.Clamp(tokens / baseline.MedianTokens * 100, 0, 100);
    }

    public static string FormatTodayMedianPercentText(long? todayTokens, DailyUsageMedianBaseline? baseline)
    {
        if (todayTokens is null
            || baseline is null
            || baseline.MedianTokens <= 0)
        {
            return "Waiting for daily usage";
        }

        return $"{CalculateTodayMedianPercentValue(todayTokens, baseline):0.0}% of 30-day median";
    }

    public static long? GetTodayTokens(UsageSnapshot snapshot, DateOnly today)
    {
        if (snapshot.DailyBuckets.Count == 0)
        {
            return null;
        }

        var todayBuckets = snapshot.DailyBuckets
            .Where(bucket => IsLocalDateBucket(bucket, today))
            .ToList();

        return todayBuckets.Count == 0
            ? null
            : todayBuckets.Sum(TotalTokens);
    }

    private static bool IsLocalDateBucket(DailyUsageBucket bucket, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(bucket.StartDate))
        {
            return false;
        }

        return DateOnly.TryParse(bucket.StartDate, out var bucketDate)
            && bucketDate == date;
    }

    private static long TotalTokens(DailyUsageBucket bucket)
    {
        return bucket.TotalTokens ?? 0;
    }

    private static DailyUsageMedianBaseline? GetRecentDailyTokenMedian(
        IReadOnlyList<DailyUsageAggregateRow> rows,
        DateOnly today)
    {
        var baselineRows = rows
            .Where(row => row.Date < today && row.Tokens > 0)
            .ToList();

        var values = baselineRows
            .Select(row => (double)row.Tokens)
            .Order()
            .ToList();

        if (values.Count == 0)
        {
            return null;
        }

        var middle = values.Count / 2;
        var medianTokens = values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;

        var firstDate = baselineRows.Min(row => row.Date);
        return new DailyUsageMedianBaseline(medianTokens, values.Count, firstDate, DailyUsageMedianWindowDays);
    }

    private static string FormatMedianComparisonText(long tokens, DailyUsageMedianBaseline? baseline)
    {
        if (baseline is null || baseline.MedianTokens <= 0)
        {
            return string.Empty;
        }

        var percent = (tokens - baseline.MedianTokens) / baseline.MedianTokens * 100;
        if (Math.Abs(percent) < 0.5)
        {
            return "near median";
        }

        return $"{percent:+0;-0}% vs median";
    }

    private static string FormatDailyUsageDateText(string? startDate, DateOnly today)
    {
        if (!DateOnly.TryParse(startDate, out var date))
        {
            return string.IsNullOrWhiteSpace(startDate) ? "Unknown" : startDate;
        }

        if (date == today)
        {
            return "Today";
        }

        return date.ToDateTime(TimeOnly.MinValue).ToString("dddd", CultureInfo.InvariantCulture);
    }

    private sealed record DailyUsageAggregateRow(DateOnly Date, long Tokens);
}
