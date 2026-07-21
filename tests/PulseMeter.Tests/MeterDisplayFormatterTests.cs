using System.Globalization;
using PulseMeter.Shared.Formatting;

namespace PulseMeter.Tests;

public sealed class MeterDisplayFormatterTests
{
    [Theory]
    [InlineData(-10, "Updated just now")]
    [InlineData(0, "Updated just now")]
    [InlineData(59, "Updated just now")]
    [InlineData(60, "Updated 1m ago")]
    [InlineData(599, "Updated 9m ago")]
    [InlineData(3_600, "Updated 1h ago")]
    [InlineData(10_800, "Updated 3h ago")]
    [InlineData(86_400, "Updated 1d ago")]
    [InlineData(518_400, "Updated 6d ago")]
    public void FormatFreshness_UsesScannableRelativeCopy(int ageSeconds, string expected)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var updated = now.AddSeconds(-ageSeconds);

        Assert.Equal(expected, MeterDisplayFormatter.FormatFreshness(updated, now));
    }

    [Fact]
    public void FormatFreshness_UsesAnAbsoluteDateAfterOneWeek()
    {
        var now = DateTimeOffset.Now;
        var updated = now.AddDays(-8);
        var localUpdated = updated.ToLocalTime();

        Assert.Equal(
            $"Updated {localUpdated.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}",
            MeterDisplayFormatter.FormatFreshness(updated, now));
    }

    [Fact]
    public void FormatFreshnessDetail_PreservesTheExactLocalTimestamp()
    {
        var updated = DateTimeOffset.Now.AddMinutes(-10);
        var localUpdated = updated.ToLocalTime();

        Assert.Equal(
            $"Updated {localUpdated.ToString("MMM d, yyyy 'at' HH:mm", CultureInfo.InvariantCulture)}",
            MeterDisplayFormatter.FormatFreshnessDetail(updated));
    }

    [Fact]
    public void FreshnessFormatters_DescribeMissingTimestamps()
    {
        Assert.Equal("Updated unknown", MeterDisplayFormatter.FormatFreshness(null, DateTimeOffset.UtcNow));
        Assert.Equal("Updated time unknown", MeterDisplayFormatter.FormatFreshnessDetail(null));
    }
}
