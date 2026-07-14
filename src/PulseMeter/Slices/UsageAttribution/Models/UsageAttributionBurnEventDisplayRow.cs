namespace PulseMeter.Slices.UsageAttribution.Models;

public sealed record UsageAttributionBurnEventDisplayRow(
    string SessionDisplayName,
    string ProjectDisplayName,
    string ProjectPath,
    string EstimatedTokensText,
    string RawLocalTokensText,
    string BreakdownText,
    string MomentText,
    string TooltipText);
