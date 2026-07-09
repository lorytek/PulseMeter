using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseMeter.Slices.NavigationRail.UI;

public sealed class NavigationRailViewModel : INotifyPropertyChanged
{
    private const double ExpandedWidth = 205;
    private const double CollapsedWidth = 64;

    private bool _isNavigationPanelExpanded = true;
    private bool _isRateLimitsVisible = true;
    private bool _isRateLimitsDailyVisible = true;
    private bool _isResetCreditsVisible = true;
    private bool _isAccountUsageVisible = true;
    private bool _isProjectUsageVisible = true;
    private bool _isUsageAttributionVisible = true;
    private bool _isDailyUsageVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsNavigationPanelExpanded
    {
        get => _isNavigationPanelExpanded;
        private set
        {
            if (SetField(ref _isNavigationPanelExpanded, value))
            {
                OnPropertyChanged(nameof(NavigationPanelWidth));
                OnPropertyChanged(nameof(NavigationPanelToggleGlyph));
                OnPropertyChanged(nameof(NavigationPanelToggleTooltip));
            }
        }
    }

    public bool IsRateLimitsVisible
    {
        get => _isRateLimitsVisible;
        set => SetField(ref _isRateLimitsVisible, value);
    }

    public bool IsRateLimitsDailyVisible
    {
        get => _isRateLimitsDailyVisible;
        set => SetField(ref _isRateLimitsDailyVisible, value);
    }

    public bool IsResetCreditsVisible
    {
        get => _isResetCreditsVisible;
        set => SetField(ref _isResetCreditsVisible, value);
    }

    public bool IsAccountUsageVisible
    {
        get => _isAccountUsageVisible;
        set => SetField(ref _isAccountUsageVisible, value);
    }

    public bool IsProjectUsageVisible
    {
        get => _isProjectUsageVisible;
        set => SetField(ref _isProjectUsageVisible, value);
    }

    public bool IsUsageAttributionVisible
    {
        get => _isUsageAttributionVisible;
        set => SetField(ref _isUsageAttributionVisible, value);
    }

    public bool IsDailyUsageVisible
    {
        get => _isDailyUsageVisible;
        set => SetField(ref _isDailyUsageVisible, value);
    }

    public double NavigationPanelWidth => IsNavigationPanelExpanded
        ? ExpandedWidth
        : CollapsedWidth;

    public string NavigationPanelToggleTooltip => IsNavigationPanelExpanded
        ? "Collapse navigation"
        : "Expand navigation";

    public string NavigationPanelToggleGlyph => IsNavigationPanelExpanded
        ? "\u00E2\u20AC\u00B9"
        : "\u00E2\u20AC\u00BA";

    public void ToggleNavigationPanel()
    {
        IsNavigationPanelExpanded = !IsNavigationPanelExpanded;
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
