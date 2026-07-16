using PulseMeter.Shared.Formatting;

namespace PulseMeter.Slices.UsageAttribution.Business;

public interface IUsageAttributionPresenter
{
    IReadOnlyList<UsageAttributionProjectDisplayRow> BuildProjectRows(
        IReadOnlyList<ProjectUsageRow> projectRows,
        UsageAttributionSnapshot snapshot);

    bool HasAttribution(UsageAttributionSnapshot snapshot);

    string SummaryText(UsageAttributionSnapshot snapshot);

    string EvidenceText(UsageAttributionSnapshot snapshot);

    string EmptyStateText(UsageAttributionSnapshot snapshot);
}

public sealed class UsageAttributionPresenter : IUsageAttributionPresenter
{
    public IReadOnlyList<UsageAttributionProjectDisplayRow> BuildProjectRows(
        IReadOnlyList<ProjectUsageRow> projectRows,
        UsageAttributionSnapshot snapshot)
    {
        if (projectRows.Count > 0)
        {
            return projectRows
                .OrderByDescending(row => row.EstimatedTokens)
                .Take(5)
                .Select(row => BuildProjectRow(
                    row.DisplayName,
                    row.FullPath,
                    row.EstimatedTokens,
                    row.SharePercent,
                    row.ActiveDaysLast7 > 0
                        ? $"{row.ActiveDaysLast7} active {(row.ActiveDaysLast7 == 1 ? "day" : "days")} in the last 7 days"
                        : "No activity in the last 7 days"))
                .ToList();
        }

        return snapshot.Sessions
            .GroupBy(row => row.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                DisplayName = group.First().ProjectDisplayName,
                FullPath = group.Key,
                EstimatedTokens = group.Sum(row => row.EstimatedTokens),
                SharePercent = group.Sum(row => row.SharePercent)
            })
            .OrderByDescending(row => row.EstimatedTokens)
            .Take(5)
            .Select(row => BuildProjectRow(
                row.DisplayName,
                row.FullPath,
                row.EstimatedTokens,
                row.SharePercent,
                "Local project activity"))
            .ToList();
    }

    public bool HasAttribution(UsageAttributionSnapshot snapshot)
    {
        return snapshot.HasAttribution;
    }

    public string SummaryText(UsageAttributionSnapshot snapshot)
    {
        if (!snapshot.HasAttribution)
        {
            return "No local burn analysis yet";
        }

        var chatText = snapshot.Sessions.Count == 1 ? "1 local chat" : $"{snapshot.Sessions.Count} local chats";
        return $"{MeterDisplayFormatter.FormatTokens(snapshot.EstimatedAttributedTokens)} attributed across {chatText}";
    }

    public string EvidenceText(UsageAttributionSnapshot snapshot)
    {
        return snapshot.EvidenceText;
    }

    public string EmptyStateText(UsageAttributionSnapshot snapshot)
    {
        return snapshot.HasAttribution
            ? string.Empty
            : "No local burn analysis yet.";
    }

    private static UsageAttributionProjectDisplayRow BuildProjectRow(
        string displayName,
        string fullPath,
        long estimatedTokens,
        double sharePercent,
        string activityText)
    {
        var tokensText = MeterDisplayFormatter.FormatTokens(estimatedTokens);
        return new UsageAttributionProjectDisplayRow(
            displayName,
            fullPath,
            tokensText,
            $"{sharePercent:0.#}%",
            activityText,
            $"{displayName}\n{fullPath}\nEstimated 30-day token burn: {tokensText}");
    }
}
