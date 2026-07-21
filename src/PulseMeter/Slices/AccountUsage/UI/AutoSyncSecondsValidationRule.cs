using System.Globalization;
using System.Windows.Controls;

namespace PulseMeter.Slices.AccountUsage.UI;

public sealed class AutoSyncSecondsValidationRule : ValidationRule
{
    public const int MinimumSeconds = 1;
    public const int MaximumSeconds = 86_400;

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (!int.TryParse(
                Convert.ToString(value, cultureInfo),
                NumberStyles.Integer,
                cultureInfo,
                out var seconds))
        {
            return new ValidationResult(false, "Enter a whole number of seconds.");
        }

        return seconds is >= MinimumSeconds and <= MaximumSeconds
            ? ValidationResult.ValidResult
            : new ValidationResult(false, "Use a value from 1 to 86,400 seconds.");
    }
}
