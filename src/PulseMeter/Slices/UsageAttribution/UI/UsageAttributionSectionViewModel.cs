using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseMeter.Slices.UsageAttribution.UI;

public sealed class UsageAttributionSectionViewModel : INotifyPropertyChanged
{
    private readonly IUsageAttributionPresenter _presenter;
    private UsageAttributionSnapshot _snapshot = UsageAttributionSnapshot.Empty;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public UsageAttributionSectionViewModel(IUsageAttributionPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UsageAttributionSessionDisplayRow> SessionRows { get; } = new();

    public ObservableCollection<UsageAttributionBurnEventDisplayRow> BurnEventRows { get; } = new();

    public bool HasAttribution => _presenter.HasAttribution(_snapshot);

    public string SummaryText => _presenter.SummaryText(_snapshot);

    public string EvidenceText => _presenter.EvidenceText(_snapshot);

    public string EmptyStateText => _presenter.EmptyStateText(_snapshot);

    public void ApplySnapshot(UsageAttributionSnapshot snapshot, DateTimeOffset now)
    {
        _snapshot = snapshot;
        _now = now;
        RebuildRows();
    }

    public void Refresh(DateTimeOffset now)
    {
        _now = now;
        RebuildRows();
    }

    private void RebuildRows()
    {
        SessionRows.Clear();
        foreach (var row in _presenter.BuildSessionRows(_snapshot, _now))
        {
            SessionRows.Add(row);
        }

        BurnEventRows.Clear();
        foreach (var row in _presenter.BuildBurnEventRows(_snapshot, _now))
        {
            BurnEventRows.Add(row);
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
