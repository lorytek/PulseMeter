using System.ComponentModel;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.NavigationRail.Models;
using PulseMeter.Slices.NavigationRail.UI;

namespace PulseMeter.Tests;

public sealed class NavigationRailViewModelTests
{
    [Fact]
    public void StartsAtOverview()
    {
        var viewModel = new NavigationRailViewModel();

        Assert.Equal(NavigationSection.Overview, viewModel.SelectedSection);
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
    public void RemovedUsageExplorerIsNotANavigationDestination()
    {
        Assert.DoesNotContain("UsageExplorer", Enum.GetNames<NavigationSection>());
    }
}
