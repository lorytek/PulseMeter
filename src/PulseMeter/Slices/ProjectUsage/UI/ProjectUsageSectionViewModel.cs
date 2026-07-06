using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ProjectUsage.UI;

public sealed class ProjectUsageSectionViewModel : INotifyPropertyChanged
{
    private readonly IProjectUsagePresenter _presenter;

    public ProjectUsageSectionViewModel(IProjectUsagePresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectUsageDisplayRow> ProjectUsageRows { get; } = new();

    public bool HasProjectUsage => ProjectUsageRows.Count > 0;

    public string ProjectUsageEstimateText => _presenter.EstimateText;

    public void ApplyRows(IEnumerable<ProjectUsageRow> rows)
    {
        ProjectUsageRows.Clear();
        foreach (var row in _presenter.BuildRows(rows))
        {
            ProjectUsageRows.Add(row);
        }

        OnPropertyChanged(nameof(HasProjectUsage));
        OnPropertyChanged(nameof(ProjectUsageEstimateText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
