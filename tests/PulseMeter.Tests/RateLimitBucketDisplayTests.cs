using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class RateLimitBucketDisplayTests
{
    [Fact]
    public void RemainingPercentValue_DisplaysQuotaLeft()
    {
        var bucket = new RateLimitBucket { UsedPercent = 8 };

        Assert.Equal(92, bucket.RemainingPercentValue);
        Assert.Equal("92% left", bucket.RemainingPercentText);
    }

    [Fact]
    public void PercentValue_StillRepresentsUsedQuotaForSafetyLogic()
    {
        var bucket = new RateLimitBucket { UsedPercent = 8 };

        Assert.Equal(8, bucket.PercentValue);
    }
}
