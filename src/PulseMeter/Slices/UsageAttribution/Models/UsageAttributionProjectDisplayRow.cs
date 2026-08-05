namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed record UsageAttributionProjectDisplayRow(
    string DisplayName,
    string FullPath,
    string EstimatedTokensText,
    string ShareText,
    string ActivityText,
    string TooltipText)
{
    public string AccessibleSummary =>
        $"{DisplayName}. {ShareText} share. Estimated {EstimatedTokensText} tokens. {ActivityText}";
}
