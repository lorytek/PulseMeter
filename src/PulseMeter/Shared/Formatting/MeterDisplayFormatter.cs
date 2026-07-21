using System.Globalization;

namespace PulseMeter.Shared.Formatting;

internal static class MeterDisplayFormatter
{
    public static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000_000)
        {
            return $"{tokens / 1_000_000_000d:0.0}B";
        }

        if (tokens >= 1_000_000)
        {
            return $"{tokens / 1_000_000d:0.0}M";
        }

        if (tokens >= 1_000)
        {
            return $"{tokens / 1_000d:0.0}K";
        }

        return tokens.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string FormatMetricTokens(long? tokens)
    {
        return tokens is long value ? FormatTokens(value) : "--";
    }

    public static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalMinutes >= 1 && interval.TotalSeconds % 60 == 0)
        {
            var minutes = (int)interval.TotalMinutes;
            return minutes == 1 ? "1m" : $"{minutes}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(interval.TotalSeconds))}s";
    }

    public static string FormatFreshness(DateTimeOffset? updatedUtc, DateTimeOffset nowUtc)
    {
        if (updatedUtc is not DateTimeOffset updated)
        {
            return "Updated unknown";
        }

        var age = nowUtc.ToUniversalTime() - updated.ToUniversalTime();
        if (age < TimeSpan.Zero || age < TimeSpan.FromMinutes(1))
        {
            return "Updated just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"Updated {Math.Max(1, (int)age.TotalMinutes)}m ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"Updated {Math.Max(1, (int)age.TotalHours)}h ago";
        }

        if (age < TimeSpan.FromDays(7))
        {
            return $"Updated {Math.Max(1, (int)age.TotalDays)}d ago";
        }

        return $"Updated {updated.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";
    }

    public static string FormatFreshnessDetail(DateTimeOffset? updatedUtc)
    {
        return updatedUtc is DateTimeOffset updated
            ? $"Updated {updated.ToLocalTime().ToString("MMM d, yyyy 'at' HH:mm", CultureInfo.InvariantCulture)}"
            : "Updated time unknown";
    }

    public static string FormatWholePercent(double value)
    {
        return $"{Math.Clamp(value, 0, 100):0}%";
    }

    public static string CreateRingArcData(double percent)
    {
        var normalized = Math.Clamp(percent, 0, 99.99);
        if (normalized <= 0)
        {
            return "M 56 13";
        }

        const double center = 56;
        const double radius = 43;
        var angle = -90 + (normalized / 100 * 360);
        var radians = angle * Math.PI / 180;
        var endX = center + (radius * Math.Cos(radians));
        var endY = center + (radius * Math.Sin(radians));
        var largeArc = normalized > 50 ? 1 : 0;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {center:0.###} {center - radius:0.###} A {radius:0.###} {radius:0.###} 0 {largeArc} 1 {endX:0.###} {endY:0.###}");
    }

    public static string InterpolateHexColor(string startHex, string endHex, double amount)
    {
        var normalizedAmount = Math.Clamp(amount, 0, 1);
        var startR = Convert.ToInt32(startHex.Substring(1, 2), 16);
        var startG = Convert.ToInt32(startHex.Substring(3, 2), 16);
        var startB = Convert.ToInt32(startHex.Substring(5, 2), 16);
        var endR = Convert.ToInt32(endHex.Substring(1, 2), 16);
        var endG = Convert.ToInt32(endHex.Substring(3, 2), 16);
        var endB = Convert.ToInt32(endHex.Substring(5, 2), 16);

        var r = (int)Math.Round(startR + ((endR - startR) * normalizedAmount));
        var g = (int)Math.Round(startG + ((endG - startG) * normalizedAmount));
        var b = (int)Math.Round(startB + ((endB - startB) * normalizedAmount));

        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
