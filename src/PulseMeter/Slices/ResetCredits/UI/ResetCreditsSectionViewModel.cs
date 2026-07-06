using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.ResetCredits.UI;

public sealed class ResetCreditsSectionViewModel : INotifyPropertyChanged
{
    private readonly IResetCreditsPresenter _presenter;
    private UsageSnapshot _snapshot = new();

    public ResetCreditsSectionViewModel(IResetCreditsPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ResetCreditListItem> ResetCredits { get; } = new();

    public string ResetCreditsHeaderText => _presenter.HeaderText(_snapshot);

    public string ResetCreditsAvailableText => _presenter.AvailableText(_snapshot);

    public void Refresh(DateTimeOffset nowUtc)
    {
        ReplaceItems(_presenter.Refresh(nowUtc));
        RefreshDisplayProperties();
    }

    public void ApplySnapshot(UsageSnapshot snapshot, DateTimeOffset nowUtc, bool shouldPersist)
    {
        _snapshot = snapshot;
        ReplaceItems(_presenter.Update(snapshot, nowUtc, shouldPersist));
        RefreshDisplayProperties();
    }

    private void ReplaceItems(IEnumerable<ResetCreditListItem> credits)
    {
        ResetCredits.Clear();
        foreach (var credit in credits)
        {
            ResetCredits.Add(credit);
        }
    }

    private void RefreshDisplayProperties()
    {
        OnPropertyChanged(nameof(ResetCreditsHeaderText));
        OnPropertyChanged(nameof(ResetCreditsAvailableText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
