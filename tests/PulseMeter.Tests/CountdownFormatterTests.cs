using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class CountdownFormatterTests
{
    [Fact]
    public void FormatResetCountdown_ReturnsUnknownWhenTimestampMissing()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_730_000_000);

        Assert.Equal("reset unknown", CountdownFormatter.FormatResetCountdown(null, now));
    }

    [Fact]
    public void FormatResetCountdown_ReturnsNowWhenResetIsExpired()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_730_000_000);

        Assert.Equal("now", CountdownFormatter.FormatResetCountdown(now.AddSeconds(-1).ToUnixTimeSeconds(), now));
    }

    [Fact]
    public void FormatResetCountdown_ReturnsHoursAndMinutes()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_730_000_000);

        Assert.Equal("2h 01m", CountdownFormatter.FormatResetCountdown(now.AddHours(2).AddMinutes(1).ToUnixTimeSeconds(), now));
    }

    [Fact]
    public void FormatResetCountdown_ReturnsDaysHoursAndMinutes()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_730_000_000);

        Assert.Equal("6d 23h 15m", CountdownFormatter.FormatResetCountdown(now.AddDays(6).AddHours(23).AddMinutes(15).ToUnixTimeSeconds(), now));
    }
}
