using System.Globalization;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ProjectUsage.Business;

internal static class ProjectUsageDisplayBuilder
{
    public static IReadOnlyList<ProjectUsageDisplayRow> BuildRows(IEnumerable<ProjectUsageRow> rows)
    {
        return rows
            .Select(row => new ProjectUsageDisplayRow(
                row.DisplayName,
                row.FullPath,
                MeterDisplayFormatter.FormatTokens(row.EstimatedTokens),
                $"{row.SharePercent:0.#}%",
                row.ThreadCount.ToString("N0", CultureInfo.InvariantCulture),
                Math.Clamp(row.SharePercent, 0, 100)))
            .ToList();
    }
}
