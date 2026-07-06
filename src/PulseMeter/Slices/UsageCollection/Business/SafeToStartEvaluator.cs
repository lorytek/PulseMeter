using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

public enum SafeToStartLevel
{
    Green,
    Yellow,
    Red
}

public sealed record SafeToStartResult(SafeToStartLevel Level, string Message);

public static class SafeToStartEvaluator
{
    public static SafeToStartResult Evaluate(IEnumerable<RateLimitBucket> buckets)
    {
        var bucketList = buckets.ToList();

        if (bucketList.Any(bucket => bucket.IsReached || bucket.PercentValue >= 90))
        {
            return new SafeToStartResult(SafeToStartLevel.Red, "Wait for reset before long tasks");
        }

        if (bucketList.Any(bucket => bucket.PercentValue >= 75))
        {
            return new SafeToStartResult(SafeToStartLevel.Yellow, "Small tasks OK - long runs risky");
        }

        return new SafeToStartResult(SafeToStartLevel.Green, "Safe for small and medium tasks");
    }
}
