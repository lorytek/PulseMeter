namespace PulseMeter.Slices.ProjectUsage.Models;

public sealed record ProjectUsageDisplayRow(
    string DisplayName,
    string FullPath,
    string EstimatedTokensText,
    string ShareText,
    string ThreadCountText,
    double SharePercentValue,
    string Last7DaysText,
    string TrendText,
    string TrendBrush,
    long RecentDeltaTokens,
    long EstimatedLast7Days,
    string ActivityText,
    string SpikeDaysText,
    string LeadingChatsText,
    string LargestMomentText);
