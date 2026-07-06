using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class WindowDurationLabelerTests
{
    [Theory]
    [InlineData(15, "15m")]
    [InlineData(60, "1h")]
    [InlineData(300, "5h")]
    [InlineData(1440, "1d")]
    [InlineData(10080, "7d")]
    public void LabelFor_ReturnsFriendlyDuration(int minutes, string expected)
    {
        Assert.Equal(expected, WindowDurationLabeler.LabelFor(minutes));
    }

    [Fact]
    public void LabelFor_FallsBackToLimitIdWhenDurationMissing()
    {
        Assert.Equal("codex", WindowDurationLabeler.LabelFor(null, "codex"));
    }
}
