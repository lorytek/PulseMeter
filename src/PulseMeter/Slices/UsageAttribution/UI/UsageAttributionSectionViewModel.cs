using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseMeter.Slices.UsageAttribution.UI;

public sealed class UsageAttributionSectionViewModel : INotifyPropertyChanged
{
    private readonly IUsageAttributionPresenter _presenter;
    private UsageAttributionSnapshot _snapshot = UsageAttributionSnapshot.Empty;
    private IReadOnlyList<ProjectUsageRow> _selectableProjects = Array.Empty<ProjectUsageRow>();

    public UsageAttributionSectionViewModel(IUsageAttributionPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UsageAttributionProjectDisplayRow> ProjectRows { get; } = new();

    public bool HasAttribution => ProjectRows.Count > 0;

    public string SummaryText => _presenter.SummaryText(_snapshot);

    public string EvidenceText => "Estimated from local project activity, scaled to account usage";

    public string EmptyStateText => _presenter.EmptyStateText(_snapshot);

    public void ApplySnapshot(
        UsageAttributionSnapshot snapshot,
        DateTimeOffset _,
        IReadOnlyList<ProjectUsageRow>? selectableProjects = null)
    {
        _snapshot = snapshot;
        _selectableProjects = selectableProjects ?? Array.Empty<ProjectUsageRow>();
        RebuildRows();
    }

    public void Refresh(DateTimeOffset _)
    {
        RebuildRows();
    }

    private void RebuildRows()
    {
        ProjectRows.Clear();
        foreach (var row in _presenter.BuildProjectRows(_selectableProjects, _snapshot))
        {
            ProjectRows.Add(row);
        }

        OnPropertyChanged(nameof(HasAttribution));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(EvidenceText));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
