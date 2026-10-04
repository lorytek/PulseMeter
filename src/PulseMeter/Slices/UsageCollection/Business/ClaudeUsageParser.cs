using System.Globalization;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

/// <summary>
/// Parses the Claude Code subscription usage payload (the same data the Claude Code
/// <c>/usage</c> command shows). The payload is defensive-parsed because it is not a
/// versioned public API and may evolve.
/// </summary>
public static class ClaudeUsageParser
{
    public const string GeneralLimitId = "claude";
    public const string Source = "Claude";

    private const int FiveHourMinutes = 300;
    private const int SevenDayMinutes = 10_080;

    public static IReadOnlyList<RateLimitBucket> ParseRateLimitBuckets(JsonElement payload, DateTimeOffset now)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<RateLimitBucket>();
        }

        var buckets = new List<RateLimitBucket>();
        foreach (var property in payload.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object
                || TryGetWindowDuration(property.Name) is not int duration
                || ReadDouble(property.Value, "utilization") is not double utilization)
            {
                continue;
            }

            var resetsAt = ReadDateTimeOffset(property.Value, "resets_at");
            var resetsAtUnix = resetsAt?.ToUnixTimeSeconds();
            var (limitId, groupLabel, limitName) = DescribeLimit(property.Name);
            var usedPercent = Math.Clamp(utilization, 0, 100);

            buckets.Add(new RateLimitBucket
            {
                LimitId = limitId,
                LimitName = limitName,
                UsedPercent = usedPercent,
                WindowDurationMins = duration,
                ResetsAtUnixSeconds = resetsAtUnix,
                ResetsAtUtc = resetsAt,
                RateLimitReachedType = usedPercent >= 100 ? "usage_limit_reached" : null,
                GroupLabel = groupLabel,
                WindowLabel = WindowDurationLabeler.LabelFor(duration, limitId, null),
                Label = WindowDurationLabeler.LabelFor(duration, limitId, limitName),
                ResetCountdown = CountdownFormatter.FormatResetCountdown(resetsAtUnix, now)
            });
        }

        // Keep the general 5h/7d pair first so it becomes the default limit selection.
        return buckets
            .OrderBy(bucket => string.Equals(bucket.LimitId, GeneralLimitId, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(bucket => bucket.LimitId, StringComparer.Ordinal)
            .ThenBy(bucket => bucket.WindowDurationMins)
            .ToList();
    }

    private static int? TryGetWindowDuration(string name)
    {
        if (name.Equals("five_hour", StringComparison.OrdinalIgnoreCase))
        {
            return FiveHourMinutes;
        }

        if (name.StartsWith("seven_day", StringComparison.OrdinalIgnoreCase))
        {
            return SevenDayMinutes;
        }

        return null;
    }

    private static (string LimitId, string GroupLabel, string? LimitName) DescribeLimit(string name)
    {
        if (name.Equals("five_hour", StringComparison.OrdinalIgnoreCase)
            || name.Equals("seven_day", StringComparison.OrdinalIgnoreCase))
        {
            return (GeneralLimitId, "General", null);
        }

        // seven_day_opus -> "Opus", seven_day_oauth_apps -> "Oauth apps"
        var rawSuffix = name.Length > "seven_day_".Length
            ? name["seven_day_".Length..].ToLowerInvariant()
            : "weekly";
        var label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawSuffix.Replace('_', ' ').Trim());
        return ($"{GeneralLimitId}_{rawSuffix}", label, label);
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unix))
        {
            return unix > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                : DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        return null;
    }
}
