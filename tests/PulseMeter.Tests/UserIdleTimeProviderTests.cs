using PulseMeter.Platform.Windows;

namespace PulseMeter.Tests;

public sealed class UserIdleTimeProviderTests
{
    [Fact]
    public void CalculateIdleTime_ReturnsElapsedTimeBeforeTickCountRollover()
    {
        var idleTime = UserIdleTimeProvider.CalculateIdleTime(10_000, 9_250);

        Assert.Equal(TimeSpan.FromMilliseconds(750), idleTime);
    }

    [Fact]
    public void CalculateIdleTime_HandlesTickCountRollover()
    {
        var currentTickCount = (ulong)uint.MaxValue + 101;
        var lastInputTickCount = uint.MaxValue - 50;

        var idleTime = UserIdleTimeProvider.CalculateIdleTime(currentTickCount, lastInputTickCount);

        Assert.Equal(TimeSpan.FromMilliseconds(151), idleTime);
    }

    [Fact]
    public void CalculateIdleTime_UsesLowTickCountBitsAfterLongUptime()
    {
        var currentTickCount = (ulong)uint.MaxValue + 5_001;

        var idleTime = UserIdleTimeProvider.CalculateIdleTime(currentTickCount, 4_500);

        Assert.Equal(TimeSpan.FromMilliseconds(500), idleTime);
    }
}
