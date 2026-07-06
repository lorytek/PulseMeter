using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace PulseMeter.Slices.ExpandedHeader.UI;

public sealed class ExpandedHeaderViewModel : INotifyPropertyChanged
{
    private string _compactTitleText = string.Empty;
    private string _statusBadgeText = string.Empty;
    private string _expandCollapseTooltip = string.Empty;
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
        string expandCollapseTooltip,
        ICommand syncNowCommand)
    {
        CompactTitleText = compactTitleText;
        StatusBadgeText = statusBadgeText;
        ExpandCollapseTooltip = expandCollapseTooltip;
        SyncNowCommand = syncNowCommand;
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
