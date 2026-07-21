using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimitsDaily.Business;

internal static class RateLimitsDailyDisplayBuilder
{
    private const int DailyRateLimitSliceCount = 7;
    private const int WeeklyWindowMinutes = 10_080;

    public static IReadOnlyList<DailyRateLimitDisplayRow> BuildRows(
        IEnumerable<RateLimitBucket> selectedBuckets,
        DateTimeOffset now)
    {
        var rows = new List<DailyRateLimitDisplayRow>();
        var weeklyBucket = GetWeeklyBucket(selectedBuckets);
        if (weeklyBucket?.UsedPercent is not double weeklyUsedPercent)
        {
            return rows;
        }

        var clampedWeeklyUsed = Math.Clamp(weeklyUsedPercent, 0, 100);
        var sliceSize = 100.0 / DailyRateLimitSliceCount;
        var activeDayIndex = GetCalendarDayIndex(weeklyBucket, clampedWeeklyUsed, sliceSize, now);

        for (var index = 0; index < DailyRateLimitSliceCount; index++)
        {
            var sliceStart = index * sliceSize;
            var consumedPercent = Math.Round(
                Math.Clamp((clampedWeeklyUsed - sliceStart) / sliceSize, 0, 1) * 100,
                2);
            var remainingPercent = Math.Round(100 - consumedPercent, 2);
            rows.Add(new DailyRateLimitDisplayRow(
                GetWeekdayLabel(weeklyBucket, index, activeDayIndex, now),
                index == activeDayIndex ? "#1F73FF" : "#6B7280",
                MeterDisplayFormatter.FormatWholePercent(remainingPercent),
                remainingPercent,
                CreateDailyRateLimitRingBrush(remainingPercent),
                MeterDisplayFormatter.CreateRingArcData(remainingPercent)));
        }

        return rows;
    }

    public static string BuildWarningText(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        var weeklyBucket = GetWeeklyBucket(selectedBuckets);
        if (weeklyBucket?.UsedPercent is not double weeklyUsedPercent)
        {
            return string.Empty;
        }

        var clampedWeeklyUsed = Math.Clamp(weeklyUsedPercent, 0, 100);
        var sliceSize = 100.0 / DailyRateLimitSliceCount;
        if (TryGetWeeklyWindowTiming(weeklyBucket, now, out var elapsed, out var duration))
        {
            if (elapsed >= duration)
            {
                return string.Empty;
            }

            if (clampedWeeklyUsed >= 100)
            {
                var resetWait = duration - elapsed;
                return $"Daily allowance exceeded; weekly allowance is fully consumed. Wait {FormatPaceWaitDuration(resetWait)} to get back on pace.";
            }

            var timedAllowanceDay = GetCalendarDayIndex(weeklyBucket, clampedWeeklyUsed, sliceSize, now) + 1;
            if (clampedWeeklyUsed <= timedAllowanceDay * sliceSize)
            {
                return string.Empty;
            }

            var futureAllowanceDay = Math.Min(
                DailyRateLimitSliceCount,
                (int)Math.Floor(clampedWeeklyUsed / sliceSize) + 1);
            var waitTicks = (duration.Ticks / DailyRateLimitSliceCount * (futureAllowanceDay - 1)) - elapsed.Ticks;
            if (waitTicks <= 0)
            {
                return string.Empty;
            }

            var wait = TimeSpan.FromTicks(waitTicks);
            return $"Daily allowance exceeded; using Day {futureAllowanceDay} allowance early. Wait {FormatPaceWaitDuration(wait)} to get back on pace.";
        }

        if (clampedWeeklyUsed <= sliceSize)
        {
            return string.Empty;
        }

        if (clampedWeeklyUsed >= 100)
        {
            return "Daily allowance exceeded; weekly allowance is fully consumed.";
        }

        var currentAllowanceDay = Math.Min(DailyRateLimitSliceCount, (int)Math.Floor(clampedWeeklyUsed / sliceSize) + 1);
        return $"Daily allowance exceeded; now consuming Day {currentAllowanceDay}.";
    }

