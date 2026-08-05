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
    string LargestMomentText)
{
    public string AccessibleSummary =>
        $"{DisplayName}. {ShareText} of 30-day usage. {Last7DaysText} in the last 7 days. " +
        $"{TrendText} versus the prior 7 days. {EstimatedTokensText} in the last 30 days.";
}
