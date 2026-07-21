using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace PulseMeter.Slices.ExpandedHeader.UI;

public sealed class ExpandedHeaderViewModel : INotifyPropertyChanged
{
    private string _compactTitleText = string.Empty;
    private string _statusBadgeText = string.Empty;
    private string _statusBadgeBrush = "#64748B";
    private string _lastUpdatedText = string.Empty;
    private string _lastUpdatedDetailText = string.Empty;
    private string _expandCollapseTooltip = string.Empty;
    private string _syncButtonText = "Sync now";
    private bool _isRefreshing;
    private ICommand? _syncNowCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CompactTitleText
    {
        get => _compactTitleText;
        private set => SetField(ref _compactTitleText, value);
    }

    public string StatusBadgeText
    {
        get => _statusBadgeText;
        private set => SetField(ref _statusBadgeText, value);
    }

    public string StatusBadgeBrush
    {
        get => _statusBadgeBrush;
        private set => SetField(ref _statusBadgeBrush, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetField(ref _lastUpdatedText, value);
    }

    public string LastUpdatedDetailText
    {
        get => _lastUpdatedDetailText;
        private set => SetField(ref _lastUpdatedDetailText, value);
    }

    public string StatusSummaryText => $"{StatusBadgeText}. {LastUpdatedDetailText}";

    public string SyncButtonText
    {
        get => _syncButtonText;
        private set => SetField(ref _syncButtonText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetField(ref _isRefreshing, value);
    }

    public string SyncButtonTooltip => IsRefreshing ? "Syncing usage" : "Sync usage (F5)";

    public string SyncButtonAccessibleLabel => IsRefreshing ? "Syncing usage" : "Sync usage now";

    public string ExpandCollapseTooltip
    {
        get => _expandCollapseTooltip;
        private set => SetField(ref _expandCollapseTooltip, value);
    }

    public ICommand? SyncNowCommand
    {
        get => _syncNowCommand;
        private set => SetField(ref _syncNowCommand, value);
    }

    public void ApplyState(
        string compactTitleText,
        string statusBadgeText,
        string statusBadgeBrush,
        string lastUpdatedText,
        string lastUpdatedDetailText,
        string expandCollapseTooltip,
        string syncButtonText,
        bool isRefreshing,
        ICommand syncNowCommand)
    {
        CompactTitleText = compactTitleText;
        StatusBadgeText = statusBadgeText;
        StatusBadgeBrush = statusBadgeBrush;
        LastUpdatedText = lastUpdatedText;
        LastUpdatedDetailText = lastUpdatedDetailText;
        ExpandCollapseTooltip = expandCollapseTooltip;
        SyncButtonText = syncButtonText;
        IsRefreshing = isRefreshing;
        SyncNowCommand = syncNowCommand;
        OnPropertyChanged(nameof(StatusSummaryText));
        OnPropertyChanged(nameof(SyncButtonTooltip));
        OnPropertyChanged(nameof(SyncButtonAccessibleLabel));
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
