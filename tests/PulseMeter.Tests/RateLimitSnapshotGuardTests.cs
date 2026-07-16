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
    public void IsSuspiciousRegression_AllowsUsageDropWhenResetCreditWasConsumed()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset, resetCreditsAvailable: 2);
        var candidate = Snapshot(5, reset.AddMinutes(3), resetCreditsAvailable: 1);

        Assert.False(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsSuspiciousRegression_FlagsUsageDropWhenResetCreditCountIsUnchanged()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset, resetCreditsAvailable: 1);
        var candidate = Snapshot(5, reset.AddMinutes(3), resetCreditsAvailable: 1);

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsSuspiciousRegression_FlagsWeeklyUsageDropEvenWhenResetCreditWasConsumed()
    {
        var reset = DateTimeOffset.UtcNow.AddDays(2);
        var previous = Snapshot(60, reset, resetCreditsAvailable: 2, windowMinutes: 10080);
        var candidate = Snapshot(5, reset.AddMinutes(3), resetCreditsAvailable: 1, windowMinutes: 10080);

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsSuspiciousRegression_FlagsShortWindowDisappearingWhenResetCreditWasConsumed()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var previous = new UsageSnapshot
        {
            ResetCreditsAvailable = 2,
            Buckets =
            [
                Bucket("codex", 60, reset),
                Bucket("codex", 25, weeklyReset, windowMinutes: 10080)
            ]
        };
        var candidate = new UsageSnapshot
        {
            ResetCreditsAvailable = 1,
            Buckets = [Bucket("codex", 25, weeklyReset, windowMinutes: 10080)]
        };

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
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

    [Fact]
    public void IsSuspiciousRegression_FlagsAddedBucket()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 60, reset)]
        };
        var candidate = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 62, reset.AddMinutes(1)),
                Bucket("codex", 25, DateTimeOffset.UtcNow.AddDays(2), windowMinutes: 10080)
            ]
        };

        Assert.True(RateLimitSnapshotGuard.IsSuspiciousRegression(previous, candidate));
    }

    [Fact]
    public void IsConfirmedSnapshotChange_AcceptsAddedMatchingValidTopologyWithConsistentValues()
    {
        var reset = DateTimeOffset.UtcNow.AddDays(2);
        var primaryReset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 60, primaryReset)]
        };
        var candidate = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 62, primaryReset.AddMinutes(1)),
                Bucket("codex", 25, reset, windowMinutes: 10080)
            ]
        };
        var confirmation = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 64, primaryReset.AddMinutes(2)),
                Bucket("codex", 28, reset.AddMinutes(1), windowMinutes: 10080)
            ]
        };

        Assert.True(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, confirmation));
    }

    [Fact]
    public void IsConfirmedSnapshotChange_AcceptsRemovedMatchingValidTopology()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var previous = new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", 60, reset),
                Bucket("codex", 25, weeklyReset, windowMinutes: 10080)
            ]
        };
        var candidate = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 25, weeklyReset, windowMinutes: 10080)]
        };
        var confirmation = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 26, weeklyReset.AddMinutes(1), windowMinutes: 10080)]
        };

        Assert.True(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, confirmation));
    }

    [Fact]
    public void IsConfirmedSnapshotChange_AcceptsMatchingUsageDropWithoutTopologyChange()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset);
        var candidate = Snapshot(5, reset.AddMinutes(3));
        var confirmation = Snapshot(6, reset.AddMinutes(4));

        Assert.True(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, confirmation));
    }

    [Fact]
    public void IsConfirmedSnapshotChange_RejectsConflictingUsageDropWithoutTopologyChange()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var previous = Snapshot(60, reset);
        var candidate = Snapshot(5, reset.AddMinutes(3));
        var confirmation = Snapshot(58, reset.AddMinutes(4));

        Assert.False(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, confirmation));
    }

    [Fact]
    public void IsConfirmedSnapshotChange_RejectsEmptyOrMalformedConfirmation()
    {
        var reset = DateTimeOffset.UtcNow.AddDays(2);
        var previous = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 60, DateTimeOffset.UtcNow.AddHours(2))]
        };
        var candidate = new UsageSnapshot
        {
            Buckets = [Bucket("codex", 25, reset, windowMinutes: 10080)]
        };
        var emptyConfirmation = new UsageSnapshot();
        var malformedConfirmation = new UsageSnapshot
        {
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    UsedPercent = 25,
                    WindowDurationMins = 10080
                }
            ]
        };

        Assert.False(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, emptyConfirmation));
        Assert.False(RateLimitSnapshotGuard.IsConfirmedSnapshotChange(previous, candidate, malformedConfirmation));
    }

    private static UsageSnapshot Snapshot(
        double usedPercent,
        DateTimeOffset reset,
        int? resetCreditsAvailable = null,
        int windowMinutes = 300)
    {
        return new UsageSnapshot
        {
            ResetCreditsAvailable = resetCreditsAvailable,
            Buckets =
            [
                Bucket("codex", usedPercent, reset, windowMinutes)
            ]
        };
    }

    private static RateLimitBucket Bucket(
        string limitId,
        double usedPercent,
        DateTimeOffset reset,
        int windowMinutes = 300)
    {
        return new RateLimitBucket
        {
            LimitId = limitId,
            WindowDurationMins = windowMinutes,
            UsedPercent = usedPercent,
            ResetsAtUtc = reset,
            ResetsAtUnixSeconds = reset.ToUnixTimeSeconds()
        };
    }
}
