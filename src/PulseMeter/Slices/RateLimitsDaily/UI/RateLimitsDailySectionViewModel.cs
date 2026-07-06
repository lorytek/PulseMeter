using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimitsDaily.UI;

public sealed class RateLimitsDailySectionViewModel : INotifyPropertyChanged
{
    private readonly IRateLimitsDailyPresenter _presenter;
    private IReadOnlyList<RateLimitBucket> _selectedBuckets = [];

    public RateLimitsDailySectionViewModel(IRateLimitsDailyPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DailyRateLimitDisplayRow> DailyRateLimitRows { get; } = new();

    public bool HasDailyRateLimitRows => DailyRateLimitRows.Count > 0;

    public string RateLimitsDailySummaryText => _presenter.BuildSummaryText(HasDailyRateLimitRows);

    public bool HasRateLimitsDailyWarning => !string.IsNullOrWhiteSpace(RateLimitsDailyWarningText);

    public string RateLimitsDailyWarningText => _presenter.BuildWarningText(_selectedBuckets, DateTimeOffset.UtcNow);

    public void ApplySelectedBuckets(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        _selectedBuckets = selectedBuckets.ToList();
        Refresh(now);
    }

    public void Refresh(DateTimeOffset now)
    {
        DailyRateLimitRows.Clear();
        foreach (var row in _presenter.BuildRows(_selectedBuckets, now))
        {
            DailyRateLimitRows.Add(row);
        }

        OnPropertyChanged(nameof(HasDailyRateLimitRows));
        OnPropertyChanged(nameof(RateLimitsDailySummaryText));
        OnPropertyChanged(nameof(HasRateLimitsDailyWarning));
        OnPropertyChanged(nameof(RateLimitsDailyWarningText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
