using System.Globalization;
using PulseMeter.Slices.AccountUsage.UI;

namespace PulseMeter.Tests;

public sealed class AutoSyncSecondsValidationRuleTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("90")]
    [InlineData("86400")]
    public void Validate_AcceptsSupportedWholeSecondIntervals(string value)
    {
        var result = new AutoSyncSecondsValidationRule().Validate(value, CultureInfo.InvariantCulture);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("0")]
    [InlineData("86401")]
    public void Validate_RejectsInvalidOrOutOfRangeIntervals(string value)
    {
        var result = new AutoSyncSecondsValidationRule().Validate(value, CultureInfo.InvariantCulture);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorContent?.ToString()));
    }
}
