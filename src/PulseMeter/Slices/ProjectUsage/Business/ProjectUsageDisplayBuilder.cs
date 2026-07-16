using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ProjectUsage.Business;

internal static class ProjectUsageDisplayBuilder
{
    public static IReadOnlyList<ProjectUsageDisplayRow> BuildRows(IEnumerable<ProjectUsageRow> rows)
    {
        return rows
            .Select(BuildRow)
            .ToList();
    }

    private static ProjectUsageDisplayRow BuildRow(ProjectUsageRow row)
    {
        var recentDeltaTokens = row.EstimatedLast7Days - row.EstimatedPrevious7Days;
        var trendText = recentDeltaTokens switch
        {
            > 0 => $"+{MeterDisplayFormatter.FormatTokens(recentDeltaTokens)}",
            < 0 => $"-{MeterDisplayFormatter.FormatTokens(Math.Abs(recentDeltaTokens))}",
            _ => "No change"
        };
        var trendBrush = recentDeltaTokens switch
        {
            > 0 => "#D97706",
            < 0 => "#16A34A",
            _ => "#6B7280"
        };
        var activityText = row.ActiveDaysLast7 switch
        {
            0 => "No activity in the last 7 days",
            1 => "1 active day in the last 7 days",
            _ => $"{row.ActiveDaysLast7} active days in the last 7 days"
        };
        var spikeDaysText = row.SpikeDays switch
        {
            0 => "No spike days",
            1 => "1 spike day",
            _ => $"{row.SpikeDays} spike days"
        };

        return new ProjectUsageDisplayRow(
            row.DisplayName,
            row.FullPath,
            MeterDisplayFormatter.FormatTokens(row.EstimatedTokens),
            $"{row.SharePercent:0.#}%",
            row.ThreadCount.ToString("N0", CultureInfo.InvariantCulture),
            Math.Clamp(row.SharePercent, 0, 100),
            MeterDisplayFormatter.FormatTokens(row.EstimatedLast7Days),
            trendText,
            trendBrush,
            recentDeltaTokens,
            row.EstimatedLast7Days,
            activityText,
            spikeDaysText,
            BuildLeadingChatsText(row),
            BuildLargestMomentText(row));
    }

    private static string BuildLeadingChatsText(ProjectUsageRow row)
    {
        if (string.IsNullOrWhiteSpace(row.LeadingChatDisplayName))
        {
            return "No local chat activity in the last 7 days.";
        }

        var leadingChat = $"Top chat: {row.LeadingChatDisplayName} ({MeterDisplayFormatter.FormatTokens(row.LeadingChatEstimatedTokens)})";
        return string.IsNullOrWhiteSpace(row.SecondLeadingChatDisplayName)
            ? leadingChat
            : $"{leadingChat}  Next: {row.SecondLeadingChatDisplayName} ({MeterDisplayFormatter.FormatTokens(row.SecondLeadingChatEstimatedTokens)})";
    }

    private static string BuildLargestMomentText(ProjectUsageRow row)
    {
        if (string.IsNullOrWhiteSpace(row.LargestBurnMomentChatDisplayName)
            || row.LargestBurnMomentAtUtc is null)
        {
            return "Largest recent moment unavailable.";
        }

        var localMoment = row.LargestBurnMomentAtUtc.Value.ToLocalTime();
        return $"Largest moment: {row.LargestBurnMomentChatDisplayName} ({MeterDisplayFormatter.FormatTokens(row.LargestBurnMomentEstimatedTokens)}, {localMoment:dd MMM HH:mm})";
    }
}
