using System.Globalization;
using PulseMeter.Shared.RateLimits;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.AccountUsage.Business;

internal static class AccountUsageFreshnessEvaluator
{
    public static AccountUsageFreshnessState Evaluate(
        UsageSnapshot previousSnapshot,
        UsageSnapshot nextSnapshot,
        DateOnly today,
        bool useMockMode,
        bool currentDailyWarning,
        bool currentAccountSummaryWarning)
    {
        var liveSnapshot = nextSnapshot.SyncStatus == SyncStatus.Live && !useMockMode;
        var usageActivityDetected = DidRateLimitUsageMove(previousSnapshot.Buckets, nextSnapshot.Buckets)
            || HasAnyRateLimitUsage(nextSnapshot);
        var dailyMissing = liveSnapshot
            && nextSnapshot.Buckets.Count > 0
            && nextSnapshot.DailyBuckets.Count == 0;
        var accountSummaryMissing = liveSnapshot
            && nextSnapshot.Buckets.Count > 0
            && !AccountUsageDisplayBuilder.HasAccountSummary(nextSnapshot);
        var previousToday = AccountUsageDisplayBuilder.GetTodayTokens(previousSnapshot, today);
        var nextToday = AccountUsageDisplayBuilder.GetTodayTokens(nextSnapshot, today);
        var todayUsageMissing = liveSnapshot
            && nextSnapshot.Buckets.Count > 0
            && nextToday is null;
        var dailyWarning = currentDailyWarning;
        var summaryWarning = currentAccountSummaryWarning;

        if (!liveSnapshot)
        {
            dailyWarning = false;
            summaryWarning = false;
        }
        else
        {
            if (dailyMissing || todayUsageMissing)
            {
                dailyWarning = true;
            }
            else if (previousToday is long previousTokens && nextToday is long nextTokens)
            {
                dailyWarning = nextTokens == previousTokens && usageActivityDetected;
            }
            else if (nextToday is null)
            {
                dailyWarning = false;
            }

            summaryWarning = accountSummaryMissing || todayUsageMissing;
        }

        return new AccountUsageFreshnessState(dailyWarning, summaryWarning);
    }

    private static bool DidRateLimitUsageMove(
        IReadOnlyList<RateLimitBucket> previousBuckets,
        IReadOnlyList<RateLimitBucket> nextBuckets)
    {
        if (previousBuckets.Count == 0 || nextBuckets.Count == 0)
        {
            return false;
        }

        var previousUsage = previousBuckets
            .Where(bucket => bucket.UsedPercent is not null)
            .ToDictionary(RateLimitUsageKey, bucket => bucket.UsedPercent!.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var bucket in nextBuckets.Where(bucket => bucket.UsedPercent is not null))
        {
            if (previousUsage.TryGetValue(RateLimitUsageKey(bucket), out var previous)
                && Math.Abs(previous - bucket.UsedPercent!.Value) >= 0.001)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyRateLimitUsage(UsageSnapshot snapshot)
    {
        return snapshot.Buckets.Any(bucket =>
            bucket.UsedPercent is double usedPercent
            && usedPercent > 0);
    }

    private static string RateLimitUsageKey(RateLimitBucket bucket)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{RateLimitBucketKeys.Get(bucket)}|{bucket.WindowDurationMins}|{bucket.WindowLabel}");
    }
}

public sealed record AccountUsageFreshnessState(
    bool HasDailyUsageFreshnessWarning,
    bool HasAccountSummaryFreshnessWarning);
