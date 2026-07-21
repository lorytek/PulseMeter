using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.NavigationRail.Models;

namespace PulseMeter.Slices.NavigationRail.UI;

public sealed class NavigationRailViewModel : INotifyPropertyChanged
{
    private const double ExpandedWidth = 205;
    private const double CollapsedWidth = 64;
    private const int CustomizableSectionCount = 8;

    private bool _isNavigationPanelExpanded = true;
    private bool _isRateLimitsVisible = true;
    private bool _isRateLimitsDailyVisible = true;
    private bool _isRunwayForecastVisible = true;
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

    public bool IsRunwayForecastVisible
    {
        get => _isRunwayForecastVisible;
        set => SetVisibility(ref _isRunwayForecastVisible, value, NavigationSection.RunwayForecast);
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
        ? "\uE76B"
        : "\uE76C";

    public string ApplicationVersionText { get; } = BuildApplicationVersionText();

    public int VisibleSectionCount =>
        (IsRateLimitsVisible ? 1 : 0) +
        (IsRateLimitsDailyVisible ? 1 : 0) +
        (IsRunwayForecastVisible ? 1 : 0) +
        (IsResetCreditsVisible ? 1 : 0) +
        (IsAccountUsageVisible ? 1 : 0) +
        (IsProjectUsageVisible ? 1 : 0) +
        (IsUsageAttributionVisible ? 1 : 0) +
        (IsDailyUsageVisible ? 1 : 0);

    public string VisibleSectionSummaryText => $"{VisibleSectionCount} of {CustomizableSectionCount} visible · changes save automatically";

    public bool HasHiddenSections => VisibleSectionCount < CustomizableSectionCount;

    private static string BuildApplicationVersionText()
    {
        var informationalVersion = typeof(NavigationRailViewModel)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = informationalVersion?.Split('+', 2)[0]
            ?? typeof(NavigationRailViewModel).Assembly.GetName().Version?.ToString(3)
            ?? "Unknown";
        return $"PulseMeter · v{version}";
    }

    public void ToggleNavigationPanel()
    {
        IsNavigationPanelExpanded = !IsNavigationPanelExpanded;
    }

    public void ApplyPanelState(bool isExpanded)
    {
        IsNavigationPanelExpanded = isExpanded;
    }

    public void SelectSection(NavigationSection section)
    {
        SelectedSection = IsSectionVisible(section) ? section : NavigationSection.Overview;
    }

    public void RevealAndSelectSection(NavigationSection section)
    {
        switch (section)
        {
            case NavigationSection.RateLimits:
                IsRateLimitsVisible = true;
                break;
            case NavigationSection.WeeklyPace:
                IsRateLimitsDailyVisible = true;
                break;
            case NavigationSection.RunwayForecast:
                IsRunwayForecastVisible = true;
                break;
            case NavigationSection.ResetCredits:
                IsResetCreditsVisible = true;
                break;
            case NavigationSection.AccountUsage:
                IsAccountUsageVisible = true;
                break;
            case NavigationSection.ProjectUsage:
                IsProjectUsageVisible = true;
                break;
            case NavigationSection.BurnAnalysis:
                IsUsageAttributionVisible = true;
                break;
            case NavigationSection.DailyUsage:
                IsDailyUsageVisible = true;
                break;
        }

        SelectSection(section);
    }

    public void ApplyVisibility(DashboardVisibilitySettings? visibility)
    {
        var settings = visibility ?? new DashboardVisibilitySettings();
        IsRateLimitsVisible = settings.RateLimits;
        IsRateLimitsDailyVisible = settings.WeeklyPace;
        IsRunwayForecastVisible = settings.RunwayForecast;
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
            IsRunwayForecastVisible,
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

        OnPropertyChanged(nameof(VisibleSectionCount));
        OnPropertyChanged(nameof(VisibleSectionSummaryText));
        OnPropertyChanged(nameof(HasHiddenSections));

        return true;
    }

    private bool IsSectionVisible(NavigationSection section)
    {
        return section switch
        {
            NavigationSection.Overview => true,
            NavigationSection.RateLimits => IsRateLimitsVisible,
            NavigationSection.WeeklyPace => IsRateLimitsDailyVisible,
            NavigationSection.RunwayForecast => IsRunwayForecastVisible,
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
