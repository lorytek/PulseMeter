using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Shared.RateLimits;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimits.Business;

internal static class QuotaDisplayBuilder
{
    private const int WeeklyWindowMinutes = 10_080;

    public static IReadOnlyList<QuotaDisplayRow> BuildQuotaRows(IEnumerable<RateLimitBucket> buckets, DateTimeOffset now)
    {
        return buckets.Select(bucket => ToQuotaDisplayRow(bucket, now)).ToList();
    }

    public static IReadOnlyList<QuotaDisplayRow> BuildCompactRows(IEnumerable<QuotaDisplayRow> rows)
    {
        return rows
            .Take(2)
            .Select((row, index) => row with { ShowCompactSeparator = index > 0 })
            .ToList();
    }

    public static string LimitKey(RateLimitBucket bucket)
    {
        return RateLimitBucketKeys.Get(bucket);
    }

    public static string SanitizeDisplayLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "General";
        }

        var sanitized = label
            .Replace("-Codex-", "-", StringComparison.OrdinalIgnoreCase)
            .Replace(" Codex ", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("Codex-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-Codex", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Codex ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" Codex", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Codex", "Monitor", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "General" : sanitized;
    }

    public static int LimitOptionSortPriority(RateLimitOption option)
    {
        return option.Key.Equals("codex", StringComparison.OrdinalIgnoreCase)
            || option.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1;
    }

    private static QuotaDisplayRow ToQuotaDisplayRow(RateLimitBucket bucket, DateTimeOffset now)
    {
        var isWeekly = IsWeeklyWindow(bucket);
        var label = isWeekly ? "Weekly" : bucket.WindowLabel;
        var usageLimitLabel = isWeekly ? "Weekly usage limit" : ToUsageLimitLabel(bucket);

        return new QuotaDisplayRow(
            label,
            usageLimitLabel,
            bucket.RemainingPercentText,
            FormatCompactReset(bucket, now),
            bucket.RemainingPercentValue,
            isWeekly ? bucket.RemainingPercentText : MeterDisplayFormatter.FormatWholePercent(bucket.RemainingPercentValue),
            MeterDisplayFormatter.CreateRingArcData(bucket.RemainingPercentValue),
            CreateQuotaRingBrush(bucket),
            MeterDisplayFormatter.FormatWholePercent(bucket.RemainingPercentValue),
            isWeekly ? "7-Day Usage" : $"{label} Window",
            isWeekly,
            false);
    }

    private static string FormatCompactReset(RateLimitBucket bucket, DateTimeOffset now)
    {
        var resetAtUtc = bucket.ResetsAtUtc
            ?? (bucket.ResetsAtUnixSeconds is long unix ? DateTimeOffset.FromUnixTimeSeconds(unix) : null);

        if (resetAtUtc is DateTimeOffset resetAt)
        {
            var localReset = resetAt.ToLocalTime();
            return IsShortWindow(bucket)
                ? localReset.ToString("h:mm tt", CultureInfo.InvariantCulture)
                : localReset.ToString("ddd h:mm tt", CultureInfo.InvariantCulture);
        }

        return bucket.ResetCountdown == "reset unknown" ? "unknown" : bucket.ResetCountdown;
    }

    private static bool IsShortWindow(RateLimitBucket bucket)
    {
        if (bucket.WindowDurationMins is int mins)
        {
            return mins < 1_440;
        }

        return bucket.WindowLabel.Contains('h', StringComparison.OrdinalIgnoreCase)
            && !bucket.WindowLabel.Contains('d', StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateQuotaRingBrush(RateLimitBucket bucket)
    {
        if (bucket.UsedPercent is not double)
        {
            return "#9CA3AF";
        }

        var remainingRatio = Math.Clamp(bucket.RemainingPercentValue / 100, 0, 1);

        return remainingRatio >= 0.5
            ? MeterDisplayFormatter.InterpolateHexColor("#F59E0B", "#22C55E", (remainingRatio - 0.5) * 2)
            : MeterDisplayFormatter.InterpolateHexColor("#EF4444", "#F59E0B", remainingRatio * 2);
    }

    private static string ToUsageLimitLabel(RateLimitBucket bucket)
    {
        if (bucket.WindowDurationMins is int mins && mins > 0 && mins % 60 == 0)
        {
            var hours = mins / 60;
            return hours == 1 ? "1 hour usage limit" : $"{hours} hour usage limit";
        }

        return $"{bucket.WindowLabel} usage limit";
    }

    private static bool IsWeeklyWindow(RateLimitBucket bucket)
    {
        if (bucket.WindowDurationMins is int mins)
        {
            return mins >= WeeklyWindowMinutes;
        }

        return bucket.WindowLabel.Contains('d', StringComparison.OrdinalIgnoreCase);
    }

}
