namespace PulseMeter.Slices.UsageCollection.Business;

public static class RateLimitSnapshotGuard
{
    private const double SuspiciousUsageDropPercent = 10;

    public static bool IsSuspiciousRegression(UsageSnapshot previous, UsageSnapshot candidate)
    {
        if (previous.Buckets.Count > 0 && candidate.Buckets.Count == 0)
        {
            return true;
        }

        foreach (var previousBucket in previous.Buckets)
        {
            var candidateBucket = candidate.Buckets.FirstOrDefault(bucket => IsSameWindow(previousBucket, bucket));
            if (candidateBucket is null)
            {
                return true;
            }

            if (previousBucket.UsedPercent is not double previousUsed
                || candidateBucket.UsedPercent is not double candidateUsed
                || previousUsed - candidateUsed < SuspiciousUsageDropPercent
                || previousBucket.ResetsAtUtc is not DateTimeOffset previousReset
                || candidateBucket.ResetsAtUtc is not DateTimeOffset candidateReset
                || previousBucket.WindowDurationMins is not int windowMinutes
                || windowMinutes <= 0)
            {
                continue;
            }

            var minimumResetAdvance = TimeSpan.FromMinutes(windowMinutes / 2d);
            if (candidateReset - previousReset < minimumResetAdvance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameWindow(RateLimitBucket left, RateLimitBucket right)
    {
        return left.WindowDurationMins == right.WindowDurationMins
            && string.Equals(left.LimitId, right.LimitId, StringComparison.OrdinalIgnoreCase);
    }
}
