namespace PulseMeter.Slices.ResetCredits.Models;

public sealed record ResetCreditListItem(
    int Number,
    string ExpiryText,
    double ExpiryProgressValue,
    string ExpiryProgressBrush)
{
    public string DisplayText => $"Credit {Number} - {ExpiryText}";
}
