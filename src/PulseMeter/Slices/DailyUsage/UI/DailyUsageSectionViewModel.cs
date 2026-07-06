using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.DailyUsage.UI;

public sealed class DailyUsageSectionViewModel : INotifyPropertyChanged
{
    private readonly IDailyUsagePresenter _presenter;
    private bool _isDailyUsageExpanded = true;

    public DailyUsageSectionViewModel(IDailyUsagePresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DailyUsageDisplayRow> DailyUsageRows { get; } = new();

    public DailyUsageMedianBaseline? MedianBaseline { get; private set; }

    public bool IsDailyUsageExpanded
    {
        get => _isDailyUsageExpanded;
        private set
        {
            if (SetField(ref _isDailyUsageExpanded, value))
            {
                OnPropertyChanged(nameof(DailyUsageExpandCollapseGlyph));
                OnPropertyChanged(nameof(DailyUsageExpandCollapseTooltip));
            }
        }
    }

    public bool HasDailyUsageMedianSummary => MedianBaseline is not null;

    public string DailyUsageMedianSummaryText => _presenter.FormatMedianSummaryText(MedianBaseline);

    public string DailyUsageWindowText => DailyUsageRows.Count == 1 ? "1 day" : $"{DailyUsageRows.Count} days";

    public string DailyUsageExpandCollapseGlyph => IsDailyUsageExpanded ? "-" : "+";

    public string DailyUsageExpandCollapseTooltip => IsDailyUsageExpanded
        ? "Collapse daily usage"
        : "Expand daily usage";

    public void ApplyBuckets(IReadOnlyList<DailyUsageBucket> buckets, DateOnly today)
    {
        var result = _presenter.BuildRows(buckets, today);
        MedianBaseline = result.MedianBaseline;

        DailyUsageRows.Clear();
        foreach (var row in result.Rows)
        {
            DailyUsageRows.Add(row);
        }

        OnPropertyChanged(nameof(MedianBaseline));
        OnPropertyChanged(nameof(DailyUsageWindowText));
        OnPropertyChanged(nameof(DailyUsageMedianSummaryText));
        OnPropertyChanged(nameof(HasDailyUsageMedianSummary));
    }

    public void ToggleDailyUsageExpanded()
    {
        IsDailyUsageExpanded = !IsDailyUsageExpanded;
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