    private static RateLimitBucket? GetWeeklyBucket(IEnumerable<RateLimitBucket> buckets)
    {
        return buckets.FirstOrDefault(IsWeeklyWindow);
    }

    private static bool IsWeeklyWindow(RateLimitBucket bucket)
    {
        if (bucket.WindowDurationMins is int mins)
        {
            return mins >= WeeklyWindowMinutes;
        }

        return bucket.WindowLabel.Contains('d', StringComparison.OrdinalIgnoreCase);
    }

    private static int GetCalendarDayIndex(
        RateLimitBucket weeklyBucket,
        double clampedWeeklyUsed,
        double sliceSize,
        DateTimeOffset now)
    {
        if (TryGetWeeklyWindowTiming(weeklyBucket, now, out var elapsed, out var duration))
        {
            var dayTicks = Math.Max(1, duration.Ticks / DailyRateLimitSliceCount);
            return Math.Clamp((int)(elapsed.Ticks / dayTicks), 0, DailyRateLimitSliceCount - 1);
        }

        return Math.Min(DailyRateLimitSliceCount - 1, (int)Math.Floor(clampedWeeklyUsed / sliceSize));
    }

    private static string GetWeekdayLabel(
        RateLimitBucket weeklyBucket,
        int index,
        int activeDayIndex,
        DateTimeOffset now)
    {
        var resetAtUtc = GetResetAtUtc(weeklyBucket);
        var duration = TimeSpan.FromMinutes(weeklyBucket.WindowDurationMins ?? WeeklyWindowMinutes);
        if (resetAtUtc is not null && duration > TimeSpan.Zero)
        {
            var sliceTicks = Math.Max(1, duration.Ticks / DailyRateLimitSliceCount);
            var sliceStartUtc = resetAtUtc.Value - duration + TimeSpan.FromTicks(sliceTicks * index);
            return FormatCalendarDayLabel(sliceStartUtc, now);
        }

        return FormatCalendarDayLabel(now.AddDays(index - activeDayIndex), now);
    }

    private static string FormatCalendarDayLabel(DateTimeOffset day, DateTimeOffset now)
    {
        var localDay = day.ToLocalTime();
        return localDay.Date == now.ToLocalTime().Date
            ? "Today"
            : localDay.ToString("dddd", CultureInfo.CurrentCulture);
    }

    private static bool TryGetWeeklyWindowTiming(
        RateLimitBucket weeklyBucket,
        DateTimeOffset now,
        out TimeSpan elapsed,
        out TimeSpan duration)
    {
        elapsed = TimeSpan.Zero;
        duration = TimeSpan.Zero;

        var resetAtUtc = GetResetAtUtc(weeklyBucket);
        if (resetAtUtc is null)
        {
            return false;
        }

        duration = TimeSpan.FromMinutes(weeklyBucket.WindowDurationMins ?? WeeklyWindowMinutes);
        if (duration <= TimeSpan.Zero)
        {
            return false;
        }

        var remaining = resetAtUtc.Value - now;
        elapsed = duration - remaining;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }
        else if (elapsed > duration)
        {
            elapsed = duration;
        }

        return true;
    }

    private static DateTimeOffset? GetResetAtUtc(RateLimitBucket weeklyBucket)
    {
        return weeklyBucket.ResetsAtUtc
            ?? (weeklyBucket.ResetsAtUnixSeconds is long unix ? DateTimeOffset.FromUnixTimeSeconds(unix) : null);
    }

    private static string FormatPaceWaitDuration(TimeSpan wait)
    {
        if (wait.TotalDays >= 1)
        {
            return $"{(int)wait.TotalDays}d {wait.Hours}h {wait.Minutes:00}m";
        }

        if (wait.TotalHours >= 1)
        {
            return $"{(int)wait.TotalHours}h {wait.Minutes:00}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))}m";
    }

    private static string CreateDailyRateLimitRingBrush(double remainingPercent)
    {
        var remainingRatio = Math.Clamp(remainingPercent / 100, 0, 1);
        return MeterDisplayFormatter.InterpolateHexColor("#EF4444", "#1F73FF", remainingRatio);
    }
}
