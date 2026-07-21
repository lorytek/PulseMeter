using System.ComponentModel;
using System.Globalization;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;
using System.Reflection;

namespace PulseMeter.Tests;

public sealed class NavigationRailViewModelTests
{
    [Fact]
    public void ApplicationVersionText_UsesTheActualAssemblyVersion()
    {
        var viewModel = new NavigationRailViewModel();
        var version = typeof(NavigationRailViewModel)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+', 2)[0];

        Assert.Equal($"PulseMeter · v{version}", viewModel.ApplicationVersionText);
    }

    [Fact]
    public void SelectedSectionConverter_MatchesOnlyTheCurrentNavigationSection()
    {
        var converter = new NavigationSectionSelectedConverter();

        Assert.Equal(
            "Current",
            converter.Convert(
                NavigationSection.RunwayForecast,
                typeof(bool),
                NavigationSection.RunwayForecast,
                CultureInfo.InvariantCulture));
        Assert.Equal(
            string.Empty,
            converter.Convert(
                NavigationSection.RunwayForecast,
                typeof(bool),
                NavigationSection.ResetCredits,
                CultureInfo.InvariantCulture));
        Assert.Equal(
            string.Empty,
            converter.Convert(
                NavigationSection.RunwayForecast,
                typeof(bool),
                parameter: null!,
                CultureInfo.InvariantCulture));
    }

    [Fact]
    public void StartsAtOverview()
    {
        var viewModel = new NavigationRailViewModel();

        Assert.Equal(NavigationSection.Overview, viewModel.SelectedSection);
    }

    [Fact]
    public void PanelToggle_UsesNativeChevronGlyphsAndCanRestoreSavedState()
    {
        var viewModel = new NavigationRailViewModel();

        Assert.True(viewModel.IsNavigationPanelExpanded);
        Assert.Equal("\uE76B", viewModel.NavigationPanelToggleGlyph);
        Assert.Equal("Collapse navigation", viewModel.NavigationPanelToggleTooltip);

        viewModel.ToggleNavigationPanel();

        Assert.False(viewModel.IsNavigationPanelExpanded);
        Assert.Equal("\uE76C", viewModel.NavigationPanelToggleGlyph);
        Assert.Equal("Expand navigation", viewModel.NavigationPanelToggleTooltip);

        viewModel.ApplyPanelState(true);

        Assert.True(viewModel.IsNavigationPanelExpanded);
    }

    [Fact]
    public void SelectingSectionRaisesPropertyChangedWithoutChangingVisibility()
    {
        var viewModel = new NavigationRailViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        viewModel.SelectSection(NavigationSection.BurnAnalysis);

        Assert.Equal(NavigationSection.BurnAnalysis, viewModel.SelectedSection);
        Assert.True(viewModel.IsUsageAttributionVisible);
        Assert.Contains(nameof(NavigationRailViewModel.SelectedSection), changed);
    }

    [Fact]
    public void ApplyingVisibilityRoundTripsAllDashboardSections()
    {
        var viewModel = new NavigationRailViewModel();
        var visibility = new DashboardVisibilitySettings(
            RateLimits: false,
            WeeklyPace: true,
            RunwayForecast: false,
            ResetCredits: false,
            AccountUsage: true,
            ProjectUsage: false,
            BurnAnalysis: true,
            DailyUsage: false);

        viewModel.ApplyVisibility(visibility);

        Assert.Equal(visibility, viewModel.CaptureVisibility());
        Assert.Equal(3, viewModel.VisibleSectionCount);
        Assert.Equal("3 of 8 visible · changes save automatically", viewModel.VisibleSectionSummaryText);
        Assert.True(viewModel.HasHiddenSections);
    }

    [Fact]
    public void VisibilitySummary_TracksCustomizationAndRestoreAllState()
    {
        var viewModel = new NavigationRailViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);

        Assert.Equal(8, viewModel.VisibleSectionCount);
        Assert.False(viewModel.HasHiddenSections);

        viewModel.IsDailyUsageVisible = false;

        Assert.Equal(7, viewModel.VisibleSectionCount);
        Assert.Equal("7 of 8 visible · changes save automatically", viewModel.VisibleSectionSummaryText);
        Assert.True(viewModel.HasHiddenSections);
        Assert.Contains(nameof(NavigationRailViewModel.VisibleSectionCount), changed);
        Assert.Contains(nameof(NavigationRailViewModel.VisibleSectionSummaryText), changed);
        Assert.Contains(nameof(NavigationRailViewModel.HasHiddenSections), changed);

        viewModel.ApplyVisibility(new DashboardVisibilitySettings());

        Assert.Equal(8, viewModel.VisibleSectionCount);
        Assert.False(viewModel.HasHiddenSections);
    }

    [Fact]
    public void HidingSelectedSectionReturnsToOverview()
    {
        var viewModel = new NavigationRailViewModel();
        viewModel.SelectSection(NavigationSection.ProjectUsage);

        viewModel.IsProjectUsageVisible = false;

        Assert.Equal(NavigationSection.Overview, viewModel.SelectedSection);
    }

    [Fact]
    public void HidingDifferentSectionKeepsCurrentSelection()
    {
        var viewModel = new NavigationRailViewModel();
        viewModel.SelectSection(NavigationSection.AccountUsage);

        viewModel.IsDailyUsageVisible = false;

        Assert.Equal(NavigationSection.AccountUsage, viewModel.SelectedSection);
    }

    [Fact]
    public void RevealAndSelectSection_RestoresUserHiddenSection()
    {
        var viewModel = new NavigationRailViewModel
        {
            IsProjectUsageVisible = false
        };

        viewModel.RevealAndSelectSection(NavigationSection.ProjectUsage);

        Assert.True(viewModel.IsProjectUsageVisible);
        Assert.Equal(NavigationSection.ProjectUsage, viewModel.SelectedSection);
    }

    [Fact]
    public void RemovedUsageExplorerIsNotANavigationDestination()
    {
        Assert.DoesNotContain("UsageExplorer", Enum.GetNames<NavigationSection>());
    }
}
