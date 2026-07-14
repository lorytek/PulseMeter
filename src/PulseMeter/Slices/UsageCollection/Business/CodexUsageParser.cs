using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

public static class CodexUsageParser
{
    public static UsageSnapshot ParseRateLimits(JsonElement result, DateTimeOffset now, string source)
    {
        var buckets = ParseRateLimitBuckets(result, now).ToList();

        return new UsageSnapshot
        {
            Buckets = buckets,
            ResetCreditsAvailable = TryReadResetCredits(result),
            ResetCreditsExpiresAtUtc = TryReadResetCreditExpiry(result),
            SyncStatus = source.Equals("Mock", StringComparison.OrdinalIgnoreCase) ? SyncStatus.Mocked : SyncStatus.Live,
            LastUpdatedUtc = now,
            Source = source,
            RawRateLimitsJson = SafeRawText(result)
        };
    }

    public static UsageSnapshot MergeUsageSummary(UsageSnapshot snapshot, JsonElement usageResult)
    {
        var summary = TryGetObject(usageResult, "summary");

        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = ReadLong(summary, "lifetimeTokens"),
            PeakDailyTokens = ReadLong(summary, "peakDailyTokens"),
            LongestRunningTurnSec = ReadInt(summary, "longestRunningTurnSec"),
            CurrentStreakDays = ReadInt(summary, "currentStreakDays"),
            LongestStreakDays = ReadInt(summary, "longestStreakDays"),
            DailyBuckets = ParseDailyBuckets(usageResult).ToList(),
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = snapshot.SyncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = snapshot.Source,
            StatusMessage = snapshot.StatusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    public static ThreadUsageSnapshot? ParseThreadUsage(JsonElement payload, DateTimeOffset now)
    {
        var threadObject = TryGetObject(payload, "thread");
        var usageObject = TryGetObject(payload, "tokenUsage") ?? TryGetObject(payload, "usage") ?? payload;

        var threadId = ReadString(payload, "threadId") ?? ReadString(threadObject, "id") ?? ReadString(payload, "id");
        var threadName = ReadString(payload, "threadName") ?? ReadString(threadObject, "name") ?? ReadString(threadObject, "title");

        var inputTokens = ReadLong(usageObject, "inputTokens");
        var outputTokens = ReadLong(usageObject, "outputTokens");
        var totalTokens = ReadLong(usageObject, "totalTokens") ?? ReadLong(usageObject, "tokens");

        if (totalTokens is null && (inputTokens is not null || outputTokens is not null))
        {
            totalTokens = (inputTokens ?? 0) + (outputTokens ?? 0);
        }

        var contextUsed = ReadDouble(payload, "contextUsedPercent")
            ?? ReadDouble(usageObject, "contextUsedPercent")
            ?? ReadDouble(usageObject, "usedPercent");
        var contextLeft = ReadDouble(payload, "contextLeftPercent")
            ?? ReadDouble(usageObject, "contextLeftPercent")
            ?? (contextUsed is double used ? Math.Max(0, 100 - used) : null);

        if (threadId is null && threadName is null && totalTokens is null && contextUsed is null)
        {
            return null;
        }

        return new ThreadUsageSnapshot
        {
            ThreadId = threadId,
            ThreadName = threadName,
            ContextUsedPercent = contextUsed,
            ContextLeftPercent = contextLeft,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            LastUpdatedUtc = now,
            IsExactCurrentDesktopThread = false
        };
    }

    public static UsageSnapshot WithThreadUsage(UsageSnapshot snapshot, ThreadUsageSnapshot? threadUsage)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = threadUsage ?? snapshot.RecentActiveThread,
            SyncStatus = snapshot.SyncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = snapshot.Source,
            StatusMessage = snapshot.StatusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    public static UsageSnapshot WithStatus(UsageSnapshot snapshot, SyncStatus syncStatus, string source, string? statusMessage)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = syncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = source,
            StatusMessage = statusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    public static UsageSnapshot WithResetCredits(UsageSnapshot snapshot, ResetCreditFetchResult resetCredits)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = resetCredits.AvailableCount,
            ResetCreditsExpiresAtUtc = resetCredits.Credits
                .Select(credit => credit.ExpiresAtUtc)
                .Where(expiresAt => expiresAt is not null)
                .Min(),
            ResetCredits = resetCredits.Credits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = snapshot.SyncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = snapshot.Source,
            StatusMessage = snapshot.StatusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    public static UsageSnapshot WithProjectUsage(UsageSnapshot snapshot, IReadOnlyList<ProjectUsageRow> projectUsageRows)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = projectUsageRows,
            UsageAttribution = snapshot.UsageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = snapshot.SyncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = snapshot.Source,
            StatusMessage = snapshot.StatusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    public static UsageSnapshot WithUsageAttribution(UsageSnapshot snapshot, UsageAttributionSnapshot usageAttribution)
    {
        return new UsageSnapshot
        {
            Buckets = snapshot.Buckets,
            LifetimeTokens = snapshot.LifetimeTokens,
            PeakDailyTokens = snapshot.PeakDailyTokens,
            LongestRunningTurnSec = snapshot.LongestRunningTurnSec,
            CurrentStreakDays = snapshot.CurrentStreakDays,
            LongestStreakDays = snapshot.LongestStreakDays,
            DailyBuckets = snapshot.DailyBuckets,
            ProjectUsageRows = snapshot.ProjectUsageRows,
            UsageAttribution = usageAttribution,
            ResetCreditsAvailable = snapshot.ResetCreditsAvailable,
            ResetCreditsExpiresAtUtc = snapshot.ResetCreditsExpiresAtUtc,
            ResetCredits = snapshot.ResetCredits,
            RecentActiveThread = snapshot.RecentActiveThread,
            SyncStatus = snapshot.SyncStatus,
            LastUpdatedUtc = snapshot.LastUpdatedUtc,
            Source = snapshot.Source,
            StatusMessage = snapshot.StatusMessage,
            RawRateLimitsJson = snapshot.RawRateLimitsJson
        };
    }

    private static IEnumerable<RateLimitBucket> ParseRateLimitBuckets(JsonElement result, DateTimeOffset now)
    {
        if (TryGetObject(result, "rateLimitsByLimitId") is JsonElement byLimitId)
        {
            foreach (var property in byLimitId.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var bucket in ParseBucket(property.Value, now, property.Name))
                    {
                        yield return bucket;
                    }
                }
            }

            yield break;
        }

