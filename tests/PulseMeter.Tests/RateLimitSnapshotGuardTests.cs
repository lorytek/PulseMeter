using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class RateLimitSnapshotGuardTests
{
    [Fact]
    public void IsSuspiciousRegression_FlagsLargeUsageDropInsideSameResetWindow()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset);
        var candidate = Snapshot(5, reset.AddMinutes(3));

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsSuspiciousRegression_AllowsUsageDropAfterRealWindowReset()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset);
        var candidate = Snapshot(5, reset.AddHours(5));

        Assert.False(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsSuspiciousRegression_FlagsMissingBucketWhenAnotherBucketRemains()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 60, reset),
                Bucket("gpt", 25, reset)
            ]
        };
        var candidate = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 60, reset)
            ]
        };

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    private static UsageSnapshot Snapshot(double usedPercent, DateTimeOffset reset)
    {
        return new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", usedPercent, reset)
            ]
        };
    }

    private static RateLimitBucket Bucket(string limitId, double usedPercent, DateTimeOffset reset)
    {
        return new RateLimitBucket
        {
            LimitId = limitId,
            WindowDurationMins = 300,
            UsedPercent = usedPercent,
            ResetsAtUtc = reset,
            ResetsAtUnixSeconds = reset.ToUnixTimeSeconds()
        };
    }
}
