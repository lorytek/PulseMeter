using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ProjectUsage.Business;

public interface IProjectUsagePresenter
{
    string EstimateText { get; }

    IReadOnlyList<ProjectUsageDisplayRow> BuildRows(IEnumerable<ProjectUsageRow> rows);
}

public sealed class ProjectUsagePresenter : IProjectUsagePresenter
{
    public string EstimateText => "Estimated from local sessions, scaled to account usage";

    public IReadOnlyList<ProjectUsageDisplayRow> BuildRows(IEnumerable<ProjectUsageRow> rows)
    {
        return ProjectUsageDisplayBuilder.BuildRows(rows);
    }
}
