using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Shared.RateLimits;

internal static class RateLimitBucketKeys
{
    public static string Get(RateLimitBucket bucket)
    {
        return string.IsNullOrWhiteSpace(bucket.LimitId) ? bucket.GroupLabel : bucket.LimitId;
    }

    public static string GetWindowScope(RateLimitBucket bucket) =>
        $"{Get(bucket)}|{bucket.WindowDurationMins?.ToString() ?? "unknown"}";
}
