using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.RateLimits;

namespace PulseMeter.Slices.DataBar.UI;

public sealed class DataBarViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private string _statusBadgeText = string.Empty;
    private string _expandCollapseTooltip = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<QuotaDisplayRow> CompactQuotaRows { get; } = new();

    public bool IsWeeklyOnlyCompactLayout =>
        CompactQuotaRows.Count == 1 && CompactQuotaRows[0].IsWeekly;

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetField(ref _isExpanded, value);
    }

    public string StatusBadgeText
    {
        get => _statusBadgeText;
        private set => SetField(ref _statusBadgeText, value);
    }

    public string ExpandCollapseTooltip
    {
        get => _expandCollapseTooltip;
        private set => SetField(ref _expandCollapseTooltip, value);
    }

    public void ApplyState(
        bool isExpanded,
        IEnumerable<QuotaDisplayRow> compactQuotaRows,
        string statusBadgeText,
        string expandCollapseTooltip)
    {
        IsExpanded = isExpanded;
        StatusBadgeText = statusBadgeText;
        ExpandCollapseTooltip = expandCollapseTooltip;

        CompactQuotaRows.Clear();
        foreach (var row in compactQuotaRows)
        {
            CompactQuotaRows.Add(row);
        }

        OnPropertyChanged(nameof(IsWeeklyOnlyCompactLayout));
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