        if (TryGetObject(result, "rateLimits") is not JsonElement rateLimits)
        {
            yield break;
        }

        if (rateLimits.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in rateLimits.EnumerateArray())
            {
                if (bucket.ValueKind == JsonValueKind.Object)
                {
                    foreach (var parsed in ParseBucket(bucket, now, null))
                    {
                        yield return parsed;
                    }
                }
            }

            yield break;
        }

        if (rateLimits.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (LooksLikeRateLimitBucket(rateLimits))
        {
            foreach (var bucket in ParseBucket(rateLimits, now, ReadString(rateLimits, "limitId")))
            {
                yield return bucket;
            }

            yield break;
        }

        foreach (var property in rateLimits.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var bucket in ParseBucket(property.Value, now, property.Name))
                {
                    yield return bucket;
                }
            }
        }
    }

    private static IEnumerable<RateLimitBucket> ParseBucket(JsonElement bucket, DateTimeOffset now, string? fallbackLimitId)
    {
        var limitId = ReadString(bucket, "limitId") ?? fallbackLimitId;
        var limitName = ReadString(bucket, "limitName");
        var groupLabel = LabelForGroup(limitId, limitName);
        var primary = TryGetObject(bucket, "primary");
        var secondary = TryGetObject(bucket, "secondary");

        if (primary is JsonElement primaryWindow)
        {
            yield return ParseBucketWindow(bucket, primaryWindow, now, limitId, limitName, groupLabel);
        }

        if (secondary is JsonElement secondaryWindow)
        {
            yield return ParseBucketWindow(bucket, secondaryWindow, now, limitId, limitName, groupLabel);
        }

        if (primary is null && secondary is null)
        {
            yield return ParseBucketWindow(bucket, bucket, now, limitId, limitName, groupLabel);
        }
    }

    private static RateLimitBucket ParseBucketWindow(
        JsonElement bucket,
        JsonElement window,
        DateTimeOffset now,
        string? limitId,
        string? limitName,
        string groupLabel)
    {
        var duration = ReadInt(window, "windowDurationMins") ?? ReadInt(bucket, "windowDurationMins");
        var resetsAt = ReadLong(window, "resetsAt") ?? ReadLong(bucket, "resetsAt");

        return new RateLimitBucket
        {
            LimitId = limitId,
            LimitName = limitName,
            UsedPercent = ReadDouble(window, "usedPercent") ?? ReadDouble(bucket, "usedPercent"),
            WindowDurationMins = duration,
            ResetsAtUnixSeconds = resetsAt,
            ResetsAtUtc = resetsAt is long unix ? DateTimeOffset.FromUnixTimeSeconds(unix) : null,
            RateLimitReachedType = ReadString(window, "rateLimitReachedType") ?? ReadString(bucket, "rateLimitReachedType"),
            GroupLabel = groupLabel,
            WindowLabel = WindowDurationLabeler.LabelFor(duration, limitId, null),
            Label = WindowDurationLabeler.LabelFor(duration, limitId, limitName),
            ResetCountdown = CountdownFormatter.FormatResetCountdown(resetsAt, now)
        };
    }

    private static string LabelForGroup(string? limitId, string? limitName)
    {
        if (!string.IsNullOrWhiteSpace(limitName))
        {
            return limitName;
        }

        if (string.Equals(limitId, "codex", StringComparison.OrdinalIgnoreCase))
        {
            return "General";
        }

        return string.IsNullOrWhiteSpace(limitId)
            ? "Usage"
            : limitId.Replace('_', ' ');
    }

    private static IEnumerable<DailyUsageBucket> ParseDailyBuckets(JsonElement usageResult)
    {
        var buckets = TryGetArray(usageResult, "dailyUsageBuckets")
            ?? TryGetArray(usageResult, "dailyBuckets");

        if (buckets is not JsonElement array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            yield return new DailyUsageBucket
            {
                StartDate = ReadString(item, "startDate") ?? ReadString(item, "date"),
                Tokens = ReadLong(item, "tokens"),
                InputTokens = ReadLong(item, "inputTokens"),
                OutputTokens = ReadLong(item, "outputTokens")
            };
        }
    }

    private static bool LooksLikeRateLimitBucket(JsonElement element)
    {
        return element.TryGetProperty("primary", out _)
            || element.TryGetProperty("usedPercent", out _)
            || element.TryGetProperty("limitId", out _);
    }

    private static int? TryReadResetCredits(JsonElement result)
    {
        if (TryGetObject(result, "rateLimitResetCredits") is JsonElement credits)
        {
            return ReadInt(credits, "availableCount") ?? ReadInt(credits, "available");
        }

        return ReadInt(result, "resetCreditsAvailable");
    }

    private static DateTimeOffset? TryReadResetCreditExpiry(JsonElement result)
    {
        if (TryGetObject(result, "rateLimitResetCredits") is JsonElement credits)
        {
            return ReadDateTimeOffset(credits, "expiresAt")
                ?? ReadDateTimeOffset(credits, "expiresAtUtc")
                ?? ReadDateTimeOffset(credits, "expiresAtUnixSeconds")
                ?? ReadDateTimeOffset(credits, "expirationTime")
                ?? ReadDateTimeOffset(credits, "availableUntil");
        }

        return ReadDateTimeOffset(result, "resetCreditsExpiresAt")
            ?? ReadDateTimeOffset(result, "resetCreditsExpiresAtUtc")
            ?? ReadDateTimeOffset(result, "resetCreditsExpiresAtUnixSeconds");
    }

    private static JsonElement? TryGetObject(JsonElement? element, string name)
    {
        if (element is not JsonElement actual || actual.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return actual.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;
    }

    private static JsonElement? TryGetArray(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Array
                ? property
                : null;
    }

    private static string? ReadString(JsonElement? element, string name)
    {
        if (element is not JsonElement actual
            || actual.ValueKind != JsonValueKind.Object
            || !actual.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int? ReadInt(JsonElement? element, string name)
    {
        if (ReadLong(element, name) is long value)
        {
            return value is >= int.MinValue and <= int.MaxValue ? (int)value : null;
        }

        return null;
    }

    private static long? ReadLong(JsonElement? element, string name)
    {
        if (element is not JsonElement actual
            || actual.ValueKind != JsonValueKind.Object
            || !actual.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement? element, string name)
    {
        if (element is not JsonElement actual
            || actual.ValueKind != JsonValueKind.Object
            || !actual.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString();
        if (long.TryParse(text, out var parsedUnixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(parsedUnixSeconds);
        }

        return DateTimeOffset.TryParse(text, out var parsedDate) ? parsedDate.ToUniversalTime() : null;
    }

    private static double? ReadDouble(JsonElement? element, string name)
    {
        if (element is not JsonElement actual
            || actual.ValueKind != JsonValueKind.Object
            || !actual.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && double.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? SafeRawText(JsonElement element)
    {
        try
        {
            return element.GetRawText();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
