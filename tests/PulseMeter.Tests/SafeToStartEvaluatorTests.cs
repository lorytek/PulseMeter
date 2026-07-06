using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class SafeToStartEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsRedWhenAnyBucketReached()
    {
        var bucket = new RateLimitBucket { UsedPercent = 10, RateLimitReachedType = "hard" };

        Assert.Equal(SafeToStartLevel.Red, SafeToStartEvaluator.Evaluate([bucket]).Level);
    }

    [Fact]
    public void Evaluate_ReturnsYellowWhenUsageIsElevated()
    {
        var bucket = new RateLimitBucket { UsedPercent = 76 };

        Assert.Equal(SafeToStartLevel.Yellow, SafeToStartEvaluator.Evaluate([bucket]).Level);
    }

    [Fact]
    public void Evaluate_ReturnsGreenWhenUsageIsLow()
    {
        var bucket = new RateLimitBucket { UsedPercent = 40 };

        Assert.Equal(SafeToStartLevel.Green, SafeToStartEvaluator.Evaluate([bucket]).Level);
    }
}
