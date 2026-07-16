using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimitsDaily.UI;

public sealed class RateLimitsDailySectionViewModel : INotifyPropertyChanged
{
    private readonly IRateLimitsDailyPresenter _presenter;
    private IReadOnlyList<RateLimitBucket> _selectedBuckets = [];
    private string _warningText = string.Empty;

    public RateLimitsDailySectionViewModel(IRateLimitsDailyPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DailyRateLimitDisplayRow> DailyRateLimitRows { get; } = new();

    public bool HasDailyRateLimitRows => DailyRateLimitRows.Count > 0;

    public string RateLimitsDailySummaryText => _presenter.BuildSummaryText(HasDailyRateLimitRows);

    public bool HasRateLimitsDailyWarning => !string.IsNullOrWhiteSpace(_warningText);

    public string RateLimitsDailyWarningText => _warningText;

    public bool IsAheadOfWeeklyPace => HasRateLimitsDailyWarning;

    public string WeeklyPaceDetailText => BuildWeeklyPaceDetailText(_warningText);

    public void ApplySelectedBuckets(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        _selectedBuckets = selectedBuckets.ToList();
        Refresh(now);
    }

    public void Refresh(DateTimeOffset now)
    {
        _warningText = _presenter.BuildWarningText(_selectedBuckets, now);
        DailyRateLimitRows.Clear();
        foreach (var row in _presenter.BuildRows(_selectedBuckets, now))
        {
            DailyRateLimitRows.Add(row);
        }

        OnPropertyChanged(nameof(HasDailyRateLimitRows));
        OnPropertyChanged(nameof(RateLimitsDailySummaryText));
        OnPropertyChanged(nameof(HasRateLimitsDailyWarning));
        OnPropertyChanged(nameof(RateLimitsDailyWarningText));
        OnPropertyChanged(nameof(IsAheadOfWeeklyPace));
        OnPropertyChanged(nameof(WeeklyPaceDetailText));
    }

    private static string BuildWeeklyPaceDetailText(string warningText)
    {
        const string waitMarker = "Wait ";
        var waitIndex = warningText.IndexOf(waitMarker, StringComparison.Ordinal);
        return waitIndex >= 0
            ? warningText[waitIndex..].TrimEnd('.')
            : string.IsNullOrWhiteSpace(warningText)
                ? "Within weekly pace"
                : "Weekly usage is ahead of pace";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
