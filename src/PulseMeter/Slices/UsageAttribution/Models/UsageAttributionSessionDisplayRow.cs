namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed record UsageAttributionSessionDisplayRow(
    string DisplayName,
    string ProjectDisplayName,
    string ProjectPath,
    string EstimatedTokensText,
    string ShareText,
    string RawLocalTokensText,
    string BreakdownText,
    string AgeText,
    double SharePercentValue,
    string TooltipText);
