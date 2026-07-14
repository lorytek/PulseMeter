using PulseMeter.Shared.Formatting;

namespace PulseMeter.Slices.UsageAttribution.Business;

public interface IUsageAttributionPresenter
{
    IReadOnlyList<UsageAttributionSessionDisplayRow> BuildSessionRows(UsageAttributionSnapshot snapshot, DateTimeOffset now);

    IReadOnlyList<UsageAttributionBurnEventDisplayRow> BuildBurnEventRows(UsageAttributionSnapshot snapshot, DateTimeOffset now);

    bool HasAttribution(UsageAttributionSnapshot snapshot);

    string SummaryText(UsageAttributionSnapshot snapshot);

    string EvidenceText(UsageAttributionSnapshot snapshot);

    string EmptyStateText(UsageAttributionSnapshot snapshot);
}

public sealed class UsageAttributionPresenter : IUsageAttributionPresenter
{
    public IReadOnlyList<UsageAttributionSessionDisplayRow> BuildSessionRows(UsageAttributionSnapshot snapshot, DateTimeOffset now)
    {
        return snapshot.Sessions
            .Select(row => new UsageAttributionSessionDisplayRow(
                row.DisplayName,
                row.ProjectDisplayName,
                row.ProjectPath,
                MeterDisplayFormatter.FormatTokens(row.EstimatedTokens),
                $"{row.SharePercent:0.#}%",
                $"local raw {MeterDisplayFormatter.FormatTokens(row.RawLocalTokens)}",
                FormatBreakdown(row.InputTokens, row.OutputTokens, row.CachedInputTokens, row.ReasoningTokens),
                FormatAge(row.LastEventAtUtc ?? row.ThreadUpdatedAtUtc, now),
                Math.Clamp(row.SharePercent, 0, 100),
                FormatTooltip(row.ThreadId, row.DisplayName, row.ProjectDisplayName, row.ProjectPath, row.LastEventAtUtc ?? row.ThreadUpdatedAtUtc, "Updated")))
            .ToList();
    }

    public IReadOnlyList<UsageAttributionBurnEventDisplayRow> BuildBurnEventRows(UsageAttributionSnapshot snapshot, DateTimeOffset now)
    {
        return snapshot.BurnEvents
            .Select(row => new UsageAttributionBurnEventDisplayRow(
                row.SessionDisplayName,
                row.ProjectDisplayName,
                row.ProjectPath,
                MeterDisplayFormatter.FormatTokens(row.EstimatedTokens),
                $"local raw {MeterDisplayFormatter.FormatTokens(row.RawLocalTokens)}",
                FormatBreakdown(row.InputTokens, row.OutputTokens, row.CachedInputTokens, row.ReasoningTokens),
                FormatMomentTime(row.TimestampUtc),
                FormatTooltip(row.ThreadId, row.SessionDisplayName, row.ProjectDisplayName, row.ProjectPath, row.TimestampUtc, "Moment")))
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

    private static string FormatBreakdown(long? inputTokens, long? outputTokens, long? cachedInputTokens, long? reasoningTokens)
    {
        var parts = new List<string>();
        if (inputTokens is long input)
        {
            parts.Add($"{MeterDisplayFormatter.FormatTokens(input)} in");
        }

        if (outputTokens is long output)
        {
            parts.Add($"{MeterDisplayFormatter.FormatTokens(output)} out");
        }

        if (cachedInputTokens is long cached)
        {
            parts.Add($"{MeterDisplayFormatter.FormatTokens(cached)} cached");
        }

        if (reasoningTokens is long reasoning)
        {
            parts.Add($"{MeterDisplayFormatter.FormatTokens(reasoning)} reasoning");
        }

        return parts.Count == 0 ? "breakdown unavailable" : string.Join(" / ", parts);
    }

    private static string FormatMomentTime(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("dd MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatAge(DateTimeOffset? timestampUtc, DateTimeOffset now)
    {
        if (timestampUtc is null)
        {
            return "time unknown";
        }

        var elapsed = now.ToUniversalTime() - timestampUtc.Value.ToUniversalTime();
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)Math.Round(elapsed.TotalMinutes)}m ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{(int)Math.Round(elapsed.TotalHours)}h ago";
        }

        return $"{(int)Math.Round(elapsed.TotalDays)}d ago";
    }

    private static string FormatTooltip(
        string? threadId,
        string displayName,
        string projectDisplayName,
        string projectPath,
        DateTimeOffset? timestampUtc,
        string timestampLabel)
    {
        var lines = new List<string>
        {
            $"Chat: {displayName}",
            $"Project: {projectDisplayName}",
            $"Path: {projectPath}"
        };

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            lines.Insert(0, $"Chat id: {threadId}");
        }

        if (timestampUtc is DateTimeOffset timestamp)
        {
            lines.Add($"{timestampLabel}: {timestamp.ToUniversalTime():dd MMM yyyy HH:mm} UTC");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
