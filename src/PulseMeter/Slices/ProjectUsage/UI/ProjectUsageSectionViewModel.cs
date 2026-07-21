using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ProjectUsage.UI;

public sealed class ProjectUsageSectionViewModel : INotifyPropertyChanged
{
    private readonly IProjectUsagePresenter _presenter;
    private ProjectUsageDisplayRow? _selectedProjectRow;

    public ProjectUsageSectionViewModel(IProjectUsagePresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectUsageDisplayRow> ProjectUsageRows { get; } = new();

    public bool HasProjectUsage => ProjectUsageRows.Count > 0;

    public string ProjectUsageEstimateText => _presenter.EstimateText;

    public ProjectUsageDisplayRow? SelectedProjectRow
    {
        get => _selectedProjectRow;
        set
        {
            if (ReferenceEquals(_selectedProjectRow, value))
            {
                return;
            }

            _selectedProjectRow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProjectTitle));
            OnPropertyChanged(nameof(SelectedProjectPathText));
            OnPropertyChanged(nameof(HasSelectedProject));
            OnPropertyChanged(nameof(SelectedProjectSummary));
            OnPropertyChanged(nameof(SelectedProjectChatsText));
            OnPropertyChanged(nameof(SelectedProjectMomentText));
        }
    }

    public string LargestIncreaseText => FormatLargestChange(ProjectUsageRows.Where(row => row.RecentDeltaTokens > 0).OrderByDescending(row => row.RecentDeltaTokens).FirstOrDefault(), "increase");

    public string LargestDropText => FormatLargestChange(ProjectUsageRows.Where(row => row.RecentDeltaTokens < 0).OrderBy(row => row.RecentDeltaTokens).FirstOrDefault(), "drop");

    public string LargestIncreaseProjectText => GetLargestIncrease()?.DisplayName ?? "No recent increase";

    public string LargestIncreaseValueText => GetLargestIncrease()?.TrendText ?? "--";

    public string LargestDropProjectText => GetLargestDrop()?.DisplayName ?? "No recent drop";

    public string LargestDropValueText => GetLargestDrop()?.TrendText ?? "--";

    public string SelectedProjectTitle => SelectedProjectRow?.DisplayName ?? "Select a project";

    public string SelectedProjectPathText => SelectedProjectRow?.FullPath ?? string.Empty;

    public bool HasSelectedProject => SelectedProjectRow is not null;

    public string SelectedProjectSummary => SelectedProjectRow is null
        ? "Choose a project to see its recent activity and local attribution evidence."
        : $"{SelectedProjectRow.Last7DaysText} in the last 7 days; {SelectedProjectRow.TrendText} versus the prior 7 days. {SelectedProjectRow.ActivityText}; {SelectedProjectRow.SpikeDaysText}.";

    public string SelectedProjectChatsText => SelectedProjectRow?.LeadingChatsText ?? string.Empty;

    public string SelectedProjectMomentText => SelectedProjectRow?.LargestMomentText ?? string.Empty;

    public void ApplyRows(IEnumerable<ProjectUsageRow> rows)
    {
        var selectedPath = SelectedProjectRow?.FullPath;
        ProjectUsageRows.Clear();
        foreach (var row in _presenter.BuildRows(rows))
        {
            ProjectUsageRows.Add(row);
        }

        SelectedProjectRow = ProjectUsageRows.FirstOrDefault(row => string.Equals(row.FullPath, selectedPath, StringComparison.Ordinal))
            ?? ProjectUsageRows.OrderByDescending(row => row.EstimatedLast7Days).FirstOrDefault();

        OnPropertyChanged(nameof(HasProjectUsage));
        OnPropertyChanged(nameof(ProjectUsageEstimateText));
        OnPropertyChanged(nameof(LargestIncreaseText));
        OnPropertyChanged(nameof(LargestDropText));
        OnPropertyChanged(nameof(LargestIncreaseProjectText));
        OnPropertyChanged(nameof(LargestIncreaseValueText));
        OnPropertyChanged(nameof(LargestDropProjectText));
        OnPropertyChanged(nameof(LargestDropValueText));
    }

    private ProjectUsageDisplayRow? GetLargestIncrease()
    {
        return ProjectUsageRows
            .Where(row => row.RecentDeltaTokens > 0)
            .OrderByDescending(row => row.RecentDeltaTokens)
            .FirstOrDefault();
    }

    private ProjectUsageDisplayRow? GetLargestDrop()
    {
        return ProjectUsageRows
            .Where(row => row.RecentDeltaTokens < 0)
            .OrderBy(row => row.RecentDeltaTokens)
            .FirstOrDefault();
    }

    private static string FormatLargestChange(ProjectUsageDisplayRow? row, string direction)
    {
        return row is null
            ? $"No 7-day {direction} yet"
            : $"Largest 7-day {direction}: {row.DisplayName} {row.TrendText}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
