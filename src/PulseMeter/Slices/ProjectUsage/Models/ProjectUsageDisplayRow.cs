namespace PulseMeter.Slices.ProjectUsage.Models;

public sealed record ProjectUsageDisplayRow(
    string DisplayName,
    string FullPath,
    string EstimatedTokensText,
    string ShareText,
    string ThreadCountText,
    double SharePercentValue);
