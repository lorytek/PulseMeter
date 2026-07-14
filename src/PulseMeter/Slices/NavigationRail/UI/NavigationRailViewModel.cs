using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.NavigationRail.Models;

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
    private NavigationSection _selectedSection = NavigationSection.Overview;

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
        set => SetVisibility(ref _isRateLimitsVisible, value, NavigationSection.RateLimits);
    }

    public bool IsRateLimitsDailyVisible
    {
        get => _isRateLimitsDailyVisible;
        set => SetVisibility(ref _isRateLimitsDailyVisible, value, NavigationSection.WeeklyPace);
    }

    public bool IsResetCreditsVisible
    {
        get => _isResetCreditsVisible;
        set => SetVisibility(ref _isResetCreditsVisible, value, NavigationSection.ResetCredits);
    }

    public bool IsAccountUsageVisible
    {
        get => _isAccountUsageVisible;
        set => SetVisibility(ref _isAccountUsageVisible, value, NavigationSection.AccountUsage);
    }

    public bool IsProjectUsageVisible
    {
        get => _isProjectUsageVisible;
        set => SetVisibility(ref _isProjectUsageVisible, value, NavigationSection.ProjectUsage);
    }

    public bool IsUsageAttributionVisible
    {
        get => _isUsageAttributionVisible;
        set => SetVisibility(ref _isUsageAttributionVisible, value, NavigationSection.BurnAnalysis);
    }

    public bool IsDailyUsageVisible
    {
        get => _isDailyUsageVisible;
        set => SetVisibility(ref _isDailyUsageVisible, value, NavigationSection.DailyUsage);
    }

    public NavigationSection SelectedSection
    {
        get => _selectedSection;
        private set => SetField(ref _selectedSection, value);
    }

    public double NavigationPanelWidth => IsNavigationPanelExpanded
        ? ExpandedWidth
        : CollapsedWidth;

    public string NavigationPanelToggleTooltip => IsNavigationPanelExpanded
        ? "Collapse navigation"
        : "Expand navigation";

    public string NavigationPanelToggleGlyph => IsNavigationPanelExpanded
        ? "\u2039"
        : "\u203A";

    public void ToggleNavigationPanel()
    {
        IsNavigationPanelExpanded = !IsNavigationPanelExpanded;
    }

    public void SelectSection(NavigationSection section)
    {
        SelectedSection = IsSectionVisible(section) ? section : NavigationSection.Overview;
    }

    public void ApplyVisibility(DashboardVisibilitySettings? visibility)
    {
        var settings = visibility ?? new DashboardVisibilitySettings();
        IsRateLimitsVisible = settings.RateLimits;
        IsRateLimitsDailyVisible = settings.WeeklyPace;
        IsResetCreditsVisible = settings.ResetCredits;
        IsAccountUsageVisible = settings.AccountUsage;
        IsProjectUsageVisible = settings.ProjectUsage;
        IsUsageAttributionVisible = settings.BurnAnalysis;
        IsDailyUsageVisible = settings.DailyUsage;
    }

    public DashboardVisibilitySettings CaptureVisibility()
    {
        return new DashboardVisibilitySettings(
            IsRateLimitsVisible,
            IsRateLimitsDailyVisible,
            IsResetCreditsVisible,
            IsAccountUsageVisible,
            IsProjectUsageVisible,
            IsUsageAttributionVisible,
            IsDailyUsageVisible);
    }

    private bool SetVisibility(ref bool field, bool value, NavigationSection section, [CallerMemberName] string? propertyName = null)
    {
        if (!SetField(ref field, value, propertyName))
        {
            return false;
        }

        if (!value && SelectedSection == section)
        {
            SelectedSection = NavigationSection.Overview;
        }

        return true;
    }

    private bool IsSectionVisible(NavigationSection section)
    {
        return section switch
        {
            NavigationSection.Overview => true,
            NavigationSection.RateLimits => IsRateLimitsVisible,
            NavigationSection.WeeklyPace => IsRateLimitsDailyVisible,
            NavigationSection.ResetCredits => IsResetCreditsVisible,
            NavigationSection.AccountUsage => IsAccountUsageVisible,
            NavigationSection.ProjectUsage => IsProjectUsageVisible,
            NavigationSection.BurnAnalysis => IsUsageAttributionVisible,
            NavigationSection.DailyUsage => IsDailyUsageVisible,
            _ => false
        };
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
