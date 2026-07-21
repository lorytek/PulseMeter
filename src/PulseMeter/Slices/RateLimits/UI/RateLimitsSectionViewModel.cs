using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimits.UI;

public sealed class RateLimitsSectionViewModel : INotifyPropertyChanged
{
    private readonly IRateLimitsPresenter _presenter;
    private RateLimitOption? _selectedLimitOption;
    private UsageSignalsSnapshot _signals = UsageSignalsSnapshot.Empty;
    private DateTimeOffset _lastUpdatedAt = DateTimeOffset.UtcNow;
    private bool _isAheadOfWeeklyPace;
    private string _weeklyPaceDetailText = string.Empty;

    public RateLimitsSectionViewModel(IRateLimitsPresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RateLimitOption> LimitOptions { get; } = new();

    public ObservableCollection<QuotaDisplayRow> CompactQuotaRows { get; } = new();

    public ObservableCollection<RateLimitBucket> SelectedBuckets { get; } = new();

    public ObservableCollection<QuotaDisplayRow> SelectedQuotaRows { get; } = new();

    public bool HasMultipleSelectedQuotaRows => SelectedQuotaRows.Count > 1;

    public int SelectedQuotaColumnCount => HasMultipleSelectedQuotaRows ? 2 : 1;

    public RateLimitOption? SelectedLimitOption
    {
        get => _selectedLimitOption;
        set
        {
            if (EqualityComparer<RateLimitOption?>.Default.Equals(_selectedLimitOption, value))
            {
                return;
            }

            _selectedLimitOption = value;
            RefreshSelectedBuckets();
            RefreshRunwayHintProperties();
            OnPropertyChanged();
        }
    }

    public string CompactTitleText => _presenter.BuildCompactTitle(SelectedBuckets, _buckets, SelectedLimitOption);

    public string CompactQuotaSummaryText => _presenter.BuildCompactQuotaSummary(CompactQuotaRows);

    public string ExpandedQuotaSummaryText => _presenter.BuildExpandedQuotaSummary(CompactQuotaRows);

    private IReadOnlyList<RateLimitBucket> _buckets = [];

    public bool HasRunwayHint => SelectedRunwaySignal is not null;

    public string RunwayHintText => SelectedRunwaySignal?.HintText ?? string.Empty;

    public string RunwayEvidenceText => HasRunwayHint ? "Estimated" : string.Empty;

    public void ApplyBuckets(
        IEnumerable<RateLimitBucket> buckets,
        DateTimeOffset now,
        string? preferredLimitKey = null)
    {
        _lastUpdatedAt = now;
        var selectedKey = SelectedLimitOption?.Key ?? preferredLimitKey;
        _buckets = buckets.ToList();
        var options = _presenter.BuildOptions(_buckets);

        LimitOptions.Clear();
        foreach (var option in options)
        {
            LimitOptions.Add(option);
        }

        _selectedLimitOption = _presenter.SelectOption(options, selectedKey);
        OnPropertyChanged(nameof(SelectedLimitOption));
        RefreshSelectedBuckets(now);
        RefreshRunwayHintProperties();
    }

    public void ApplyUsageSignals(UsageSignalsSnapshot signals)
    {
        _signals = signals;
        RefreshQuotaRows(_lastUpdatedAt);
        RefreshRunwayHintProperties();
    }

    public void ApplyWeeklyPace(bool isAheadOfPace, string detailText)
    {
        _isAheadOfWeeklyPace = isAheadOfPace;
        _weeklyPaceDetailText = detailText;
        RefreshQuotaRows(_lastUpdatedAt);
    }

    public void Refresh(DateTimeOffset now)
    {
        _lastUpdatedAt = now;
        RefreshQuotaRows(now);
    }

    private void RefreshSelectedBuckets()
    {
        RefreshSelectedBuckets(DateTimeOffset.UtcNow);
    }

    private void RefreshSelectedBuckets(DateTimeOffset now)
    {
        SelectedBuckets.Clear();
        foreach (var bucket in _presenter.SelectBuckets(_buckets, SelectedLimitOption))
        {
            SelectedBuckets.Add(bucket);
        }

        RefreshQuotaRows(now);
    }

    private void RefreshQuotaRows(DateTimeOffset now)
    {
        SelectedQuotaRows.Clear();
        foreach (var row in _presenter.BuildQuotaRows(SelectedBuckets, now).Take(2))
        {
            var rowWithRunway = QuotaDisplayBuilder.ApplyRunwayForecast(row, FindRunwaySignal(row));
            SelectedQuotaRows.Add(QuotaDisplayBuilder.ApplyWeeklyPace(
                rowWithRunway,
                _isAheadOfWeeklyPace,
                _weeklyPaceDetailText));
        }

        OnPropertyChanged(nameof(HasMultipleSelectedQuotaRows));
        OnPropertyChanged(nameof(SelectedQuotaColumnCount));

        CompactQuotaRows.Clear();
        foreach (var row in _presenter.BuildCompactRows(SelectedQuotaRows))
        {
            CompactQuotaRows.Add(row);
        }

        OnPropertyChanged(nameof(CompactTitleText));
        OnPropertyChanged(nameof(CompactQuotaSummaryText));
        OnPropertyChanged(nameof(ExpandedQuotaSummaryText));
    }

    private LimitRunwaySignal? FindRunwaySignal(QuotaDisplayRow row)
    {
        if (row.IsWeekly)
        {
            return null;
        }

        return _signals.RunwaySignals.FirstOrDefault(signal =>
            signal.BucketId.Equals(row.BucketId, StringComparison.OrdinalIgnoreCase)
            && signal.LimitKey.Equals(row.LimitKey, StringComparison.OrdinalIgnoreCase));
    }

    private LimitRunwaySignal? SelectedRunwaySignal
    {
        get
        {
            var selectedKey = SelectedLimitOption?.Key;
            if (string.IsNullOrWhiteSpace(selectedKey))
            {
                return null;
            }

            return _signals.RunwaySignals.FirstOrDefault(signal =>
                signal.LimitKey.Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void RefreshRunwayHintProperties()
    {
        OnPropertyChanged(nameof(HasRunwayHint));
        OnPropertyChanged(nameof(RunwayHintText));
        OnPropertyChanged(nameof(RunwayEvidenceText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
