using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Shared.RateLimits;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageSignals.Models;

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

    public static QuotaDisplayRow ApplyRunwayForecast(QuotaDisplayRow row, LimitRunwaySignal? signal)
    {
        if (signal is null || row.IsWeekly)
        {
            return row;
        }

        return row with
        {
            PaceText = CreateRunwayPaceText(signal),
            PaceBrush = signal.AccentBrush,
            PaceIconGlyph = "\uE7BA",
            HasRunwayForecast = true
        };
    }

    public static QuotaDisplayRow ApplyWeeklyPace(
        QuotaDisplayRow row,
        bool isAheadOfPace,
        string paceDetailText)
    {
        if (!row.IsWeekly || !isAheadOfPace)
        {
            return row;
        }

        return row with
        {
            RingBrush = "#F59E0B",
            StatusText = "Ahead of pace",
            StatusBrush = "#D97706",
            PaceText = string.IsNullOrWhiteSpace(paceDetailText)
                ? "Weekly usage is ahead of pace"
                : paceDetailText,
            PaceBrush = "#D97706",
            PaceIconGlyph = "\uE7BA"
        };
    }

    private static QuotaDisplayRow ToQuotaDisplayRow(RateLimitBucket bucket, DateTimeOffset now)
    {
        var isWeekly = IsWeeklyWindow(bucket);
        var label = isWeekly ? "Weekly" : bucket.WindowLabel;
        var usageLimitLabel = isWeekly ? "Weekly usage limit" : ToUsageLimitLabel(bucket);
        var resetDisplayText = FormatCompactReset(bucket, now);
        var (statusText, statusBrush) = CreateStatus(bucket);
        var ringEnd = CreateGaugeEndPoint(bucket.RemainingPercentValue);

        return new QuotaDisplayRow(
            label,
            usageLimitLabel,
            bucket.RemainingPercentText,
            resetDisplayText,
            bucket.RemainingPercentValue,
            isWeekly ? bucket.RemainingPercentText : MeterDisplayFormatter.FormatWholePercent(bucket.RemainingPercentValue),
            CreateGaugeArcData(0, bucket.RemainingPercentValue),
            CreateQuotaRingBrush(bucket, statusText),
            MeterDisplayFormatter.FormatWholePercent(bucket.RemainingPercentValue),
            isWeekly ? "7-Day Usage" : $"{label} Window",
            isWeekly,
            false,
            CreateBucketId(bucket),
            LimitKey(bucket),
            CreateResetTimeText(bucket, resetDisplayText),
            CreateResetCountdownText(bucket),
            statusText,
            statusBrush,
            isWeekly ? "\uE787" : "\uE823",
            CreatePaceText(isWeekly, statusText),
            statusBrush,
            statusText is "Critical" or "Warning" ? "\uE7BA" : "\uE73E",
            false,
            CreateRowTitle(bucket, isWeekly, label),
            CreateCriticalGaugeArcData(bucket, statusText),
            statusText == "Critical" && bucket.RemainingPercentValue > 0,
            !isWeekly,
            ringEnd.X - 3.5,
            ringEnd.Y - 3.5,
            ringEnd.X - 6,
            ringEnd.Y - 6);
    }

    private static string CreateCriticalGaugeArcData(RateLimitBucket bucket, string statusText)
    {
        if (statusText != "Critical")
        {
            return CreateGaugeArcData(0, 0);
        }

        var remaining = bucket.RemainingPercentValue;
        return CreateGaugeArcData(Math.Max(0, remaining - 2), remaining);
    }

    private static string CreateGaugeArcData(double startPercent, double endPercent)
    {
        const double center = 56;
        const double radius = 43;
        const double startAngle = -90;
        const double totalSweep = 360;

        var normalizedStart = Math.Clamp(startPercent, 0, 100);
        var normalizedEnd = Math.Clamp(endPercent, normalizedStart, 99.9);
        var start = startAngle + (normalizedStart / 100 * totalSweep);
        var end = startAngle + (normalizedEnd / 100 * totalSweep);
        var startRadians = start * Math.PI / 180;
        var endRadians = end * Math.PI / 180;
        var startX = center + (radius * Math.Cos(startRadians));
        var startY = center + (radius * Math.Sin(startRadians));

        if (normalizedEnd <= normalizedStart)
        {
            return string.Create(CultureInfo.InvariantCulture, $"M {startX:0.###} {startY:0.###}");
        }

        var endX = center + (radius * Math.Cos(endRadians));
        var endY = center + (radius * Math.Sin(endRadians));
        var largeArc = (normalizedEnd - normalizedStart) / 100 * totalSweep > 180 ? 1 : 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {startX:0.###} {startY:0.###} A {radius:0.###} {radius:0.###} 0 {largeArc} 1 {endX:0.###} {endY:0.###}");
    }

    private static (double X, double Y) CreateGaugeEndPoint(double percent)
    {
        const double center = 56;
        const double radius = 43;
        var normalized = Math.Clamp(percent, 0, 99.9);
        var angle = (-90 + (normalized / 100 * 360)) * Math.PI / 180;
        return (
            center + (radius * Math.Cos(angle)),
            center + (radius * Math.Sin(angle)));
    }

    private static string CreateRunwayPaceText(LimitRunwaySignal signal)
    {
        var leadTime = signal.ResetsAtUtc - signal.ExhaustsAtUtc;
        return leadTime > TimeSpan.Zero
            ? $"May run out {FormatDuration(leadTime)} before reset"
            : "May run out before reset";
    }

    private static string CreateRowTitle(RateLimitBucket bucket, bool isWeekly, string label)
    {
        if (isWeekly)
        {
            return "Weekly";
        }

        return !string.IsNullOrWhiteSpace(bucket.Label)
            && !bucket.Label.Equals("Usage", StringComparison.OrdinalIgnoreCase)
                ? bucket.Label
                : $"{label} Window";
    }

    private static string CreateBucketId(RateLimitBucket bucket)
    {
        var duration = bucket.WindowDurationMins?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        return $"{LimitKey(bucket)}|{duration}";
    }

    private static (string Text, string Brush) CreateStatus(RateLimitBucket bucket)
    {
        if (bucket.UsedPercent is not double)
        {
            return ("Unavailable", "#6B7280");
        }

        var remaining = bucket.RemainingPercentValue;
        return remaining <= 5
            ? ("Critical", "#DC2626")
            : remaining <= 10
                ? ("Warning", "#D97706")
                : ("On pace", "#16A34A");
    }

    private static string CreatePaceText(bool isWeekly, string statusText)
    {
        return statusText switch
        {
            "Critical" => "Limit nearly exhausted",
            "Warning" => "Use is above pace",
            "Unavailable" => "Pace unavailable",
            _ => isWeekly ? "Within weekly pace" : "Within current pace"
        };
    }

    private static string CreateResetTimeText(RateLimitBucket bucket, string resetDisplayText)
    {
        return bucket.ResetCountdown == "reset unknown"
            ? "Reset time unknown"
            : $"Resets {resetDisplayText}";
    }

    private static string CreateResetCountdownText(RateLimitBucket bucket)
    {
        return bucket.ResetCountdown == "reset unknown"
            ? "Reset unknown"
            : $"in {bucket.ResetCountdown}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 24)
        {
            var days = (int)duration.TotalDays;
            var hours = duration.Hours;
            return hours == 0 ? $"{days}d" : $"{days}d {hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes))}m";
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

    private static string CreateQuotaRingBrush(RateLimitBucket bucket, string statusText)
    {
        if (bucket.UsedPercent is not double)
        {
            return "#9CA3AF";
        }

        return statusText switch
        {
            "Critical" => "#F97316",
            "Warning" => "#F59E0B",
            "On pace" => "#16A34A",
            _ => "#9CA3AF"
        };
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
