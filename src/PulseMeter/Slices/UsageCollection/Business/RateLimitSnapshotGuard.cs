namespace PulseMeter.Slices.UsageCollection.Business;

public static class RateLimitSnapshotGuard
{
    private const double SuspiciousUsageDropPercent = 10;
    private static readonly TimeSpan MaximumConfirmationResetDifference = TimeSpan.FromMinutes(10);

    public static bool IsSuspiciousRegression(UsageSnapshot previous, UsageSnapshot candidate)
    {
        var resetCreditWasConsumed = previous.ResetCreditsAvailable is int previousCredits
            && candidate.ResetCreditsAvailable is int candidateCredits
            && candidateCredits < previousCredits;

        if (previous.Buckets.Count > 0
            && (!IsValidForTopologyConfirmation(candidate) || IsTopologyChanged(previous, candidate)))
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
            if (candidateReset - previousReset < minimumResetAdvance
                && !(resetCreditWasConsumed && IsResettableShortWindow(previousBucket)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsConfirmedSnapshotChange(
        UsageSnapshot previous,
        UsageSnapshot candidate,
        UsageSnapshot confirmation)
    {
        if (!IsSuspiciousRegression(previous, candidate)
            || !IsValidForTopologyConfirmation(candidate)
            || !IsValidForTopologyConfirmation(confirmation)
            || !HasSameTopology(candidate, confirmation))
        {
            return false;
        }

        return candidate.Buckets.All(candidateBucket =>
        {
            var confirmedBucket = confirmation.Buckets.First(bucket => IsSameWindow(candidateBucket, bucket));
            return AreMateriallyConsistent(candidateBucket, confirmedBucket);
        });
    }

    public static bool IsValidForTopologyConfirmation(UsageSnapshot snapshot)
    {
        return snapshot.Buckets.Count > 0
            && snapshot.Buckets.All(IsWellFormed)
            && snapshot.Buckets
                .Select(GetWindowKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == snapshot.Buckets.Count;
    }

    private static bool IsTopologyChanged(UsageSnapshot previous, UsageSnapshot candidate)
    {
        return !HasSameTopology(previous, candidate);
    }

    private static bool HasSameTopology(UsageSnapshot left, UsageSnapshot right)
    {
        return left.Buckets.Count == right.Buckets.Count
            && left.Buckets.All(leftBucket => right.Buckets.Any(rightBucket => IsSameWindow(leftBucket, rightBucket)));
    }

    private static bool IsWellFormed(RateLimitBucket bucket)
    {
        return !string.IsNullOrWhiteSpace(bucket.LimitId)
            && bucket.WindowDurationMins is > 0
            && bucket.UsedPercent is double usedPercent
            && double.IsFinite(usedPercent)
            && usedPercent is >= 0 and <= 100
            && bucket.ResetsAtUtc is not null;
    }

    private static string GetWindowKey(RateLimitBucket bucket)
    {
        return $"{bucket.LimitId}\u001f{bucket.WindowDurationMins}";
    }

    private static bool AreMateriallyConsistent(RateLimitBucket candidate, RateLimitBucket confirmation)
    {
        return candidate.UsedPercent is double candidateUsed
            && confirmation.UsedPercent is double confirmationUsed
            && Math.Abs(candidateUsed - confirmationUsed) <= SuspiciousUsageDropPercent
            && candidate.ResetsAtUtc is DateTimeOffset candidateReset
            && confirmation.ResetsAtUtc is DateTimeOffset confirmationReset
            && (candidateReset - confirmationReset).Duration() <= MaximumConfirmationResetDifference;
    }

    private static bool IsSameWindow(RateLimitBucket left, RateLimitBucket right)
    {
        return left.WindowDurationMins == right.WindowDurationMins
            && string.Equals(left.LimitId, right.LimitId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResettableShortWindow(RateLimitBucket bucket)
    {
        return bucket.WindowDurationMins is > 0 and <= 1440;
    }
}
