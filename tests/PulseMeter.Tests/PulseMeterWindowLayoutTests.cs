namespace PulseMeter.Tests;

public sealed class PulseMeterWindowLayoutTests
{
    [Fact]
    public void ExpandedPulseMeter_UsesSeparateSectionControlsInWindowMarkup()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");

        Assert.Contains("xmlns:rateLimits=\"clr-namespace:PulseMeter.Slices.RateLimits.UI\"", windowXaml);
        Assert.Contains("xmlns:rateLimitsDaily=\"clr-namespace:PulseMeter.Slices.RateLimitsDaily.UI\"", windowXaml);
        Assert.DoesNotContain("xmlns:runwayForecast=\"clr-namespace:PulseMeter.Slices.RunwayForecast.UI\"", windowXaml);
        Assert.Contains("xmlns:resetCredits=\"clr-namespace:PulseMeter.Slices.ResetCredits.UI\"", windowXaml);
        Assert.Contains("xmlns:needsAttention=\"clr-namespace:PulseMeter.Slices.NeedsAttention.UI\"", windowXaml);
        Assert.Contains("xmlns:accountUsage=\"clr-namespace:PulseMeter.Slices.AccountUsage.UI\"", windowXaml);
        Assert.Contains("xmlns:projectUsage=\"clr-namespace:PulseMeter.Slices.ProjectUsage.UI\"", windowXaml);
        Assert.Contains("xmlns:usageAttribution=\"clr-namespace:PulseMeter.Slices.UsageAttribution.UI\"", windowXaml);
        Assert.Contains("xmlns:dailyUsage=\"clr-namespace:PulseMeter.Slices.DailyUsage.UI\"", windowXaml);
        Assert.Contains("<rateLimits:RateLimitsSection", windowXaml);
        Assert.Contains("<rateLimitsDaily:RateLimitsDailySection", windowXaml);
        Assert.DoesNotContain("<runwayForecast:RunwayForecastSection", windowXaml);
        Assert.Contains("<resetCredits:ResetCreditsSection", windowXaml);
        Assert.Contains("<needsAttention:NeedsAttentionSection", windowXaml);
        Assert.Contains("<accountUsage:AccountUsageSection", windowXaml);
        Assert.Contains("<projectUsage:ProjectUsageSection", windowXaml);
        Assert.Contains("<usageAttribution:UsageAttributionSection", windowXaml);
        Assert.Contains("<dailyUsage:DailyUsageSection", windowXaml);
        Assert.DoesNotContain("xmlns:budgetAlerts", windowXaml);
        Assert.DoesNotContain("<budgetAlerts:BudgetAlertsSection", windowXaml);
        Assert.DoesNotContain("x:Name=\"RateLimitsPanel\"", windowXaml);
        Assert.DoesNotContain("x:Name=\"DailyUsagePanel\"", windowXaml);
    }

    [Fact]
    public void CompactPulseMeter_UsesDataBarComponentInWindowMarkup()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");

        Assert.Contains("xmlns:dataBar=\"clr-namespace:PulseMeter.Slices.DataBar.UI\"", windowXaml);
        Assert.Contains("<dataBar:DataBar", windowXaml);
        Assert.Contains("DataContext=\"{Binding DataBar}\"", windowXaml);
        Assert.Contains("ToggleExpandedRequested=\"ExpandCollapseButton_Click\"", windowXaml);
        Assert.Contains("HideRequested=\"HideButton_Click\"", windowXaml);
        Assert.DoesNotContain("x:Name=\"CompactHeaderGrid\"", windowXaml);
        Assert.DoesNotContain("x:Name=\"CompactQuotaSummaryItemsControl\"", windowXaml);
        Assert.DoesNotContain("x:Name=\"CompactHeaderControls\"", windowXaml);
    }

    [Fact]
    public void ExpandedPulseMeter_UsesNavigationRailComponentInWindowMarkup()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");

        Assert.Contains("xmlns:navigationRail=\"clr-namespace:PulseMeter.Slices.NavigationRail.UI\"", windowXaml);
        Assert.Contains("<navigationRail:NavigationRail", windowXaml);
        Assert.Contains("DataContext=\"{Binding NavigationRail}\"", windowXaml);
        Assert.Contains("Grid.Column=\"0\"", windowXaml);
        Assert.DoesNotContain("x:Key=\"NavigationSectionToggleStyle\"", windowXaml);
        Assert.DoesNotContain("IsRateLimitsVisible", windowXaml);
        Assert.DoesNotContain("NavigationPanelToggleButton_Click", windowXaml);
    }

    [Fact]
    public void NavigationRail_UsesChildViewModelForPanelAndSectionState()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var navigationRegistration = ReadSliceRegistration("NavigationRail", "NavigationRailRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var navigationCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml.cs"));

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRailViewModel.cs")));
        Assert.Contains("<navigationRail:NavigationRail Grid.Column=\"0\"", windowXaml);
        Assert.Contains("DataContext=\"{Binding NavigationRail}\"", windowXaml);
        Assert.Contains("public NavigationRailViewModel NavigationRail { get; }", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<NavigationRailViewModel>", navigationRegistration);
        Assert.Contains("NavigationRailViewModel", navigationCode);
        Assert.DoesNotContain("PulseMeterWindowViewModel", navigationCode);
    }

    [Fact]
    public void ExpandedPulseMeter_UsesExpandedHeaderComponentInWindowMarkup()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var headerXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml");
        var headerCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml.cs"));

        Assert.Contains("<expandedHeader:ExpandedHeader", windowXaml);
        Assert.Contains("DataContext=\"{Binding ExpandedHeader}\"", windowXaml);
        Assert.Contains("ToggleExpandedRequested=\"ExpandCollapseButton_Click\"", windowXaml);
        Assert.Contains("HideRequested=\"HideButton_Click\"", windowXaml);
        Assert.DoesNotContain("x:Name=\"ExpandedHeaderLogo\"", windowXaml);
        Assert.DoesNotContain("StatusBadgeText", windowXaml);
        Assert.Contains("x:Name=\"ExpandedStickyHeader\"", headerXaml);
        Assert.Contains("x:Name=\"ExpandedHeaderLogo\"", headerXaml);
        Assert.Contains("public event RoutedEventHandler? ToggleExpandedRequested", headerCode);
        Assert.Contains("public event RoutedEventHandler? HideRequested", headerCode);
        Assert.DoesNotContain("PulseMeterWindowViewModel", headerCode);
        Assert.DoesNotContain("Window.GetWindow", headerCode);
    }

    [Fact]
    public void ExpandedPulseMeter_ShowsActionableSyncIssuesOutsideCustomizableSections()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var rateLimitsXaml = ReadXamlFile("src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSection.xaml");

        var bannerStart = windowXaml.IndexOf("x:Name=\"SyncIssueBanner\"", StringComparison.Ordinal);
        var scrollViewerStart = windowXaml.IndexOf("x:Name=\"ExpandedContentScrollViewer\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, bannerStart);
        Assert.NotEqual(-1, scrollViewerStart);
        Assert.True(bannerStart < scrollViewerStart);
        Assert.Contains("Visibility=\"{Binding HasActionableSyncIssue", windowXaml);
        Assert.Contains("Text=\"{Binding SyncIssueTitle}\"", windowXaml);
        Assert.Contains("Text=\"{Binding SyncIssueText}\"", windowXaml);
        Assert.Contains("Command=\"{Binding SyncNowCommand}\"", windowXaml[bannerStart..scrollViewerStart]);
        Assert.Contains("AutomationProperties.LiveSetting=\"Assertive\"", windowXaml[bannerStart..scrollViewerStart]);
        Assert.Contains("AutomationProperties.Name=\"Retry usage sync\"", windowXaml[bannerStart..scrollViewerStart]);
        Assert.Contains("HasSectionStatusMessage", rateLimitsXaml);
    }

    [Fact]
    public void DataBarAndExpandedHeader_UseChildViewModelsForDisplayState()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var dataBarRegistration = ReadSliceRegistration("DataBar", "DataBarRegistration.cs");
        var expandedHeaderRegistration = ReadSliceRegistration("ExpandedHeader", "ExpandedHeaderRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var dataBarXaml = ReadXamlFile("src", "PulseMeter", "Slices", "DataBar", "DataBar.xaml");
        var expandedHeaderXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml");

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "DataBar", "DataBarViewModel.cs")));
        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeaderViewModel.cs")));
        Assert.Contains("<dataBar:DataBar Grid.Row=\"0\"", windowXaml);
        Assert.Contains("DataContext=\"{Binding DataBar}\"", windowXaml);
        Assert.Contains("<expandedHeader:ExpandedHeader Grid.Row=\"0\"", windowXaml);
        Assert.Contains("DataContext=\"{Binding ExpandedHeader}\"", windowXaml);
        Assert.Contains("public DataBarViewModel DataBar { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public ExpandedHeaderViewModel ExpandedHeader { get; }", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<DataBarViewModel>", dataBarRegistration);
        Assert.Contains("AddSingleton<ExpandedHeaderViewModel>", expandedHeaderRegistration);
        Assert.Contains("ItemsSource=\"{Binding CompactQuotaRows}\"", dataBarXaml);
        Assert.Contains("Text=\"{Binding StatusBadgeText}\"", dataBarXaml);
        Assert.Contains("Background=\"{Binding StatusBadgeBrush}\"", dataBarXaml);
        Assert.Contains("Foreground=\"{Binding StatusBadgeBrush}\"", dataBarXaml);
        Assert.Contains("ToolTip=\"{Binding StatusSummaryText}\"", dataBarXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding StatusSummaryText}\"", dataBarXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding CompactAccessibleSummary}\"", dataBarXaml);
        Assert.Contains("ToolTip=\"{Binding CompactAccessibleSummary}\"", dataBarXaml);
        Assert.Contains("ToolTip=\"{Binding ExpandCollapseTooltip}\"", dataBarXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding ExpandCollapseTooltip}\"", dataBarXaml);
        Assert.Contains("AutomationProperties.Name=\"Hide PulseMeter\"", dataBarXaml);
        Assert.Contains("Text=\"{Binding CompactTitleText}\"", expandedHeaderXaml);
        Assert.Contains("Text=\"{Binding StatusBadgeText}\"", expandedHeaderXaml);
        Assert.Contains("Text=\"{Binding LastUpdatedText}\"", expandedHeaderXaml);
        Assert.Contains("ToolTip=\"{Binding LastUpdatedDetailText}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding LastUpdatedDetailText}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding StatusSummaryText}\"", expandedHeaderXaml);
        Assert.Contains("Background=\"{Binding StatusBadgeBrush}\"", expandedHeaderXaml);
        Assert.Contains("Command=\"{Binding SyncNowCommand}\"", expandedHeaderXaml);
        Assert.Contains("Text=\"{Binding SyncButtonText}\"", expandedHeaderXaml);
        Assert.Contains("ToolTip=\"{Binding SyncButtonTooltip}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding SyncButtonAccessibleLabel}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.AcceleratorKey=\"F5\"", expandedHeaderXaml);
        Assert.Contains("IsChecked=\"{Binding DataContext.IsAlwaysOnTop, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"Always on top\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.HelpText=\"Keep PulseMeter above other windows\"", expandedHeaderXaml);
        Assert.Contains("Style=\"{DynamicResource LightIconToggleButtonStyle}\"", expandedHeaderXaml);
        Assert.Contains("ToolTip=\"{Binding ExpandCollapseTooltip}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding ExpandCollapseTooltip}\"", expandedHeaderXaml);
        Assert.Contains("AutomationProperties.Name=\"Hide PulseMeter\"", expandedHeaderXaml);
    }

    [Fact]
    public void PulseMeterWindow_UsesSharedPulseMeterControlStyleDictionary()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var stylesXaml = ReadXamlFile("src", "PulseMeter", "Shared", "Styles", "PulseMeterControls.xaml");

        Assert.Contains("Source=\"/PulseMeter;component/Shared/Styles/PulseMeterControls.xaml\"", windowXaml);
        Assert.DoesNotContain("x:Key=\"LightCardStyle\"", windowXaml);
        Assert.DoesNotContain("x:Key=\"CompactIconButtonStyle\"", windowXaml);
        Assert.Contains("x:Key=\"LightCardStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"CompactIconButtonStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightIconToggleButtonStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightTextBoxStyle\"", stylesXaml);
        Assert.Contains("<Trigger Property=\"IsKeyboardFocused\" Value=\"True\">", stylesXaml);
        Assert.Contains("<Trigger Property=\"Validation.HasError\" Value=\"True\">", stylesXaml);
        Assert.Contains("Path=(Validation.Errors)[0].ErrorContent", stylesXaml);
        Assert.Contains("x:Key=\"LightEvidencePillStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightEvidencePillTextStyle\"", stylesXaml);
        Assert.Contains("<KeyBinding Key=\"F5\" Command=\"{Binding SyncNowCommand}\" />", windowXaml);
    }

    [Fact]
    public void UsageSections_ShowCompactEvidenceLabels()
    {
        var accountUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSection.xaml");
        var projectUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml");
        var usageAttributionSection = ReadXamlFile("src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml");
        var dailyUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml");

        Assert.Contains("x:Name=\"AccountUsageEvidenceLabel\"", accountUsageSection);
        Assert.Contains("Text=\"{Binding DataContext.SyncStatusText, RelativeSource={RelativeSource AncestorType=Window}}\"", accountUsageSection);
        Assert.Contains("Style=\"{DynamicResource LightEvidencePillStyle}\"", accountUsageSection);
        Assert.Contains("Style=\"{DynamicResource LightEvidencePillTextStyle}\"", accountUsageSection);

        Assert.Contains("x:Name=\"ProjectUsageEstimatedEvidenceLabel\"", projectUsageSection);
        Assert.Contains("Text=\"Estimated\"", projectUsageSection);
        Assert.Contains("x:Name=\"ProjectUsageLocalEvidenceLabel\"", projectUsageSection);
        Assert.Contains("Text=\"Local\"", projectUsageSection);

        Assert.Contains("x:Name=\"BurnAnalysisEstimatedEvidenceLabel\"", usageAttributionSection);
        Assert.Contains("Text=\"Estimated\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisLocalEvidenceLabel\"", usageAttributionSection);
        Assert.Contains("Text=\"Local\"", usageAttributionSection);

        Assert.Contains("x:Name=\"DailyUsageEvidenceLabel\"", dailyUsageSection);
        Assert.Contains("Text=\"{Binding DataContext.SyncStatusText, RelativeSource={RelativeSource AncestorType=Window}}\"", dailyUsageSection);
    }

    [Fact]
    public void AccountUsageHeader_CentersTitleLabelAndRefreshControls()
    {
        var accountUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSection.xaml");
        var headerStart = accountUsageSection.IndexOf("x:Name=\"DashboardHeader\"", StringComparison.Ordinal);
        var metricCardsStart = accountUsageSection.IndexOf("x:Name=\"DashboardMetricCards\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, headerStart);
        Assert.NotEqual(-1, metricCardsStart);
        Assert.True(headerStart < metricCardsStart);

        var header = accountUsageSection[headerStart..metricCardsStart];
        Assert.Contains("MinHeight=\"36\"", header);
        Assert.Contains("x:Name=\"AccountUsageHeaderTitleGroup\"", header);
        Assert.Contains("x:Name=\"AccountUsageRefreshControls\"", header);

        var titleGroupStart = header.IndexOf("x:Name=\"AccountUsageHeaderTitleGroup\"", StringComparison.Ordinal);
        var refreshControlsStart = header.IndexOf("x:Name=\"AccountUsageRefreshControls\"", StringComparison.Ordinal);
        Assert.NotEqual(-1, titleGroupStart);
        Assert.NotEqual(-1, refreshControlsStart);
        Assert.True(titleGroupStart < refreshControlsStart);

        var titleGroup = header[titleGroupStart..refreshControlsStart];
        Assert.Contains("VerticalAlignment=\"Center\"", titleGroup);
        Assert.Contains("Text=\"ACCOUNT USAGE\"", titleGroup);
        Assert.Contains("Margin=\"10,0,0,0\"", titleGroup);

        var refreshControls = header[refreshControlsStart..];
        Assert.Contains("Grid.Column=\"1\"", refreshControls);
        Assert.Contains("VerticalAlignment=\"Center\"", refreshControls);
        Assert.Contains("Text=\"Auto refresh every\"", refreshControls);
        Assert.Contains("Text=\"seconds\"", refreshControls);
    }

    [Fact]
    public void PulseMeterSliceModules_OwnTheirSectionControlsAndDisplayLogic()
    {
        var modules = new[]
        {
            ("DataBar", "DataBar.xaml", "DataBar.xaml.cs", "DataBarRegistration.cs"),
            ("NavigationRail", "NavigationRail.xaml", "NavigationRail.xaml.cs", "NavigationRailRegistration.cs"),
            ("ExpandedHeader", "ExpandedHeader.xaml", "ExpandedHeader.xaml.cs", "ExpandedHeaderRegistration.cs"),
            ("RateLimits", "RateLimitsSection.xaml", "RateLimitsPresenter.cs", "RateLimitsRegistration.cs"),
            ("RateLimitsDaily", "RateLimitsDailySection.xaml", "RateLimitsDailyPresenter.cs", "RateLimitsDailyRegistration.cs"),
            ("RunwayForecast", "RunwayForecastSection.xaml", "RunwayForecastPresenter.cs", "RunwayForecastRegistration.cs"),
            ("ResetCredits", "ResetCreditsSection.xaml", "ResetCreditsPresenter.cs", "ResetCreditsRegistration.cs"),
            ("NeedsAttention", "NeedsAttentionSection.xaml", "NeedsAttentionPresenter.cs", "NeedsAttentionRegistration.cs"),
            ("AccountUsage", "AccountUsageSection.xaml", "AccountUsagePresenter.cs", "AccountUsageRegistration.cs"),
            ("ProjectUsage", "ProjectUsageSection.xaml", "ProjectUsagePresenter.cs", "ProjectUsageRegistration.cs"),
            ("UsageAttribution", "UsageAttributionSection.xaml", "UsageAttributionPresenter.cs", "UsageAttributionRegistration.cs"),
            ("DailyUsage", "DailyUsageSection.xaml", "DailyUsagePresenter.cs", "DailyUsageRegistration.cs"),
            ("UsageSignals", "UsageSignalsTracker.cs", "UsageSignalsRegistration.cs", "UsageSignalsSnapshot.cs")
        };

        foreach (var (module, control, logic, registration) in modules)
        {
            Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", module, control)));
            Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", module, logic)));
            Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", module, registration)));
        }

        var root = TestWorkspace.FindRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "src", "PulseMeter", "Views")));
        Assert.False(Directory.Exists(Path.Combine(root, "src", "PulseMeter", "ViewModels")));
        Assert.False(Directory.Exists(Path.Combine(root, "src", "PulseMeter", "Views", "Components")));
        Assert.False(Directory.Exists(Path.Combine(root, "src", "PulseMeter", "Views", "Sections")));
    }

    [Fact]
    public void PulseMeterSliceModules_ExposePresentationContractsAndUseSliceRegistrations()
    {
        var modules = new[]
        {
            ("RateLimits", "IRateLimitsPresenter", "RateLimitsPresenter", "RateLimitsSectionViewModel", "RateLimitsRegistration.cs", "AddRateLimitsSlice"),
            ("RateLimitsDaily", "IRateLimitsDailyPresenter", "RateLimitsDailyPresenter", "RateLimitsDailySectionViewModel", "RateLimitsDailyRegistration.cs", "AddRateLimitsDailySlice"),
            ("RunwayForecast", "IRunwayForecastPresenter", "RunwayForecastPresenter", "RunwayForecastSectionViewModel", "RunwayForecastRegistration.cs", "AddRunwayForecastSlice"),
            ("ResetCredits", "IResetCreditsPresenter", "ResetCreditsPresenter", "ResetCreditsSectionViewModel", "ResetCreditsRegistration.cs", "AddResetCreditsSlice"),
            ("NeedsAttention", "INeedsAttentionPresenter", "NeedsAttentionPresenter", "NeedsAttentionSectionViewModel", "NeedsAttentionRegistration.cs", "AddNeedsAttentionSlice"),
            ("AccountUsage", "IAccountUsagePresenter", "AccountUsagePresenter", "AccountUsageSectionViewModel", "AccountUsageRegistration.cs", "AddAccountUsageSlice"),
            ("ProjectUsage", "IProjectUsagePresenter", "ProjectUsagePresenter", "ProjectUsageSectionViewModel", "ProjectUsageRegistration.cs", "AddProjectUsageSlice"),
            ("UsageAttribution", "IUsageAttributionPresenter", "UsageAttributionPresenter", "UsageAttributionSectionViewModel", "UsageAttributionRegistration.cs", "AddUsageAttributionSlice"),
            ("DailyUsage", "IDailyUsagePresenter", "DailyUsagePresenter", "DailyUsageSectionViewModel", "DailyUsageRegistration.cs", "AddDailyUsageSlice"),
            ("UsageSignals", "IUsageSignalsTracker", "UsageSignalsTracker", null, "UsageSignalsRegistration.cs", "AddUsageSignalsSlice")
        };

        foreach (var (module, contractName, implementationName, viewModelName, registrationFileName, registrationMethodName) in modules)
        {
            var presenter = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", module, $"{implementationName}.cs"));
            var registration = ReadSliceRegistration(module, registrationFileName);

            Assert.Contains($"public interface {contractName}", presenter);
            Assert.Contains($"public sealed class {implementationName} : {contractName}", presenter);
            Assert.Contains($"internal static IServiceCollection {registrationMethodName}", registration);
            Assert.Contains($"AddSingleton<{contractName}, {implementationName}>", registration);
            if (viewModelName is not null)
            {
                var owner = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", module, $"{viewModelName}.cs"));
                Assert.Contains($"AddSingleton<{viewModelName}>", registration);
                Assert.Contains(contractName, owner);
            }
        }
    }

    [Fact]
    public void PulseMeterSliceModules_RegisterSimpleViewModelSlicesLocally()
    {
        var modules = new[]
        {
            ("DataBar", "DataBarViewModel", "DataBarRegistration.cs", "AddDataBarSlice"),
            ("ExpandedHeader", "ExpandedHeaderViewModel", "ExpandedHeaderRegistration.cs", "AddExpandedHeaderSlice"),
            ("NavigationRail", "NavigationRailViewModel", "NavigationRailRegistration.cs", "AddNavigationRailSlice")
        };

        foreach (var (module, viewModelName, registrationFileName, registrationMethodName) in modules)
        {
            var registration = ReadSliceRegistration(module, registrationFileName);

            Assert.Contains($"internal static IServiceCollection {registrationMethodName}", registration);
            Assert.Contains($"AddSingleton<{viewModelName}>", registration);
        }
    }

    [Fact]
    public void PulseMeterCompositionRoot_DelegatesToGroupedRegistrationMethods()
    {
        var compositionRoot = ReadCompositionFile("PulseMeterCompositionRoot.cs");
        var pulseMeterSlicesRegistration = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "Business", "PulseMeterSlicesRegistration.cs"));

        Assert.Contains("new ServiceCollection()", compositionRoot);
        Assert.Contains("services.AddUsageCollection();", compositionRoot);
        Assert.Contains("services.AddPulseMeterSlices();", compositionRoot);
        Assert.Contains("services.AddPulseMeterPlatform();", compositionRoot);
        Assert.Contains("services.AddPulseMeterWindow(shutdown);", compositionRoot);
        Assert.Contains("return services.BuildServiceProvider();", compositionRoot);
        Assert.DoesNotContain("AddSingleton<", compositionRoot);
        Assert.Contains("internal static IServiceCollection AddPulseMeterSlices", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddDataBarSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddExpandedHeaderSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddNavigationRailSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddRateLimitsSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddRateLimitsDailySlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddRunwayForecastSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddNeedsAttentionSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddBudgetAlertsSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddResetCreditsSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddAccountUsageSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddProjectUsageSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddUsageAttributionSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddDailyUsageSlice();", pulseMeterSlicesRegistration);
        Assert.Contains("services.AddUsageSignalsSlice();", pulseMeterSlicesRegistration);
    }

    [Fact]
    public void PulseMeterComposition_UsesAppLevelRegistrationFilesForUsagePlatformAndWindow()
    {
        var coreRegistration = ReadCompositionFile("UsageCollectionRegistration.cs");
        var infrastructureRegistration = ReadCompositionFile("PlatformRegistration.cs");
        var shellRegistration = ReadCompositionFile("PulseMeterWindowRegistration.cs");

        Assert.Contains("internal static class UsageCollectionRegistration", coreRegistration);
        Assert.Contains("internal static IServiceCollection AddUsageCollection", coreRegistration);
        Assert.Contains("AddSingleton<IUsageService, CodexUsageService>", coreRegistration);
        Assert.Contains("AddSingleton<SharedRolloutAnalyticsSource>", coreRegistration);
        Assert.Contains("new ProjectUsageService(provider.GetRequiredService<SharedRolloutAnalyticsSource>())", coreRegistration);
        Assert.Contains("new UsageAttributionService(provider.GetRequiredService<SharedRolloutAnalyticsSource>())", coreRegistration);
        Assert.Contains("AddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>", coreRegistration);
        Assert.Contains("internal static class PlatformRegistration", infrastructureRegistration);
        Assert.Contains("internal static IServiceCollection AddPulseMeterPlatform", infrastructureRegistration);
        Assert.Contains("AddSingleton<IPulseMeterAppSettingsStore, PulseMeterAppSettingsStore>", infrastructureRegistration);
        Assert.Contains("AddSingleton<IPulseMeterWindowLifecycleCoordinator, PulseMeterWindowLifecycleCoordinator>", infrastructureRegistration);
        Assert.Contains("internal static class PulseMeterWindowRegistration", shellRegistration);
        Assert.Contains("internal static IServiceCollection AddPulseMeter", shellRegistration);
        Assert.Contains("AddSingleton(sp =>", shellRegistration);
        Assert.Contains("new PulseMeterWindowViewModel(", shellRegistration);
        Assert.Contains("navigationRail.ApplyPanelState(appSettings?.IsNavigationPanelExpanded ?? true);", shellRegistration);
        Assert.Contains("AddSingleton<IPulseMeterWindow>", shellRegistration);
        Assert.Contains("AddSingleton<ITrayIconService, TrayIconService>", shellRegistration);
    }

    [Fact]
    public void BurnAnalysis_RemainsBetweenProjectAndDailyUsageWithoutUsageExplorer()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var navigationXaml = ReadXamlFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml");
        var usageAttributionSection = ReadXamlFile("src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml");
        var usageAttributionSectionCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml.cs"));
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));

        Assert.Contains("<usageAttribution:UsageAttributionSection DataContext=\"{Binding UsageAttribution}\"", windowXaml);
        Assert.DoesNotContain("UsageExplorerSection", windowXaml);
        Assert.True(
            windowXaml.IndexOf("<projectUsage:ProjectUsageSection", StringComparison.Ordinal) <
            windowXaml.IndexOf("<usageAttribution:UsageAttributionSection", StringComparison.Ordinal)
            && windowXaml.IndexOf("<usageAttribution:UsageAttributionSection", StringComparison.Ordinal) <
            windowXaml.IndexOf("<dailyUsage:DailyUsageSection", StringComparison.Ordinal));
        Assert.DoesNotContain("Usage explorer", navigationXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NavigationSection.UsageExplorer", navigationXaml);
        Assert.Contains("IsUsageAttributionVisible", navigationXaml);
        Assert.Contains("Text=\"Burn analysis\"", navigationXaml);
        Assert.Contains("ToolTip=\"Go to burn analysis\"", navigationXaml);
        Assert.Contains("public UsageAttributionSectionViewModel UsageAttribution { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public bool ShouldShowUsageAttribution", pulseMeterWindowViewModel);
        Assert.Contains("Visibility=\"{Binding DataContext.ShouldShowUsageAttribution, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", usageAttributionSection);
        Assert.Contains("Text=\"{Binding EmptyStateText}\"", usageAttributionSection);
        Assert.Contains("Binding=\"{Binding HasAttribution}\" Value=\"False\"", usageAttributionSection);
        Assert.Contains("Text=\"BURN ANALYSIS\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"USAGE EXPLORER\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"SPIKE INVESTIGATOR\"", usageAttributionSection);
        Assert.Contains("Text=\"Top projects by token burn\"", usageAttributionSection);
        Assert.Contains("Text=\"Project\"", usageAttributionSection);
        Assert.Contains("Text=\"Share\"", usageAttributionSection);
        Assert.Contains("x:Name=\"ShareInfoIcon\"", usageAttributionSection);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", usageAttributionSection);
        Assert.Contains("Text=\"&#xE946;\"", usageAttributionSection);
        Assert.Contains("ToolTip=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection);
        Assert.Contains("Text=\"Tokens\"", usageAttributionSection);
        Assert.DoesNotContain("Largest burn moments", usageAttributionSection);
        Assert.DoesNotContain("BurnEventRows", usageAttributionSection);
        Assert.DoesNotContain("BurnAnalysisTablesGrid_SizeChanged", usageAttributionSectionCode);
        Assert.DoesNotContain("Text=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"Share of total burned tokens in last 30 days\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"{Binding SummaryText}\"", usageAttributionSection);
        Assert.Contains("ToolTip=\"{Binding TooltipText}\"", usageAttributionSection);
        var shareHeaderStart = usageAttributionSection.IndexOf("Text=\"Share\"", StringComparison.Ordinal);
        var shareInfoIconStart = usageAttributionSection.IndexOf("x:Name=\"ShareInfoIcon\"", StringComparison.Ordinal);
        Assert.True(shareHeaderStart < shareInfoIconStart);
        Assert.DoesNotContain("ToolTip=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection[shareHeaderStart..shareInfoIconStart]);
        var topProjectsStart = usageAttributionSection.IndexOf("Text=\"Top projects by token burn\"", StringComparison.Ordinal);
        var topProjectsSection = usageAttributionSection[topProjectsStart..];
        Assert.Contains("ItemsSource=\"{Binding ProjectRows}\"", topProjectsSection);
        Assert.Contains("Text=\"{Binding ActivityText}\"", topProjectsSection);
        Assert.DoesNotContain("ItemsSource=\"{Binding SessionRows}\"", topProjectsSection);
        Assert.DoesNotContain("Text=\"{Binding AgeText}\"", topProjectsSection);
        Assert.DoesNotContain("<ProgressBar", topProjectsSection);
        Assert.DoesNotContain("SharePercentValue", topProjectsSection);
        var topProjectsShareHeaderStart = topProjectsSection.IndexOf("Text=\"Share\"", StringComparison.Ordinal);
        var topProjectsTokensHeaderStart = topProjectsSection.IndexOf("Text=\"Tokens\"", topProjectsShareHeaderStart, StringComparison.Ordinal);
        var topProjectsShareHeaderStackStart = topProjectsSection.LastIndexOf("<StackPanel Grid.Column=\"1\"", topProjectsShareHeaderStart, StringComparison.Ordinal);
        Assert.NotEqual(-1, topProjectsShareHeaderStart);
        Assert.NotEqual(-1, topProjectsTokensHeaderStart);
        Assert.NotEqual(-1, topProjectsShareHeaderStackStart);
        Assert.Contains("HorizontalAlignment=\"Center\"", topProjectsSection[topProjectsShareHeaderStackStart..topProjectsTokensHeaderStart]);
        Assert.DoesNotContain("Prompt", usageAttributionSection);
        Assert.DoesNotContain("MessageBody", usageAttributionSection);
        Assert.DoesNotContain("Transcript", usageAttributionSection);
    }

    [Fact]
    public void RateLimitModules_UseChildSectionViewModels()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var rateLimitsRegistration = ReadSliceRegistration("RateLimits", "RateLimitsRegistration.cs");
        var rateLimitsDailyRegistration = ReadSliceRegistration("RateLimitsDaily", "RateLimitsDailyRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSectionViewModel.cs")));
        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "RateLimitsDaily", "RateLimitsDailySectionViewModel.cs")));
        Assert.Contains("<rateLimits:RateLimitsSection DataContext=\"{Binding RateLimits}\"", windowXaml);
        Assert.Contains("<rateLimitsDaily:RateLimitsDailySection DataContext=\"{Binding RateLimitsDaily}\"", windowXaml);
        Assert.Contains("public RateLimitsSectionViewModel RateLimits { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public RateLimitsDailySectionViewModel RateLimitsDaily { get; }", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<RateLimitsSectionViewModel>", rateLimitsRegistration);
        Assert.Contains("AddSingleton<RateLimitsDailySectionViewModel>", rateLimitsDailyRegistration);
    }

    [Fact]
    public void AccountAndDailyUsageModules_UseChildSectionViewModels()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var accountUsageRegistration = ReadSliceRegistration("AccountUsage", "AccountUsageRegistration.cs");
        var dailyUsageRegistration = ReadSliceRegistration("DailyUsage", "DailyUsageRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var dailyUsageCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml.cs"));
        var accountUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSection.xaml");
        var dailyUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml");

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSectionViewModel.cs")));
        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSectionViewModel.cs")));
        Assert.Contains("<accountUsage:AccountUsageSection DataContext=\"{Binding AccountUsage}\"", windowXaml);
        Assert.Contains("<dailyUsage:DailyUsageSection DataContext=\"{Binding DailyUsage}\"", windowXaml);
        Assert.Contains("public AccountUsageSectionViewModel AccountUsage { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public DailyUsageSectionViewModel DailyUsage { get; }", pulseMeterWindowViewModel);
        Assert.DoesNotContain("IAccountUsagePresenter?", pulseMeterWindowViewModel);
        Assert.DoesNotContain("IDailyUsagePresenter?", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<AccountUsageSectionViewModel>", accountUsageRegistration);
        Assert.Contains("AddSingleton<DailyUsageSectionViewModel>", dailyUsageRegistration);
        Assert.Contains("Visibility=\"{Binding DataContext.IsAccountUsageVisible, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", accountUsageSection);
        Assert.Contains("Path=\"DataContext.AutoSyncSeconds\"", accountUsageSection);
        Assert.Contains("UpdateSourceTrigger=\"LostFocus\"", accountUsageSection);
        Assert.DoesNotContain("UpdateSourceTrigger=\"PropertyChanged\"", accountUsageSection);
        Assert.Contains("AutoSyncSecondsValidationRule", accountUsageSection);
        Assert.Contains("PreviewKeyDown=\"AutoSyncSecondsTextBox_OnPreviewKeyDown\"", accountUsageSection);
        Assert.Contains("Press Enter to apply; press Escape to cancel.", accountUsageSection);
        Assert.Contains("Visibility=\"{Binding DataContext.IsDailyUsageVisible, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", dailyUsageSection);
        Assert.Contains("Visibility=\"{Binding IsDailyUsageExpanded, Converter={StaticResource BooleanToVisibilityConverter}}\"", dailyUsageSection);
        Assert.Contains("public void ToggleDailyUsageExpanded()", pulseMeterWindowViewModel);
        Assert.Contains("DailyUsage.ToggleDailyUsageExpanded()", pulseMeterWindowViewModel);
        Assert.Contains("DailyUsageSectionViewModel", dailyUsageCode);
        Assert.DoesNotContain("PulseMeterWindowViewModel", dailyUsageCode);
        Assert.DoesNotContain("Window.GetWindow", dailyUsageCode);
    }

    [Fact]
    public void ResetCreditsAndProjectUsageModules_UseChildSectionViewModels()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var resetCreditsRegistration = ReadSliceRegistration("ResetCredits", "ResetCreditsRegistration.cs");
        var projectUsageRegistration = ReadSliceRegistration("ProjectUsage", "ProjectUsageRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var resetCreditsSection = ReadXamlFile("src", "PulseMeter", "Slices", "ResetCredits", "ResetCreditsSection.xaml");
        var projectUsageSection = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml");

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "ResetCredits", "ResetCreditsSectionViewModel.cs")));
        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSectionViewModel.cs")));
        Assert.Contains("<resetCredits:ResetCreditsSection DataContext=\"{Binding ResetCreditsSection}\"", windowXaml);
        Assert.Contains("<projectUsage:ProjectUsageSection DataContext=\"{Binding ProjectUsage}\"", windowXaml);
        Assert.Contains("public ResetCreditsSectionViewModel ResetCreditsSection { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public ProjectUsageSectionViewModel ProjectUsage { get; }", pulseMeterWindowViewModel);
        Assert.DoesNotContain("IResetCreditsPresenter?", pulseMeterWindowViewModel);
        Assert.DoesNotContain("IProjectUsagePresenter?", pulseMeterWindowViewModel);
        Assert.DoesNotContain("_resetCreditsPresenter", pulseMeterWindowViewModel);
        Assert.DoesNotContain("_projectUsagePresenter", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<ResetCreditsSectionViewModel>", resetCreditsRegistration);
        Assert.Contains("AddSingleton<ProjectUsageSectionViewModel>", projectUsageRegistration);
        Assert.Contains("Visibility=\"{Binding DataContext.IsResetCreditsVisible, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", resetCreditsSection);
        Assert.Contains("ItemsSource=\"{Binding ResetCredits}\"", resetCreditsSection);
        Assert.Contains("Visibility=\"{Binding DataContext.ShouldShowProjectUsage, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", projectUsageSection);
        Assert.Contains("ItemsSource=\"{Binding ProjectUsageRows}\"", projectUsageSection);
    }

    [Fact]
    public void NeedsAttentionModule_UsesChildSectionViewModelAndAlwaysShowsStatus()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var needsAttentionRegistration = ReadSliceRegistration("NeedsAttention", "NeedsAttentionRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var needsAttentionSection = ReadXamlFile("src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSection.xaml");
        var needsAttentionSectionCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSection.xaml.cs"));

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSectionViewModel.cs")));
        Assert.Contains("<needsAttention:NeedsAttentionSection DataContext=\"{Binding NeedsAttention}\"", windowXaml);
        Assert.Contains("public NeedsAttentionSectionViewModel NeedsAttention { get; }", pulseMeterWindowViewModel);
        Assert.DoesNotContain("INeedsAttentionPresenter?", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<NeedsAttentionSectionViewModel>", needsAttentionRegistration);
        Assert.DoesNotContain("Visibility=\"{Binding HasNeedsAttention, Converter={StaticResource BooleanToVisibilityConverter}}\"", needsAttentionSection);
        Assert.Contains("Text=\"All clear - no items need attention right now.\"", needsAttentionSection);
        Assert.Contains("DataTrigger Binding=\"{Binding HasNeedsAttention}\" Value=\"False\"", needsAttentionSection);
        Assert.Contains("ItemsSource=\"{Binding VisibleNeedsAttentionItems}\"", needsAttentionSection);
        Assert.Contains("Content=\"{Binding ToggleAttentionItemsText}\"", needsAttentionSection);
        Assert.Contains("Command=\"{Binding ToggleAttentionItemsCommand}\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.Name=\"{Binding ToggleAttentionItemsAccessibleLabel}\"", needsAttentionSection);
        Assert.Contains("Visibility=\"{Binding HasHiddenAttentionItems, Converter={StaticResource BooleanToVisibilityConverter}}\"", needsAttentionSection);
        Assert.Contains("x:Name=\"ToggleAttentionItemsButton\"", needsAttentionSection);
        Assert.Contains("x:Name=\"CollapseAttentionItemsButton\"", needsAttentionSection);
        Assert.Contains("Content=\"Show top 3\"", needsAttentionSection);
        Assert.Contains("Visibility=\"{Binding IsShowingAll, Converter={StaticResource BooleanToVisibilityConverter}}\"", needsAttentionSection);
        Assert.Contains("Click=\"CollapseAttentionItemsButton_Click\"", needsAttentionSection);
        Assert.Contains("IsVisibleChanged=\"CollapseAttentionItemsButton_IsVisibleChanged\"", needsAttentionSection);
        Assert.Contains("_restoreAttentionToggleFocusAfterCollapse = true", needsAttentionSectionCode);
        Assert.Contains("DispatcherPriority.ApplicationIdle", needsAttentionSectionCode);
        Assert.Contains("ToggleAttentionItemsButton.Focus()", needsAttentionSectionCode);
        Assert.Contains("Keyboard.Focus(ToggleAttentionItemsButton)", needsAttentionSectionCode);
        Assert.Contains("Command=\"{Binding DataContext.CopyDiagnosticCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.Name=\"{Binding CopyAccessibleLabel}\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", needsAttentionSection);
        Assert.Contains("TargetUpdated=\"CopyFeedbackText_OnTargetUpdated\"", needsAttentionSection);
        Assert.Contains("Command=\"{Binding DataContext.DismissSignalCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.Name=\"Dismiss idle alert\"", needsAttentionSection);
        Assert.Contains("Visibility=\"{Binding HasPendingDismissal, Converter={StaticResource BooleanToVisibilityConverter}}\"", needsAttentionSection);
        Assert.Contains("Content=\"Undo\"", needsAttentionSection);
        Assert.Contains("Command=\"{Binding UndoDismissCommand}\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.Name=\"Undo dismissed idle alert\"", needsAttentionSection);
        Assert.Contains("Content=\"Review\"", needsAttentionSection);
        Assert.Contains("Click=\"ReviewButton_Click\"", needsAttentionSection);
        Assert.Contains("AutomationProperties.Name=\"{Binding ReviewAccessibleLabel}\"", needsAttentionSection);
        Assert.Contains("x:Name=\"ActionPanel\"", needsAttentionSection);
        Assert.Contains("Grid.Column=\"2\"", needsAttentionSection);
        Assert.Contains("Grid.ColumnSpan=\"3\"", needsAttentionSection);
        Assert.Contains("Text=\"NEEDS ATTENTION\"", needsAttentionSection);
        Assert.True(
            windowXaml.IndexOf("<needsAttention:NeedsAttentionSection", StringComparison.Ordinal) <
            windowXaml.IndexOf("<rateLimits:RateLimitsSection", StringComparison.Ordinal));
    }

    [Fact]
    public void BudgetAlertSignals_AreInternalNeedsAttentionSignalsWithoutSeparatePanel()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var navigationXaml = ReadXamlFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml");
        var budgetAlertsRegistration = ReadSliceRegistration("BudgetAlerts", "BudgetAlertsRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var root = TestWorkspace.FindRoot();

        Assert.False(File.Exists(Path.Combine(root, "src", "PulseMeter", "Slices", "BudgetAlerts", "UI", "BudgetAlertsSection.xaml")));
        Assert.False(File.Exists(Path.Combine(root, "src", "PulseMeter", "Slices", "BudgetAlerts", "UI", "BudgetAlertsSectionViewModel.cs")));
        Assert.DoesNotContain("<budgetAlerts:BudgetAlertsSection", windowXaml);
        Assert.DoesNotContain("BudgetAlertsSectionViewModel", pulseMeterWindowViewModel);
        Assert.DoesNotContain("public BudgetAlerts", pulseMeterWindowViewModel);
        Assert.DoesNotContain("IsBudgetAlertsVisible", navigationXaml);
        Assert.DoesNotContain("Text=\"Budget alerts\"", navigationXaml);
        Assert.Contains("AddSingleton<IBudgetAlertTracker, BudgetAlertTracker>", budgetAlertsRegistration);
    }

    [Fact]
    public void UsageSignalsSlice_OwnsTemporalSignalDetection()
    {
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var usageSignalsTracker = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageSignals", "Business", "UsageSignalsTracker.cs"));
        var needsAttentionPresenter = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "Business", "NeedsAttentionPresenter.cs"));
        var platformRegistration = ReadCompositionFile("PlatformRegistration.cs");

        Assert.Contains("IUsageSignalsTracker", pulseMeterWindowViewModel);
        Assert.Contains("_usageSignalsTracker.Observe(snapshot, nowUtc)", pulseMeterWindowViewModel);
        Assert.DoesNotContain("MinimumIdleDrainPercentDelta", pulseMeterWindowViewModel);
        Assert.DoesNotContain("MinimumRunwayObservation", pulseMeterWindowViewModel);
        Assert.Contains("MinimumIdleDrainPercentDelta", usageSignalsTracker);
        Assert.Contains("MinimumRunwayObservation", usageSignalsTracker);
        Assert.Contains("AddWeeklyLimitSignal", usageSignalsTracker);
        Assert.Contains("AddProjectUsageSignal", usageSignalsTracker);
        Assert.DoesNotContain("UsageSnapshot", needsAttentionPresenter);
        Assert.DoesNotContain("AddWeeklyLimitSignal", needsAttentionPresenter);
        Assert.DoesNotContain("AddProjectUsageSignal", needsAttentionPresenter);
        Assert.Contains("AddSingleton<IUserIdleTimeProvider, UserIdleTimeProvider>", platformRegistration);
    }

    [Fact]
    public void RateLimitsSection_UsesTwoFullCircularQuotaColumnsWithPerRowPacing()
    {
        var rateLimitsSection = ReadXamlFile("src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSection.xaml");

        Assert.Contains("x:Name=\"RateLimitsPanel\"", rateLimitsSection);
        Assert.Contains("Style=\"{DynamicResource LightCardStyle}\"", rateLimitsSection);
        Assert.Contains("Text=\"Track\"", rateLimitsSection);
        Assert.Contains("SelectedItem=\"{Binding SelectedLimitOption, Mode=TwoWay}\"", rateLimitsSection);
        Assert.Contains("x:Name=\"QuotaColumnDivider\"", rateLimitsSection);
        Assert.Contains("Visibility=\"{Binding HasMultipleSelectedQuotaRows", rateLimitsSection);
        Assert.Contains("DataContext.SelectedQuotaColumnCount", rateLimitsSection);
        Assert.Contains("MaxWidth=\"520\"", rateLimitsSection);
        Assert.Contains("ItemsSource=\"{Binding SelectedQuotaRows}\"", rateLimitsSection);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding RowTitleText}\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding StatusText}\"", rateLimitsSection);
        Assert.Contains("Data=\"{Binding RingArcData}\"", rateLimitsSection);
        Assert.Contains("<Path Stroke=\"#E5E7EB\"", rateLimitsSection);
        Assert.Contains("<EllipseGeometry Center=\"56,56\"", rateLimitsSection);
        Assert.Contains("RadiusX=\"43\"", rateLimitsSection);
        Assert.Contains("RadiusY=\"43\"", rateLimitsSection);
        Assert.Contains("StrokeThickness=\"3.5\"", rateLimitsSection);
        Assert.Equal(3, CountOccurrences(rateLimitsSection, "Stretch=\"None\""));
        Assert.Contains("Canvas.Left=\"{Binding RingKnobHaloLeft}\"", rateLimitsSection);
        Assert.Contains("Canvas.Left=\"{Binding RingKnobLeft}\"", rateLimitsSection);
        Assert.Contains("Opacity=\"0.24\"", rateLimitsSection);
        Assert.Contains("Data=\"{Binding CriticalRingArcData}\"", rateLimitsSection);
        Assert.Contains("Visibility=\"{Binding HasCriticalRingArc", rateLimitsSection);
        Assert.Contains("Text=\"{Binding RingPercentText}\"", rateLimitsSection);
        Assert.Contains("Text=\"left\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding ResetTimeText}\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding ResetCountdownText}\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding PaceText}\"", rateLimitsSection);
        Assert.DoesNotContain("RunwayHintPanel", rateLimitsSection);
        Assert.DoesNotContain("WrapPanel", rateLimitsSection);
        Assert.DoesNotContain("ProgressBar", rateLimitsSection);
    }

    [Fact]
    public void DataBar_CodeBehindRaisesWindowActionRequests()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "DataBar", "DataBar.xaml.cs"));

        Assert.Contains("public event RoutedEventHandler? ToggleExpandedRequested", code);
        Assert.Contains("public event RoutedEventHandler? HideRequested", code);
        Assert.Contains("ToggleExpandedRequested?.Invoke(this, e)", code);
        Assert.Contains("HideRequested?.Invoke(this, e)", code);
        Assert.DoesNotContain("PulseMeterWindowViewModel", code);
        Assert.DoesNotContain("Window.GetWindow", code);
    }

    [Fact]
    public void ExpandedPulseMeter_ShowsResetCreditsWhereSafeToStartUsedToBe()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.DoesNotContain("Safe to start", xaml);
        Assert.DoesNotContain("SafeToStartText", xaml);
        Assert.Contains("Text=\"Reset credits\"", xaml);
        Assert.Contains("IsResetCreditsVisible", xaml);
        Assert.Contains("x:Name=\"ResetCreditsPanel\"", xaml);
        Assert.Contains("Text=\"RESET CREDITS\"", xaml);
        Assert.Contains("ResetCreditsAvailableText", xaml);
        Assert.True(
            xaml.IndexOf("Text=\"RESET CREDITS\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"AccountUsageDashboard\"", StringComparison.Ordinal));
    }

    [Fact]
    public void CompactHeader_AlignsQuotaSummaryLeftAndUsesCompactWidth()
    {
        var xaml = ReadPulseMeterMarkup();
        var controlsIndex = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);
        var quotaScaleBoxStart = xaml.IndexOf("x:Name=\"CompactQuotaScaleBox\"", StringComparison.Ordinal);
        var quotaBlockStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"CompactHeaderGrid\"", xaml);
        Assert.Contains("Width=\"382\"", xaml);
        Assert.NotEqual(-1, controlsIndex);
        Assert.NotEqual(-1, quotaScaleBoxStart);
        Assert.NotEqual(-1, quotaBlockStart);

        var controlsBlock = xaml[controlsIndex..Math.Min(xaml.Length, controlsIndex + 300)];
        Assert.Contains("Grid.Row=\"0\"", controlsBlock);

        var quotaBlock = xaml[quotaBlockStart..controlsIndex];
        Assert.Contains("ItemsSource=\"{Binding CompactQuotaRows}\"", quotaBlock);
        Assert.Contains("<Ellipse", quotaBlock);
        Assert.Contains("Fill=\"{Binding RingBrush}\"", quotaBlock);
        Assert.Contains("Text=\"{Binding CompactRemainingPercentText}\"", quotaBlock);
        Assert.Contains("x:Name=\"CompactQuotaSeparatorLine\"", quotaBlock);
        Assert.Contains("Margin=\"12,0\"", quotaBlock);
        Assert.Contains("Visibility=\"{Binding ShowCompactSeparator", quotaBlock);
        Assert.DoesNotContain("AlternationIndex", quotaBlock);
        Assert.Contains("HorizontalAlignment=\"Left\"", quotaBlock);
        Assert.Contains("Text=\"{Binding ResetDisplayText}\"", quotaBlock);

        var quotaScaleBox = xaml[quotaScaleBoxStart..quotaBlockStart];
        Assert.Contains("Grid.Row=\"0\"", quotaScaleBox);
        Assert.Contains("Grid.Column=\"0\"", quotaScaleBox);
    }

    [Fact]
    public void CompactHeader_ScalesQuotaSummaryBeforeClippingItsText()
    {
        var xaml = ReadPulseMeterMarkup();
        var quotaScaleBoxStart = xaml.IndexOf("x:Name=\"CompactQuotaScaleBox\"", StringComparison.Ordinal);
        var quotaItemsStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, quotaScaleBoxStart);
        Assert.True(quotaScaleBoxStart < quotaItemsStart);

        var quotaScaleBox = xaml[quotaScaleBoxStart..quotaItemsStart];
        Assert.Contains("Stretch=\"Uniform\"", quotaScaleBox);
        Assert.Contains("StretchDirection=\"DownOnly\"", quotaScaleBox);
    }

    [Fact]
    public void CompactHeader_ShowsResetDetailsOnASecondLineWithSubtleSeparators()
    {
        var xaml = ReadPulseMeterMarkup();
        var compactStart = xaml.IndexOf("x:Name=\"CompactHeaderGrid\"", StringComparison.Ordinal);
        var quotaStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);

        var compactBlock = xaml[compactStart..quotaStart];
        var quotaBlock = xaml[quotaStart..controlsStart];

        Assert.Contains("Height=\"52\"", compactBlock);
        Assert.Contains("Text=\"{Binding ResetDisplayText}\"", quotaBlock);
        Assert.Contains("x:Name=\"CompactQuotaSeparatorLine\"", quotaBlock);
        Assert.DoesNotContain("Text=\"|\"", quotaBlock);
    }

    [Fact]
    public void CompactHeader_UsesInlineResetDetailsOnlyForWeeklyOnlyQuotaState()
    {
        var xaml = ReadPulseMeterMarkup();
        var quotaStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);
        var quotaBlock = xaml[quotaStart..controlsStart];

        Assert.Contains("x:Name=\"CompactQuotaDetails\"", quotaBlock);
        Assert.Contains("x:Name=\"CompactQuotaResetPanel\"", quotaBlock);
        Assert.Contains("VerticalAlignment=\"Center\"", quotaBlock);
        Assert.Contains("DataContext.IsWeeklyOnlyCompactLayout", quotaBlock);
        Assert.Contains("<Setter Property=\"Orientation\" Value=\"Vertical\" />", quotaBlock);
        Assert.Contains("<Setter Property=\"Orientation\" Value=\"Horizontal\" />", quotaBlock);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"10,0,0,0\" />", quotaBlock);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\" />", quotaBlock);
    }

    [Fact]
    public void CompactHeader_UsesSubtleQuotaUnderlinesMatchedToTheirStatusDots()
    {
        var xaml = ReadPulseMeterMarkup();
        var quotaStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);
        var quotaBlock = xaml[quotaStart..controlsStart];

        Assert.Contains("x:Name=\"CompactQuotaAccentLine\"", quotaBlock);
        Assert.Contains("Height=\"1\"", quotaBlock);
        Assert.Contains("Background=\"{Binding RingBrush}\"", quotaBlock);
        Assert.Contains("Opacity=\"0.45\"", quotaBlock);
    }

    [Fact]
    public void CompactHeader_ReservesAnIndependentColumnForActionButtons()
    {
        var xaml = ReadPulseMeterMarkup();
        var gridStart = xaml.IndexOf("x:Name=\"CompactHeaderGrid\"", StringComparison.Ordinal);
        var quotaStart = xaml.IndexOf("x:Name=\"CompactQuotaScaleBox\"", StringComparison.Ordinal);
        var statusStart = xaml.IndexOf("x:Name=\"CompactStatusIndicator\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, statusStart);

        var columnsBlock = xaml[gridStart..quotaStart];
        var statusBlock = xaml[statusStart..controlsStart];
        var controlsBlock = xaml[controlsStart..];

        Assert.Contains("<ColumnDefinition Width=\"93\" />", columnsBlock);
        Assert.Contains("Grid.Column=\"1\"", statusBlock);
        Assert.Contains("Grid.Column=\"2\"", controlsBlock);
    }

    [Fact]
    public void CompactHeader_UsesCompactHeightAndLineSeparatorBetweenQuotaRows()
    {
        var xaml = ReadPulseMeterMarkup();
        var surfaceStart = xaml.IndexOf("x:Name=\"WindowSurface\"", StringComparison.Ordinal);
        var compactStart = xaml.IndexOf("x:Name=\"CompactHeaderGrid\"", StringComparison.Ordinal);
        var quotaStart = xaml.IndexOf("x:Name=\"CompactQuotaSummaryItemsControl\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("x:Name=\"CompactHeaderControls\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, surfaceStart);
        Assert.NotEqual(-1, compactStart);
        Assert.NotEqual(-1, quotaStart);
        Assert.True(controlsStart > quotaStart);

        var surfaceBlock = xaml[surfaceStart..compactStart];
        var compactBlock = xaml[compactStart..Math.Min(xaml.Length, compactStart + 500)];
        var quotaBlock = xaml[quotaStart..controlsStart];

        Assert.Contains("<Setter Property=\"Padding\" Value=\"14,6,12,6\" />", surfaceBlock);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"#008CBA\" />", surfaceBlock);
        Assert.Contains("Height=\"52\"", compactBlock);
        Assert.Contains("Width=\"382\"", compactBlock);
        Assert.Contains("x:Name=\"CompactQuotaSeparatorLine\"", quotaBlock);
        Assert.DoesNotContain("Text=\"|\"", quotaBlock);
    }

    [Fact]
    public void ExpandedBucketRows_PlaceResetTextAboveTheProgressBar()
    {
        var xaml = ReadPulseMeterMarkup();
        var quotaRowsStart = xaml.IndexOf("ItemsSource=\"{Binding SelectedQuotaRows}\"", StringComparison.Ordinal);
        var quotaRowsXaml = xaml[quotaRowsStart..];

        Assert.Contains("SelectedQuotaRows", xaml);
        Assert.True(
            quotaRowsXaml.IndexOf("ResetDisplayText", StringComparison.Ordinal) <
            quotaRowsXaml.IndexOf("<ProgressBar", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpandedSidebar_HasCollapseToggleAndCollapsedWidthBinding()
    {
        var xaml = ReadPulseMeterMarkup();
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml.cs"));

        Assert.Contains("NavigationPanelToggleButtonStyle", xaml);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
        Assert.Contains("Text=\"{Binding NavigationPanelToggleGlyph}\"", xaml);
        Assert.Contains("Width=\"{Binding NavigationPanelWidth}\"", xaml);
        Assert.Contains("IsNavigationPanelExpanded", xaml);
        Assert.Contains("NavigationPanelToggleGlyph", xaml);
        Assert.Contains("NavigationPanelToggleTooltip", xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding NavigationPanelToggleTooltip}\"", xaml);
        Assert.Contains("NavigationPanelToggleButton_Click", xaml);
        Assert.Contains("ToggleNavigationPanel", code);
    }

    [Fact]
    public void NavigationRail_CodeBehindTogglesNavigationPanelOnly()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml.cs"));

        Assert.Contains("NavigationPanelToggleButton_Click", code);
        Assert.Contains("ToggleNavigationPanel", code);
        Assert.DoesNotContain("Window.GetWindow", code);
        Assert.DoesNotContain("ToggleExpanded", code);
    }

    [Fact]
    public void RateLimitsDaily_SectionIsInSidebarAndExpandedUsageArea()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("Text=\"Weekly pace\"", xaml);
        Assert.Contains("IsRateLimitsDailyVisible", xaml);
        Assert.Contains("x:Name=\"RateLimitsDailyPanel\"", xaml);
        Assert.Contains("Text=\"WEEKLY PACE\"", xaml);
        Assert.Contains("RateLimitsDailySummaryText", xaml);
        Assert.Contains("RateLimitsDailyWarningText", xaml);
        Assert.Contains("HasRateLimitsDailyWarning", xaml);
        Assert.Contains("DailyRateLimitRows", xaml);
        Assert.Contains("RingArcData", xaml);
        Assert.Contains("RingBrush", xaml);
        Assert.Contains("RemainingPercentText", xaml);
        Assert.Contains("LabelBrush", xaml);
        Assert.Contains("Stroke=\"#E5E7EB\"", xaml);
        Assert.Contains("Stroke=\"{Binding RingBrush}\"", xaml);

        var rateLimitsSidebarStart = xaml.IndexOf("Text=\"Rate limits\"", StringComparison.Ordinal);
        var rateLimitsDailySidebarStart = xaml.IndexOf("Text=\"Weekly pace\"", StringComparison.Ordinal);
        var resetCreditsSidebarStart = xaml.IndexOf("Text=\"Reset credits\"", StringComparison.Ordinal);
        Assert.True(rateLimitsSidebarStart < rateLimitsDailySidebarStart);
        Assert.True(rateLimitsDailySidebarStart < resetCreditsSidebarStart);

        var rateLimitsPanelStart = xaml.IndexOf("x:Name=\"RateLimitsPanel\"", StringComparison.Ordinal);
        var dailyPanelStart = xaml.IndexOf("x:Name=\"RateLimitsDailyPanel\"", StringComparison.Ordinal);
        var resetCreditsPanelStart = xaml.IndexOf("x:Name=\"ResetCreditsPanel\"", StringComparison.Ordinal);
        Assert.True(rateLimitsPanelStart < dailyPanelStart);
        Assert.True(dailyPanelStart < resetCreditsPanelStart);

        var dailyPanelEnd = xaml.IndexOf("x:Name=\"ResetCreditsPanel\"", dailyPanelStart, StringComparison.Ordinal);
        var dailyPanelXaml = xaml[dailyPanelStart..dailyPanelEnd];
        Assert.Contains("Foreground=\"{Binding LabelBrush}\"", dailyPanelXaml);
        Assert.Contains("Stroke=\"#E5E7EB\"", dailyPanelXaml);
        Assert.Contains("Stroke=\"{Binding RingBrush}\"", dailyPanelXaml);
        Assert.DoesNotContain("Stroke=\"#1F73FF\"", dailyPanelXaml);
        Assert.DoesNotContain("Stroke=\"#EF4444\"", dailyPanelXaml);
        Assert.DoesNotContain("#22C55E", dailyPanelXaml);
        Assert.DoesNotContain("#F59E0B", dailyPanelXaml);
    }

    [Fact]
    public void CodingRunway_ReplacesTheStandaloneRunwayForecastSection()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var navigationXaml = ReadXamlFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml");
        var windowCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));

        Assert.Contains("<usageTrend:UsageTrendSection", windowXaml);
        Assert.Contains("x:Name=\"UsageTrendSection\"", windowXaml);
        Assert.Contains("IsRunwayForecastVisible", windowXaml);
        Assert.DoesNotContain("<runwayForecast:RunwayForecastSection", windowXaml);
        Assert.DoesNotContain("x:Name=\"RunwayForecastSection\"", windowXaml);

        Assert.Contains("Text=\"Coding runway\"", navigationXaml);
        Assert.Contains("Content=\"Coding runway\"", navigationXaml);
        Assert.Contains("ToolTip=\"Go to coding runway\"", navigationXaml);
        Assert.DoesNotContain("Text=\"Runway forecast\"", navigationXaml);
        Assert.Contains("NavigationSection.RunwayForecast => UsageTrendSection", windowCode);

        var weeklyScrollSection = windowCode.IndexOf(
            "(NavigationSection.WeeklyPace, (FrameworkElement)WeeklyPaceSection)",
            StringComparison.Ordinal);
        var runwayScrollSection = windowCode.IndexOf(
            "(NavigationSection.RunwayForecast, (FrameworkElement)UsageTrendSection)",
            StringComparison.Ordinal);
        Assert.True(weeklyScrollSection < runwayScrollSection);

        var runwayNav = navigationXaml.IndexOf("Text=\"Coding runway\"", StringComparison.Ordinal);
        var weeklyNav = navigationXaml.IndexOf("Text=\"Weekly pace\"", StringComparison.Ordinal);
        var resetNav = navigationXaml.IndexOf("Text=\"Reset credits\"", StringComparison.Ordinal);
        Assert.True(weeklyNav < runwayNav);
        Assert.True(weeklyNav < resetNav);
    }

    [Fact]
    public void ExpandedHeader_StaysOutsideScrollableContent()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var headerXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml");

        Assert.Contains("<expandedHeader:ExpandedHeader", windowXaml);
        Assert.Contains("x:Name=\"ExpandedStickyHeader\"", headerXaml);
        Assert.Contains("x:Name=\"ExpandedContentScrollViewer\"", windowXaml);

        var headerIndex = windowXaml.IndexOf("<expandedHeader:ExpandedHeader", StringComparison.Ordinal);
        var scrollIndex = windowXaml.IndexOf("x:Name=\"ExpandedContentScrollViewer\"", StringComparison.Ordinal);
        Assert.True(headerIndex < scrollIndex);

        var scrollEnd = windowXaml.IndexOf("</ScrollViewer>", scrollIndex, StringComparison.Ordinal);
        Assert.NotEqual(-1, scrollEnd);
        var scrollBlock = windowXaml[scrollIndex..scrollEnd];
        Assert.DoesNotContain("CompactTitleText", scrollBlock);
        Assert.DoesNotContain("StatusBadgeText", scrollBlock);
        Assert.Contains("BorderThickness=\"0,0,0,1\"", headerXaml);
    }

    [Fact]
    public void ExpandedHeader_HasCloseButtonThatHidesPulseMeter()
    {
        var xaml = ReadXamlFile("src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml");
        var headerStart = xaml.IndexOf("x:Name=\"ExpandedStickyHeader\"", StringComparison.Ordinal);
        var headerEnd = xaml.IndexOf("</UserControl>", StringComparison.Ordinal);

        Assert.NotEqual(-1, headerStart);
        Assert.True(headerEnd > headerStart);

        var headerBlock = xaml[headerStart..headerEnd];
        Assert.Contains("Click=\"HideButton_Click\"", headerBlock);
        Assert.Contains("ToolTip=\"Hide PulseMeter\"", headerBlock);
    }

    [Fact]
    public void ProgressBars_UseWhiteReferenceColors()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("x:Key=\"LightProgressBarStyle\"", xaml);
        Assert.Contains("Value=\"#1F73FF\"", xaml);
        Assert.Contains("Value=\"#E5E7EB\"", xaml);
        Assert.DoesNotContain("Foreground=\"#1F1F1F\"", xaml);
    }

    [Fact]
    public void PulseMeter_ShowsTodayUsedTokensCopy()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("Today used tokens", xaml);
        Assert.DoesNotContain("Today since 12am", xaml);
        Assert.Contains("TodayUsageValueText", xaml);
    }

    [Fact]
    public void ExpandedPulseMeter_UsesReferenceStyleAccountUsageDashboard()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("AccountUsageDashboard", xaml);
        Assert.Contains("PulseMeter", xaml);
        Assert.Contains("RATE LIMITS", xaml);
        Assert.Contains("RESET CREDITS", xaml);
        Assert.Contains("ACCOUNT USAGE", xaml);
        Assert.Contains("DAILY USAGE", xaml);
        Assert.Contains("Auto refresh every", xaml);
        Assert.Contains("DashboardMetricCards", xaml);
        Assert.Contains("TodayMetricCard", xaml);
        Assert.Contains("LifetimeMetricCard", xaml);
        Assert.Contains("PeakMetricCard", xaml);
        Assert.Contains("StreakMetricCard", xaml);
        Assert.Contains("TodayUsageMetricValueText", xaml);
        Assert.Contains("LifetimeUsageValueText", xaml);
        Assert.Contains("PeakUsageValueText", xaml);
        Assert.Contains("StreakDaysValueText", xaml);
        Assert.Contains("Text=\"DAILY USAGE\"", xaml);
        Assert.Contains("DailyUsageRows", xaml);

        var autoRefreshStart = xaml.IndexOf("Text=\"Auto refresh every\"", StringComparison.Ordinal);
        var autoRefreshEnd = xaml.IndexOf("x:Name=\"DashboardMetricCards\"", autoRefreshStart, StringComparison.Ordinal);
        Assert.True(autoRefreshEnd > autoRefreshStart);
        var autoRefreshBlock = xaml[autoRefreshStart..autoRefreshEnd];
        Assert.Contains("Text=\"&#xE70F;\"", autoRefreshBlock);
        Assert.DoesNotContain("Text=\"&#xE70D;\"", autoRefreshBlock);

        var accountUsageStart = xaml.IndexOf("x:Name=\"AccountUsageDashboard\"", StringComparison.Ordinal);
        var rateLimitsStart = xaml.IndexOf("Text=\"RATE LIMITS\"", StringComparison.Ordinal);
        var resetCreditsStart = xaml.IndexOf("Text=\"RESET CREDITS\"", StringComparison.Ordinal);
        var dailyUsageStart = xaml.IndexOf("x:Name=\"DailyUsagePanel\"", StringComparison.Ordinal);
        Assert.NotEqual(-1, accountUsageStart);
        Assert.NotEqual(-1, rateLimitsStart);
        Assert.True(rateLimitsStart < resetCreditsStart);
        Assert.True(resetCreditsStart < accountUsageStart);
        Assert.True(accountUsageStart < dailyUsageStart);
    }

    [Fact]
    public void PulseMeter_ResizesBetweenCompactAndExpandedDashboardStates()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("Width=\"{Binding WindowWidth}\"", xaml);
        Assert.Contains("Height=\"{Binding WindowHeight}\"", xaml);
        Assert.Contains("MinWidth=\"{Binding WindowMinWidth}\"", xaml);
        Assert.Contains("MinHeight=\"{Binding WindowMinHeight}\"", xaml);
        Assert.Contains("ResizeMode=\"CanResize\"", xaml);
        Assert.DoesNotContain("ResizeMode=\"CanResizeWithGrip\"", xaml);
        Assert.DoesNotContain("MaxWidth=\"{Binding WindowWidth}\"", xaml);
        Assert.DoesNotContain("SizeToContent=\"Height\"", xaml);
    }

    [Fact]
    public void PulseMeterWindow_CodeBehindAppliesAndPersistsNativeWindowBounds()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var applySizeStart = code.IndexOf("private void ApplyViewModelSize(PulseMeterWindowViewModel viewModel, WpfSize fittedSize)", StringComparison.Ordinal);
        var applySizeBlock = code[applySizeStart..Math.Min(code.Length, applySizeStart + 900)];

        Assert.Contains("ApplyViewModelSize", code);
        Assert.Contains("Width = fittedSize.Width", code);
        Assert.Contains("Height = fittedSize.Height", code);
        Assert.Contains("RememberWindowSize", code);
        Assert.Contains("SaveWindowState", code);
        Assert.Contains("WindowStateStore", code);
        Assert.Contains("PropertyChanged", code);
        Assert.True(
            applySizeBlock.IndexOf("WindowState = WindowState.Normal", StringComparison.Ordinal) <
            applySizeBlock.IndexOf("Width = fittedSize.Width", StringComparison.Ordinal));
        Assert.Contains("CanRememberWindowPlacement()", code);
    }

    [Fact]
    public void PulseMeterWindow_ReappliesSafeBoundsForTheNearestMonitor()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var monitorPath = Path.Combine(TestWorkspace.FindRoot(), "src", "PulseMeter", "Platform", "Windows", "WindowMonitorWorkArea.cs");
        Assert.True(File.Exists(monitorPath));

        var monitorCode = File.ReadAllText(monitorPath);
        var applySizeStart = code.IndexOf("private void ApplyViewModelBounds()", StringComparison.Ordinal);
        var applySizeBlock = code[applySizeStart..Math.Min(code.Length, applySizeStart + 1_300)];
        var propertyChangedStart = code.IndexOf("private void OnViewModelPropertyChanged", StringComparison.Ordinal);
        var propertyChangedBlock = code[propertyChangedStart..Math.Min(code.Length, propertyChangedStart + 1_000)];

        Assert.Contains("WindowMonitorWorkArea.GetFor(this)", code);
        Assert.Contains("PulseMeterWindowPlacementCalculator.FitSize", code);
        Assert.Contains("var fittedSize = GetFittedWindowSize(viewModel, workArea)", applySizeBlock);
        Assert.Contains("MinWidth = Math.Min(viewModel.WindowMinWidth, fittedSize.Width)", applySizeBlock);
        Assert.Contains("MinHeight = Math.Min(viewModel.WindowMinHeight, fittedSize.Height)", applySizeBlock);
        Assert.Contains("Width = fittedSize.Width", applySizeBlock);
        Assert.Contains("Height = fittedSize.Height", applySizeBlock);
        Assert.Contains("nameof(PulseMeterWindowViewModel.IsExpanded)", propertyChangedBlock);
        Assert.Contains("or nameof(PulseMeterWindowViewModel.WindowWidth)", propertyChangedBlock);
        Assert.Contains("or nameof(PulseMeterWindowViewModel.WindowHeight)", propertyChangedBlock);
        Assert.Contains("ApplyViewModelBounds();", propertyChangedBlock);
        Assert.DoesNotContain("!viewModel.HasWindowPosition", propertyChangedBlock);
        Assert.Contains("MonitorFromWindow(handle, MonitorDefaultToNearest)", monitorCode);
        Assert.Contains("GetMonitorInfo(monitor, ref monitorInfo)", monitorCode);
        Assert.Contains("TransformFromDevice", monitorCode);
        Assert.Contains("SystemParameters.WorkArea", monitorCode);
    }

    [Fact]
    public void PulseMeterWindow_AppliesFittedSizeAndPlacementFromOneMonitorLookup()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var boundsStart = code.IndexOf("private void ApplyViewModelBounds()", StringComparison.Ordinal);

        Assert.NotEqual(-1, boundsStart);

        var boundsBlock = code[boundsStart..Math.Min(code.Length, boundsStart + 1_200)];
        Assert.Contains("var workArea = WindowMonitorWorkArea.GetFor(this)", boundsBlock);
        Assert.Contains("var fittedSize = GetFittedWindowSize(viewModel, workArea)", boundsBlock);
        Assert.Contains("ApplyViewModelSize(viewModel, fittedSize)", boundsBlock);
        Assert.Contains("ApplyWindowPosition(viewModel, fittedSize, workArea)", boundsBlock);
    }

    [Fact]
    public void PulseMeterWindow_AppliesSavedPositionBeforeResolvingStartupMonitor()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var sourceInitializedStart = code.IndexOf("private void OnSourceInitialized", StringComparison.Ordinal);
        var sourceInitializedBlock = code[sourceInitializedStart..Math.Min(code.Length, sourceInitializedStart + 700)];
        var dataContextStart = code.IndexOf("private void OnDataContextChanged", StringComparison.Ordinal);
        var dataContextBlock = code[dataContextStart..Math.Min(code.Length, dataContextStart + 800)];

        Assert.NotEqual(-1, sourceInitializedStart);
        Assert.True(
            sourceInitializedBlock.IndexOf("ApplySavedViewModelPosition();", StringComparison.Ordinal) <
            sourceInitializedBlock.IndexOf("ApplyViewModelBounds();", StringComparison.Ordinal));
        Assert.Contains("Left = left", code);
        Assert.Contains("Top = top", code);
        Assert.Contains("if (_windowSource is not null)", dataContextBlock);
    }

    [Fact]
    public void NavigationRail_UsesNavigationButtonsAndSeparateCustomizePopup()
    {
        var xaml = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml"));
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml.cs"));

        Assert.Contains("x:Key=\"NavigationSectionButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"CustomizeDashboardButton\"", xaml);
        Assert.Contains("Text=\"{Binding ApplicationVersionText}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding ApplicationVersionText}\"", xaml);
        Assert.Contains("x:Name=\"CustomizeDashboardPopup\"", xaml);
        Assert.Contains("StaysOpen=\"False\"", xaml);
        Assert.Contains("Choose visible sections", xaml);
        Assert.Contains("CommandParameter=\"{x:Static navModels:NavigationSection.RateLimits}\"", xaml);
        Assert.Contains("IsRateLimitsVisible", xaml);
        Assert.Contains("IsUsageAttributionVisible", xaml);
        Assert.Contains("SectionRequested", code);
        Assert.Contains("SectionButton_Click", code);
        Assert.Contains("Opened=\"CustomizeDashboardPopup_Opened\"", xaml);
        Assert.Contains("Closed=\"CustomizeDashboardPopup_Closed\"", xaml);
        Assert.Contains("CustomizeDashboardPopup_KeyDown", code);
        Assert.Contains("CustomizeDashboardPopup_Opened", code);
        Assert.Contains("CustomizeDashboardPopup_Closed", code);
        Assert.Contains("FocusPopupControl(RateLimitsVisibilityCheckBox)", code);
        Assert.Contains("DispatcherPriority.ContextIdle", code);
        Assert.Contains("_restoreCustomizeFocusAfterClose = true", code);
        Assert.Contains("new Action(() => CustomizeDashboardButton.Focus())", code);
        Assert.Contains("Text=\"{Binding VisibleSectionSummaryText}\"", xaml);
        Assert.Contains("Content=\"Restore all sections\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding HasHiddenSections}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Restore all dashboard sections\"", xaml);
        Assert.Equal(8, CountOccurrences(xaml, "Style=\"{StaticResource DashboardVisibilityCheckBoxStyle}\""));
        Assert.Contains("VisibilityCheckBox_Click", code);
        Assert.Contains("if (!CustomizeDashboardPopup.IsOpen)", code);
        Assert.Contains("Keyboard.Focus(control)", code);
        Assert.DoesNotContain("NavigationSectionToggleStyle", xaml);
    }

    [Fact]
    public void NavigationRail_PlacesRequestedSectionAtTheTopOfTheViewport()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var handlerStart = code.IndexOf("private void NavigateToSection", StringComparison.Ordinal);
        var handler = code[handlerStart..Math.Min(code.Length, handlerStart + 1_400)];

        Assert.Contains("ScrollToVerticalOffset", handler);
        Assert.Contains("TransformToAncestor(ExpandedContentScrollViewer)", handler);
        Assert.DoesNotContain("target.BringIntoView()", handler);
    }

    [Fact]
    public void UsageTrend_IsAnAccessibleAnalyticalSectionAfterRateLimits()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var trendXaml = ReadXamlFile("src", "PulseMeter", "Slices", "UsageTrend", "UI", "UsageTrendSection.xaml");
        var trendChart = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageTrend", "UI", "UsageTrendChart.cs"));
        var tracker = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageSignals", "Business", "UsageSignalsTracker.cs"));

        Assert.Contains("DataContext=\"{Binding UsageTrend}\"", windowXaml);
        Assert.Contains("x:Name=\"UsageTrendSection\"", windowXaml);
        Assert.Contains("SectionRequested=\"UsageTrendSection_SectionRequested\"", windowXaml);
        Assert.Contains("Text=\"Coding runway\"", trendXaml);
        Assert.Contains("Text=\"{Binding RunwayHeadline}\"", trendXaml);
        Assert.DoesNotContain("Content=\"See pacing plan\"", trendXaml);
        Assert.DoesNotContain("Text=\"{Binding RecommendationText}\"", trendXaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleSummary}\"", trendXaml);
        Assert.Contains("AutomationProperties.Name=\"Show current pace projection\"", trendXaml);
        Assert.Contains("ToolTip=\"Reset chart view\"", trendXaml);
        Assert.Contains("Text=\"&#xE72C;\"", trendXaml);
        Assert.DoesNotContain("Text=\"&#xE713;\"", trendXaml);
        Assert.DoesNotContain("<WrapPanel Margin=\"16,10,16,0\"", trendXaml);
        Assert.Contains("UsageTrendChart Height=\"380\"", trendXaml);
        Assert.Contains("\"Sustainable pace\"", trendChart);
        Assert.DoesNotContain("\"80% range\"", trendChart);
        Assert.Contains("AutomationProperties.Name=\"Show possible-limit window\"", trendXaml);
        Assert.Contains("\"Actual\"", trendChart);
        Assert.DoesNotContain("ProjectionPen, \"Forecast\"", trendChart);
        Assert.Contains("\"Estimated reach limit ·", trendChart);
        Assert.Contains("\"Limit 100%\"", trendChart);
        Assert.Contains("BuildDailyContextTicks", trendChart);
        Assert.Contains("DrawContextStrip", trendChart);
        Assert.Contains("\"Not measured\"", trendChart);
        Assert.DoesNotContain("P10", trendXaml + trendChart, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public static readonly DependencyProperty ModelProperty", trendChart);
        Assert.DoesNotContain("StaysOpen = false", trendChart);
        Assert.Contains("ShortRunwayHistory = TimeSpan.FromHours(3)", tracker);

        var rateLimits = windowXaml.IndexOf("x:Name=\"RateLimitsSection\"", StringComparison.Ordinal);
        var usageTrend = windowXaml.IndexOf("x:Name=\"UsageTrendSection\"", StringComparison.Ordinal);
        var weeklyPace = windowXaml.IndexOf("x:Name=\"WeeklyPaceSection\"", StringComparison.Ordinal);
        Assert.True(rateLimits < weeklyPace);
        Assert.True(weeklyPace < usageTrend);
    }

    [Fact]
    public void NavigationRail_ShowsAndAnnouncesTheCurrentSection()
    {
        var navigationXaml = ReadXamlFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml");

        Assert.Contains("NavigationSectionSelectedConverter", navigationXaml);
        Assert.Equal(9, CountOccurrences(navigationXaml, "Tag=\"{Binding SelectedSection, Converter={StaticResource NavigationSectionSelectedConverter}"));
        Assert.Contains("<Trigger Property=\"Tag\" Value=\"Current\">", navigationXaml);
        Assert.Contains("Background\" Value=\"#EFF6FF\"", navigationXaml);
        Assert.Contains("AutomationProperties.ItemStatus\" Value=\"Current section\"", navigationXaml);
        Assert.Contains("AutomationProperties.HelpText\" Value=\"Current section\"", navigationXaml);
    }

    [Fact]
    public void InteractiveUsageControls_HaveAccessibleNames()
    {
        var rateLimitsXaml = ReadXamlFile("src", "PulseMeter", "Slices", "RateLimits", "UI", "RateLimitsSection.xaml");
        var accountUsageXaml = ReadXamlFile("src", "PulseMeter", "Slices", "AccountUsage", "UI", "AccountUsageSection.xaml");
        var projectUsageXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "UI", "ProjectUsageSection.xaml");

        Assert.Contains("AutomationProperties.Name=\"Rate limit track\"", rateLimitsXaml);
        Assert.Contains("AutomationProperties.Name=\"Auto refresh interval in seconds\"", accountUsageXaml);
        Assert.Contains("AutomationProperties.HelpText=\"Enter 1 to 86,400 seconds. Press Enter to apply; press Escape to cancel.\"", accountUsageXaml);
        Assert.Contains("AutomationProperties.Name=\"Project usage\"", projectUsageXaml);
        Assert.Contains("AutomationProperties.HelpText=\"Select a project to review its recent usage evidence.\"", projectUsageXaml);
        Assert.DoesNotContain("Text=\"SELECTED PROJECT\"", projectUsageXaml);
        Assert.DoesNotContain("Text=\"{Binding SelectedProjectPathText}\"", projectUsageXaml);
        Assert.DoesNotContain("Text=\"{Binding SelectedProjectSummary}\"", projectUsageXaml);
    }

    [Fact]
    public void NeedsAttentionReview_WiresTypedEventAndUsesSharedNavigationPath()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var windowCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var sectionCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "UI", "NeedsAttentionSection.xaml.cs"));

        Assert.Contains("ReviewRequested=\"NeedsAttentionSection_ReviewRequested\"", windowXaml);
        Assert.Contains("NeedsAttentionReviewRequestedEventArgs", sectionCode);
        Assert.Contains("ReviewRequested?.Invoke", sectionCode);
        Assert.Contains("RevealAndSelectSection(section)", windowCode);
        Assert.Contains("GetNavigationSection(e.Target)", windowCode);
        Assert.Contains("NavigateToSection(GetNavigationSection(e.Target), restoreHiddenSection: true)", windowCode);
    }

    [Fact]
    public void NeedsAttention_DoesNotDependOnNavigationRail()
    {
        var needsAttentionPath = Path.GetDirectoryName(
            FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "Models", "NeedsAttentionItem.cs"));
        Assert.NotNull(needsAttentionPath);
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(needsAttentionPath, "*", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("NavigationRail", source);
    }

    [Fact]
    public void PulseMeterWindow_PreventsMaximizedBlankWindowState()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));

        Assert.Contains("WmSysCommand", code);
        Assert.Contains("ScMaximize", code);
        Assert.Contains("WmSize", code);
        Assert.Contains("SizeMaximized", code);
        Assert.Contains("StateChanged += Window_StateChanged", code);
        Assert.Contains("SwRestore", code);
        Assert.Contains("ShowWindow(handle, SwRestore)", code);
        Assert.Contains("RestoreMaximizedWindowToViewModelSize", code);
    }

    [Fact]
    public void PulseMeterWindow_DragsSurfaceAndUsesNativeBorderHitTesting()
    {
        var xaml = ReadPulseMeterMarkup();
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var helperCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Windows", "WindowResizeHitTester.cs"));

        Assert.Contains("x:Name=\"WindowSurface\"", xaml);
        Assert.Contains("MouseLeftButtonDownEvent", code);
        Assert.Contains("handledEventsToo: true", code);
        Assert.DoesNotContain("MouseLeftButtonUp=\"Surface_MouseLeftButtonUp\"", xaml);
        Assert.Contains("DragMove();", code);
        Assert.Contains("WmNcHitTest", code);
        Assert.Contains("WindowResizeHitTester.GetResizeHitTest", code);
        Assert.DoesNotContain("private int? GetResizeHitTest", code);
        Assert.Contains("HtTopLeft", helperCode);
        Assert.Contains("HtBottomRight", helperCode);
        Assert.Contains("ResizeMode.CanResize", code);
        Assert.DoesNotContain("ResizeMode.CanResizeWithGrip", code);
    }

    [Fact]
    public void App_WiresWindowStateStoreIntoStartupAndExit()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Bootstrap", "Startup", "App.xaml.cs"));
        var application = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Bootstrap", "Startup", "PulseMeterApplication.cs"));
        var compositionRoot = ReadCompositionFile("PulseMeterCompositionRoot.cs");
        var infrastructureRegistration = ReadCompositionFile("PlatformRegistration.cs");
        var shellRegistration = ReadCompositionFile("PulseMeterWindowRegistration.cs");
        var lifecycleCoordinator = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "Business", "PulseMeterWindowLifecycleCoordinator.cs"));

        Assert.Contains("PulseMeterWindowStateStore", infrastructureRegistration);
        Assert.Contains("PulseMeterAppSettingsStore", infrastructureRegistration);
        Assert.Contains("PulseMeterApplication", code);
        Assert.Contains("PulseMeterCompositionRoot", application);
        Assert.Contains("BuildServiceProvider", compositionRoot);
        Assert.Contains("IPulseMeterWindowLifecycleCoordinator", application);
        Assert.Contains("IPulseMeterWindowLifecycleCoordinator", lifecycleCoordinator);
        Assert.DoesNotContain("new DispatcherTimer", code);
        Assert.DoesNotContain("UpdateForegroundVisibility", code);
        Assert.Contains("AutoSyncSeconds", shellRegistration);
        Assert.Contains("windowState:", shellRegistration);
        Assert.Contains("WindowStateStore", shellRegistration);
        Assert.Contains("CaptureWindowState", lifecycleCoordinator);
    }

    [Fact]
    public void AppRuntime_UsesInterfacesForCompositionAndPlatformServices()
    {
        var appProject = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "PulseMeter.csproj"));
        var compositionRoot = ReadCompositionFile("PulseMeterCompositionRoot.cs");
        var coreRegistration = ReadCompositionFile("UsageCollectionRegistration.cs");
        var infrastructureRegistration = ReadCompositionFile("PlatformRegistration.cs");
        var shellRegistration = ReadCompositionFile("PulseMeterWindowRegistration.cs");
        var foregroundService = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Windows", "ForegroundWindowService.cs"));
        var trayContracts = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Windows", "TrayIconContracts.cs"));

        Assert.Contains("Microsoft.Extensions.DependencyInjection", appProject);
        Assert.Contains("ServiceCollection", compositionRoot);
        Assert.Contains("AddSingleton<IUsageService, CodexUsageService>", coreRegistration);
        Assert.Contains("AddSingleton<IForegroundWindowService, ForegroundWindowService>", infrastructureRegistration);
        Assert.Contains("AddSingleton<ITrayIconService, TrayIconService>", shellRegistration);
        Assert.Contains("AddSingleton<IPulseMeterTimerFactory, DispatcherPulseMeterTimerFactory>", infrastructureRegistration);
        Assert.Contains("public interface IForegroundWindowService", foregroundService);
        Assert.Contains("public interface ITrayIconService", trayContracts);
    }

    [Fact]
    public void CoreRuntimeServices_UseInterfacesAndFactoriesForCollaborators()
    {
        var usageService = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageCollection", "Business", "CodexUsageService.cs"));
        var process = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Codex", "AppServerProcess.cs"));
        var rpc = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Codex", "CodexJsonRpcClient.cs"));

        Assert.Contains("ICodexResetCreditService", usageService);
        Assert.Contains("IProjectUsageService", usageService);
        Assert.Contains("IMockUsageService", usageService);
        Assert.Contains("IAppServerProcessFactory", usageService);
        Assert.Contains("IJsonRpcClientFactory", usageService);
        Assert.DoesNotContain("new CodexResetCreditService", usageService);
        Assert.DoesNotContain("new ProjectUsageService", usageService);
        Assert.DoesNotContain("new MockCodexUsageService", usageService);
        Assert.DoesNotContain("AppServerProcess.Start", usageService);
        Assert.DoesNotContain("new CodexJsonRpcClient", usageService);
        Assert.Contains("public interface IAppServerProcess", process);
        Assert.Contains("public interface IJsonRpcClient", rpc);
    }

    [Fact]
    public void AccountUsageDashboard_KeepsReferenceComposition()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("x:Name=\"AccountUsageDashboard\"", xaml);
        Assert.Contains("x:Name=\"DashboardHeader\"", xaml);
        Assert.Contains("x:Name=\"DashboardMetricCards\"", xaml);
        Assert.Contains("x:Name=\"DailyUsagePanel\"", xaml);
        Assert.Contains("x:Name=\"DailyUsagePanelBody\"", xaml);
        Assert.Contains("DailyUsageExpandCollapseTooltip", xaml);
        Assert.Equal(2, CountOccurrences(xaml, "AutomationProperties.Name=\"{Binding DailyUsageExpandCollapseTooltip}\""));
        Assert.Contains("DailyUsageExpandCollapseButton_Click", xaml);
        Assert.Contains("Text=\"DAILY USAGE\"", xaml);
        Assert.Contains("Visibility=\"{Binding IsDailyUsageExpanded", xaml);
        Assert.Contains("MedianComparisonText", xaml);
        Assert.Contains("HasMedianComparison", xaml);
        Assert.Contains("DailyUsageMedianSummaryText", xaml);
        Assert.Contains("HasDailyUsageMedianSummary", xaml);
        Assert.Contains("TodayMedianDailyPercentText", xaml);
        Assert.Contains("TodayMedianDailyPercentValue", xaml);
        Assert.Contains("HasAccountSummaryFreshnessWarning", xaml);
        Assert.Contains("AccountSummaryFreshnessWarningText", xaml);
        Assert.DoesNotContain("TodayPeakPercentText", xaml);
        Assert.Contains("LifetimeUsageCaptionText", xaml);
        Assert.Contains("PeakUsageCaptionText", xaml);
        Assert.Contains("StreakCaptionText", xaml);
    }

    [Fact]
    public void DailyUsageRows_UseDotsInsteadOfSquareMarkers()
    {
        var xaml = ReadPulseMeterMarkup();
        var rowsStart = xaml.IndexOf("ItemsSource=\"{Binding DailyUsageRows}\"", StringComparison.Ordinal);
        var rowsEnd = xaml.IndexOf("</ItemsControl>", rowsStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, rowsStart);
        Assert.True(rowsEnd > rowsStart);

        var rowsTemplate = xaml[rowsStart..rowsEnd];

        Assert.Contains("x:Name=\"DailyUsageDayDot\"", rowsTemplate);
        Assert.Contains("<Ellipse", rowsTemplate);
        Assert.Contains("Fill=\"#1F73FF\"", rowsTemplate);
        Assert.DoesNotContain("Width=\"13\"", rowsTemplate);
        Assert.DoesNotContain("BorderBrush=\"#1F73FF\"", rowsTemplate);
        Assert.DoesNotContain("CornerRadius=\"2\"", rowsTemplate);
    }

    [Fact]
    public void DailyUsagePanel_OmitsLocalTimezoneFooter()
    {
        var xaml = ReadXamlFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml");
        var panelStart = xaml.IndexOf("x:Name=\"DailyUsagePanel\"", StringComparison.Ordinal);
        var panelEnd = xaml.LastIndexOf("</Border>", StringComparison.Ordinal);

        Assert.NotEqual(-1, panelStart);
        Assert.True(panelEnd > panelStart);

        var panelXaml = xaml[panelStart..panelEnd];

        Assert.DoesNotContain("All times are in your local timezone", panelXaml);
        Assert.DoesNotContain("Text=\"&#xE946;\"", panelXaml);
    }

    [Fact]
    public void ExpandedPulseMeter_ShowsEstimatedProjectHealthSection()
    {
        var xaml = ReadPulseMeterMarkup();
        var projectUsageXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml");

        Assert.Contains("Text=\"Project usage\"", xaml);
        Assert.Contains("IsProjectUsageVisible", xaml);
        Assert.Contains("ShouldShowProjectUsage", xaml);
        Assert.Contains("Text=\"PROJECT HEALTH\"", projectUsageXaml);
        Assert.Contains("ProjectUsageRows", projectUsageXaml);
        Assert.Contains("Estimated from local sessions, scaled to account usage", projectUsageXaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedProjectRow, Mode=TwoWay}\"", projectUsageXaml);
        Assert.Contains("Text=\"{Binding LargestIncreaseProjectText}\"", projectUsageXaml);
        Assert.Contains("Text=\"{Binding LargestIncreaseValueText}\"", projectUsageXaml);
        Assert.Contains("Text=\"{Binding LargestDropProjectText}\"", projectUsageXaml);
        Assert.Contains("Text=\"{Binding LargestDropValueText}\"", projectUsageXaml);
        Assert.Contains("Text=\"&#x2197;\"", projectUsageXaml);
        Assert.Contains("Text=\"&#x2198;\"", projectUsageXaml);
        Assert.Contains("FontFamily=\"Segoe UI Symbol\"", projectUsageXaml);
        Assert.Contains("Text=\"30d share\"", projectUsageXaml);
        Assert.Contains("Text=\"Last 7d\"", projectUsageXaml);
        Assert.Contains("Text=\"vs prior 7d\"", projectUsageXaml);
        Assert.Contains("Text=\"Last 30d\"", projectUsageXaml);
        Assert.Contains("Difference between the last 7 days and the 7 days before that.", projectUsageXaml);
        Assert.Contains("Value=\"{Binding SharePercentValue, Mode=OneWay}\"", projectUsageXaml);
        Assert.Contains("BorderThickness\" Value=\"3,0,0,0\"", projectUsageXaml);
        Assert.DoesNotContain("Text=\"{Binding SelectedProjectChatsText}\"", projectUsageXaml);
        Assert.DoesNotContain("Text=\"{Binding SelectedProjectMomentText}\"", projectUsageXaml);
        Assert.DoesNotContain("ThreadCountText", projectUsageXaml);
        Assert.DoesNotContain("RawLocalTokens", projectUsageXaml);
        Assert.DoesNotContain("Prompt", projectUsageXaml);
        Assert.DoesNotContain("Transcript", projectUsageXaml);
    }

    [Fact]
    public void SelectableDashboardLists_RelayMouseWheelInputToTheOuterDashboard()
    {
        var projectUsageXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "UI", "ProjectUsageSection.xaml");
        var scrollRelay = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Shared", "UI", "MouseWheelScrollRelay.cs"));

        Assert.Contains("shared:MouseWheelScrollRelay.RelayToParent=\"True\"", projectUsageXaml);
        Assert.Contains("PreviewMouseWheel", scrollRelay);
        Assert.Contains("FindParentScrollViewer", scrollRelay);
        Assert.Contains("ScrollToVerticalOffset", scrollRelay);
    }

    [Fact]
    public void RateLimitsPanel_ShowsOnlyNonActionableStatusMessages()
    {
        var xaml = ReadPulseMeterMarkup();
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSection.xaml.cs"));

        Assert.Contains("Text=\"{Binding DataContext.StatusMessage, RelativeSource={RelativeSource AncestorType=Window}, NotifyOnTargetUpdated=True}\"", xaml);
        Assert.Contains("Foreground=\"{Binding DataContext.StatusMessageBrush, RelativeSource={RelativeSource AncestorType=Window}}\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.Contains("TargetUpdated=\"StatusMessage_OnTargetUpdated\"", xaml);
        Assert.Contains("AutomationEvents.LiveRegionChanged", code);
        Assert.Contains("RaiseAutomationEvent", code);
        Assert.Contains("Visibility=\"{Binding DataContext.HasSectionStatusMessage, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml);
    }

    [Fact]
    public void ExposedProgressIndicators_HaveMeaningfulAccessibleNames()
    {
        var accountUsage = ReadXamlFile("src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSection.xaml");
        var projectUsage = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml");
        var resetCredits = ReadXamlFile("src", "PulseMeter", "Slices", "ResetCredits", "ResetCreditsSection.xaml");

        Assert.Contains("AutomationProperties.Name=\"{Binding TodayMedianDailyPercentText}\"", accountUsage);
        Assert.Contains("AutomationProperties.Name=\"{Binding ShareText}\"", projectUsage);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding DisplayName}\"", projectUsage);
        Assert.Contains("AutomationProperties.Name=\"{Binding DisplayText}\"", resetCredits);
    }

    [Fact]
    public void CompactHeader_UsesExplicitChevronButtonForExpandCollapse()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("ExpandCollapseTooltip", xaml);
        Assert.Contains("ExpandCollapseButton_Click", xaml);
        Assert.Contains("Data=\"M 1 1 L 6 7 L 11 1\"", xaml);
        Assert.DoesNotContain("Content=\"-\"", xaml);
    }

    [Fact]
    public void PulseMeterWindow_CodeBehindTogglesDailyUsagePanel()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml.cs"));

        Assert.Contains("DailyUsageExpandCollapseButton_Click", code);
        Assert.Contains("ToggleDailyUsageExpanded", code);
    }

    [Fact]
    public void ExpandedPulseMeter_OmitsOldThreadContextCardFromReferenceSurface()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.DoesNotContain("Text=\"Thread context\"", xaml);
        Assert.DoesNotContain("Visibility=\"{Binding HasThreadContext", xaml);
    }

    private static string ReadPulseMeterMarkup()
    {
        var paths = new[]
        {
            new[] { "src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml" },
            new[] { "src", "PulseMeter", "Shared", "Styles", "PulseMeterControls.xaml" },
            new[] { "src", "PulseMeter", "Slices", "ExpandedHeader", "ExpandedHeader.xaml" },
            new[] { "src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml" },
            new[] { "src", "PulseMeter", "Slices", "DataBar", "DataBar.xaml" },
            new[] { "src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "RateLimitsDaily", "RateLimitsDailySection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "RunwayForecast", "RunwayForecastSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "ResetCredits", "ResetCreditsSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "AccountUsage", "AccountUsageSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml" },
            new[] { "src", "PulseMeter", "Slices", "DailyUsage", "DailyUsageSection.xaml" }
        };

        return string.Join(Environment.NewLine, paths.Select(ReadXamlFile));
    }

    private static string ReadXamlFile(params string[] segments)
    {
        return File.ReadAllText(FindWorkspaceFile(segments));
    }

    private static string ReadCompositionFile(string fileName)
    {
        return File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Bootstrap", "Composition", fileName));
    }

    private static string ReadSliceRegistration(string module, string fileName)
    {
        return File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", module, fileName));
    }

    private static string FindWorkspaceFile(params string[] segments)
    {
        var root = TestWorkspace.FindRoot();
        var directPath = Path.Combine([root, .. segments]);
        if (File.Exists(directPath))
        {
            return directPath;
        }

        if (segments is ["src", "PulseMeter", "Slices", var sliceName, var fileName])
        {
            var sliceRoot = Path.Combine(root, "src", "PulseMeter", "Slices", sliceName);
            if (Directory.Exists(sliceRoot))
            {
                var matches = Directory.EnumerateFiles(sliceRoot, fileName, SearchOption.AllDirectories).ToArray();
                if (matches.Length == 1)
                {
                    return matches[0];
                }
            }
        }

        return TestWorkspace.FindFile(segments);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
