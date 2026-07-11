namespace PulseMeter.Tests;

public sealed class PulseMeterWindowLayoutTests
{
    [Fact]
    public void ExpandedPulseMeter_UsesSeparateSectionControlsInWindowMarkup()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");

        Assert.Contains("xmlns:rateLimits=\"clr-namespace:PulseMeter.Slices.RateLimits.UI\"", windowXaml);
        Assert.Contains("xmlns:rateLimitsDaily=\"clr-namespace:PulseMeter.Slices.RateLimitsDaily.UI\"", windowXaml);
        Assert.Contains("xmlns:resetCredits=\"clr-namespace:PulseMeter.Slices.ResetCredits.UI\"", windowXaml);
        Assert.Contains("xmlns:needsAttention=\"clr-namespace:PulseMeter.Slices.NeedsAttention.UI\"", windowXaml);
        Assert.Contains("xmlns:accountUsage=\"clr-namespace:PulseMeter.Slices.AccountUsage.UI\"", windowXaml);
        Assert.Contains("xmlns:projectUsage=\"clr-namespace:PulseMeter.Slices.ProjectUsage.UI\"", windowXaml);
        Assert.Contains("xmlns:usageAttribution=\"clr-namespace:PulseMeter.Slices.UsageAttribution.UI\"", windowXaml);
        Assert.Contains("xmlns:dailyUsage=\"clr-namespace:PulseMeter.Slices.DailyUsage.UI\"", windowXaml);
        Assert.Contains("<rateLimits:RateLimitsSection", windowXaml);
        Assert.Contains("<rateLimitsDaily:RateLimitsDailySection", windowXaml);
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
        Assert.Contains("ToolTip=\"{Binding ExpandCollapseTooltip}\"", dataBarXaml);
        Assert.Contains("Text=\"{Binding CompactTitleText}\"", expandedHeaderXaml);
        Assert.Contains("Text=\"{Binding StatusBadgeText}\"", expandedHeaderXaml);
        Assert.Contains("Command=\"{Binding SyncNowCommand}\"", expandedHeaderXaml);
        Assert.Contains("ToolTip=\"{Binding ExpandCollapseTooltip}\"", expandedHeaderXaml);
    }

    [Fact]
    public void PulseMeterWindow_UsesSharedPulseMeterControlStyleDictionary()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var stylesXaml = ReadXamlFile("src", "PulseMeter", "Shared", "Styles", "PulseMeterControls.xaml");

        Assert.Contains("Source=\"/Shared/Styles/PulseMeterControls.xaml\"", windowXaml);
        Assert.DoesNotContain("x:Key=\"LightCardStyle\"", windowXaml);
        Assert.DoesNotContain("x:Key=\"CompactIconButtonStyle\"", windowXaml);
        Assert.Contains("x:Key=\"LightCardStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"CompactIconButtonStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightTextBoxStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightEvidencePillStyle\"", stylesXaml);
        Assert.Contains("x:Key=\"LightEvidencePillTextStyle\"", stylesXaml);
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
        Assert.Contains("AddSingleton<IUsageAttributionService, UsageAttributionService>", coreRegistration);
        Assert.Contains("AddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>", coreRegistration);
        Assert.Contains("internal static class PlatformRegistration", infrastructureRegistration);
        Assert.Contains("internal static IServiceCollection AddPulseMeterPlatform", infrastructureRegistration);
        Assert.Contains("AddSingleton<IPulseMeterAppSettingsStore, PulseMeterAppSettingsStore>", infrastructureRegistration);
        Assert.Contains("AddSingleton<IPulseMeterWindowLifecycleCoordinator, PulseMeterWindowLifecycleCoordinator>", infrastructureRegistration);
        Assert.Contains("internal static class PulseMeterWindowRegistration", shellRegistration);
        Assert.Contains("internal static IServiceCollection AddPulseMeter", shellRegistration);
        Assert.Contains("AddSingleton(sp =>", shellRegistration);
        Assert.Contains("new PulseMeterWindowViewModel(", shellRegistration);
        Assert.Contains("AddSingleton<IPulseMeterWindow>", shellRegistration);
        Assert.Contains("AddSingleton<ITrayIconService, TrayIconService>", shellRegistration);
    }

    [Fact]
    public void BurnAnalysisSection_IsRegisteredInNavigationAndBetweenProjectAndDailyUsage()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var navigationXaml = ReadXamlFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml");
        var usageAttributionSection = ReadXamlFile("src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml");
        var usageAttributionSectionCode = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "UsageAttribution", "UsageAttributionSection.xaml.cs"));
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));

        Assert.Contains("<usageAttribution:UsageAttributionSection DataContext=\"{Binding UsageAttribution}\"", windowXaml);
        Assert.True(
            windowXaml.IndexOf("<projectUsage:ProjectUsageSection", StringComparison.Ordinal) <
            windowXaml.IndexOf("<usageAttribution:UsageAttributionSection", StringComparison.Ordinal));
        Assert.True(
            windowXaml.IndexOf("<usageAttribution:UsageAttributionSection", StringComparison.Ordinal) <
            windowXaml.IndexOf("<dailyUsage:DailyUsageSection", StringComparison.Ordinal));
        Assert.Contains("IsUsageAttributionVisible", navigationXaml);
        Assert.Contains("Text=\"Burn analysis\"", navigationXaml);
        Assert.Contains("ToolTip=\"Go to burn analysis\"", navigationXaml);
        Assert.Contains("public UsageAttributionSectionViewModel UsageAttribution { get; }", pulseMeterWindowViewModel);
        Assert.Contains("public bool ShouldShowUsageAttribution", pulseMeterWindowViewModel);
        Assert.Contains("Visibility=\"{Binding DataContext.ShouldShowUsageAttribution, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", usageAttributionSection);
        Assert.Contains("Text=\"{Binding EmptyStateText}\"", usageAttributionSection);
        Assert.Contains("Binding=\"{Binding HasAttribution}\" Value=\"False\"", usageAttributionSection);
        Assert.Contains("Text=\"BURN ANALYSIS\"", usageAttributionSection);
        Assert.Contains("Text=\"Top chats by token burn\"", usageAttributionSection);
        Assert.Contains("Text=\"Chat\"", usageAttributionSection);
        Assert.Contains("Text=\"Share\"", usageAttributionSection);
        Assert.Contains("x:Name=\"ShareInfoIcon\"", usageAttributionSection);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", usageAttributionSection);
        Assert.Contains("Text=\"&#xE946;\"", usageAttributionSection);
        Assert.Contains("ToolTip=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection);
        Assert.Contains("Text=\"Tokens\"", usageAttributionSection);
        Assert.Contains("Text=\"Event\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisTablesGrid\"", usageAttributionSection);
        Assert.Contains("SizeChanged=\"BurnAnalysisTablesGrid_SizeChanged\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisChatsColumn\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisGapColumn\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisEventsColumn\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisStackedGapRow\"", usageAttributionSection);
        Assert.Contains("x:Name=\"BurnAnalysisEventsPanel\"", usageAttributionSection);
        var chatsColumnStart = usageAttributionSection.IndexOf("x:Name=\"BurnAnalysisChatsColumn\"", StringComparison.Ordinal);
        var gapColumnStart = usageAttributionSection.IndexOf("x:Name=\"BurnAnalysisGapColumn\"", StringComparison.Ordinal);
        var eventsColumnStart = usageAttributionSection.IndexOf("x:Name=\"BurnAnalysisEventsColumn\"", StringComparison.Ordinal);
        Assert.NotEqual(-1, chatsColumnStart);
        Assert.NotEqual(-1, gapColumnStart);
        Assert.NotEqual(-1, eventsColumnStart);
        Assert.Contains("Width=\"6*\"", usageAttributionSection[chatsColumnStart..gapColumnStart]);
        Assert.Contains("MinWidth=\"440\"", usageAttributionSection[chatsColumnStart..gapColumnStart]);
        Assert.Contains("Width=\"5*\"", usageAttributionSection[eventsColumnStart..]);
        Assert.Contains("MinWidth=\"400\"", usageAttributionSection[eventsColumnStart..]);
        Assert.Contains("Width=\"28\"", usageAttributionSection[gapColumnStart..eventsColumnStart]);
        Assert.DoesNotContain("<ColumnDefinition Width=\"5*\" MinWidth=\"360\" />", usageAttributionSection);
        Assert.DoesNotContain("<ColumnDefinition Width=\"480\" />", usageAttributionSection);
        Assert.DoesNotContain("<ColumnDefinition Width=\"380\" />", usageAttributionSection);
        Assert.DoesNotContain("<ColumnDefinition Width=\"280\" />", usageAttributionSection);
        Assert.Contains("BurnAnalysisStackedLayoutThreshold", usageAttributionSectionCode);
        Assert.Contains("BurnAnalysisTablesGrid_SizeChanged", usageAttributionSectionCode);
        Assert.Contains("ApplyBurnAnalysisLayout", usageAttributionSectionCode);
        Assert.Contains("Grid.SetRow(BurnAnalysisEventsPanel, 2)", usageAttributionSectionCode);
        Assert.Contains("Grid.SetColumn(BurnAnalysisEventsPanel, 0)", usageAttributionSectionCode);
        Assert.Contains("Grid.SetColumnSpan(BurnAnalysisEventsPanel, 3)", usageAttributionSectionCode);
        Assert.Contains("BurnAnalysisGapColumn.Width = new GridLength(0)", usageAttributionSectionCode);
        Assert.Contains("BurnAnalysisEventsColumn.Width = new GridLength(0)", usageAttributionSectionCode);
        Assert.DoesNotContain("Text=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"Share of total burned tokens in last 30 days\"", usageAttributionSection);
        Assert.DoesNotContain("Text=\"{Binding SummaryText}\"", usageAttributionSection);
        Assert.Contains("ToolTip=\"{Binding TooltipText}\"", usageAttributionSection);
        var shareHeaderStart = usageAttributionSection.IndexOf("Text=\"Share\"", StringComparison.Ordinal);
        var shareInfoIconStart = usageAttributionSection.IndexOf("x:Name=\"ShareInfoIcon\"", StringComparison.Ordinal);
        Assert.True(shareHeaderStart < shareInfoIconStart);
        Assert.DoesNotContain("ToolTip=\"Share is of total burned tokens in the last 30 days.\"", usageAttributionSection[shareHeaderStart..shareInfoIconStart]);
        var topChatsStart = usageAttributionSection.IndexOf("Text=\"Top chats by token burn\"", StringComparison.Ordinal);
        var burnEventsStart = usageAttributionSection.IndexOf("Text=\"Largest burn events\"", StringComparison.Ordinal);
        var topChatsSection = usageAttributionSection[topChatsStart..burnEventsStart];
        var burnEventsSection = usageAttributionSection[burnEventsStart..];
        Assert.DoesNotContain("Text=\"Burn\"", topChatsSection);
        Assert.DoesNotContain("<ProgressBar", topChatsSection);
        Assert.DoesNotContain("SharePercentValue", topChatsSection);
        Assert.DoesNotContain("Text=\"{Binding BreakdownText}\"", topChatsSection);
        Assert.DoesNotContain("Text=\"{Binding RawLocalTokensText}\"", topChatsSection);
        Assert.Equal(2, CountOccurrences(burnEventsSection, "<ColumnDefinition Width=\"96\" />"));
        Assert.DoesNotContain("<ColumnDefinition Width=\"76\" />", burnEventsSection);
        var topChatsShareHeaderStart = topChatsSection.IndexOf("Text=\"Share\"", StringComparison.Ordinal);
        var topChatsTokensHeaderStart = topChatsSection.IndexOf("Text=\"Tokens\"", topChatsShareHeaderStart, StringComparison.Ordinal);
        var topChatsShareHeaderStackStart = topChatsSection.LastIndexOf("<StackPanel Grid.Column=\"1\"", topChatsShareHeaderStart, StringComparison.Ordinal);
        Assert.NotEqual(-1, topChatsShareHeaderStart);
        Assert.NotEqual(-1, topChatsTokensHeaderStart);
        Assert.NotEqual(-1, topChatsShareHeaderStackStart);
        Assert.Contains("HorizontalAlignment=\"Center\"", topChatsSection[topChatsShareHeaderStackStart..topChatsTokensHeaderStart]);
        var topChatsShareValueStart = topChatsSection.IndexOf("Text=\"{Binding ShareText}\"", StringComparison.Ordinal);
        var topChatsTokensValueStart = topChatsSection.IndexOf("Text=\"{Binding EstimatedTokensText}\"", topChatsShareValueStart, StringComparison.Ordinal);
        var topChatsShareValueStackStart = topChatsSection.LastIndexOf("<StackPanel Grid.Column=\"1\"", topChatsShareValueStart, StringComparison.Ordinal);
        Assert.NotEqual(-1, topChatsShareValueStart);
        Assert.NotEqual(-1, topChatsTokensValueStart);
        Assert.NotEqual(-1, topChatsShareValueStackStart);
        Assert.Contains("HorizontalAlignment=\"Center\"", topChatsSection[topChatsShareValueStackStart..topChatsTokensValueStart]);
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
        Assert.Contains("Text=\"{Binding DataContext.AutoSyncSeconds, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", accountUsageSection);
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
    public void NeedsAttentionModule_UsesChildSectionViewModelAndSelfHidingSection()
    {
        var windowXaml = ReadXamlFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml");
        var needsAttentionRegistration = ReadSliceRegistration("NeedsAttention", "NeedsAttentionRegistration.cs");
        var pulseMeterWindowViewModel = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindowViewModel.cs"));
        var needsAttentionSection = ReadXamlFile("src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSection.xaml");

        Assert.True(File.Exists(FindWorkspaceFile("src", "PulseMeter", "Slices", "NeedsAttention", "NeedsAttentionSectionViewModel.cs")));
        Assert.Contains("<needsAttention:NeedsAttentionSection DataContext=\"{Binding NeedsAttention}\"", windowXaml);
        Assert.Contains("public NeedsAttentionSectionViewModel NeedsAttention { get; }", pulseMeterWindowViewModel);
        Assert.DoesNotContain("INeedsAttentionPresenter?", pulseMeterWindowViewModel);
        Assert.Contains("AddSingleton<NeedsAttentionSectionViewModel>", needsAttentionRegistration);
        Assert.Contains("Visibility=\"{Binding HasNeedsAttention, Converter={StaticResource BooleanToVisibilityConverter}}\"", needsAttentionSection);
        Assert.Contains("ItemsSource=\"{Binding NeedsAttentionItems}\"", needsAttentionSection);
        Assert.Contains("Command=\"{Binding DataContext.CopyDiagnosticCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}\"", needsAttentionSection);
        Assert.Contains("Command=\"{Binding DataContext.DismissSignalCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}\"", needsAttentionSection);
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
    public void RateLimitsSection_ShowsCompactRunwayHintWithEvidenceLabel()
    {
        var rateLimitsSection = ReadXamlFile("src", "PulseMeter", "Slices", "RateLimits", "RateLimitsSection.xaml");

        Assert.Contains("x:Name=\"RunwayHintPanel\"", rateLimitsSection);
        Assert.Contains("Visibility=\"{Binding HasRunwayHint, Converter={StaticResource BooleanToVisibilityConverter}}\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding RunwayHintText}\"", rateLimitsSection);
        Assert.Contains("Text=\"{Binding RunwayEvidenceText}\"", rateLimitsSection);
        Assert.Contains("Style=\"{DynamicResource LightEvidencePillStyle}\"", rateLimitsSection);
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

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void PulseMeter_UsesWhiteExpandedDashboardWithCompactDarkHeader()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("Property=\"Background\" Value=\"#20242C\"", xaml);
        Assert.Contains("Property=\"Background\" Value=\"#FFFFFF\"", xaml);
        Assert.Contains("Property=\"BorderBrush\" Value=\"#E5E7EB\"", xaml);
        Assert.Contains("Text=\"PulseMeter\"", xaml);
        Assert.Contains("Text=\"Overview\"", xaml);
    }

    [Fact]
    public void ExpandedSidebar_HasCollapseToggleAndCollapsedWidthBinding()
    {
        var xaml = ReadPulseMeterMarkup();
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "NavigationRail.xaml.cs"));

        Assert.Contains("NavigationPanelToggleButtonStyle", xaml);
        Assert.Contains("Width=\"{Binding NavigationPanelWidth}\"", xaml);
        Assert.Contains("IsNavigationPanelExpanded", xaml);
        Assert.Contains("NavigationPanelToggleGlyph", xaml);
        Assert.Contains("NavigationPanelToggleTooltip", xaml);
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

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_UsesBlueOverviewUnderline()
    {
        var xaml = ReadPulseMeterMarkup();
        var overviewIndex = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, overviewIndex);

        var overviewBlock = xaml[overviewIndex..Math.Min(xaml.Length, overviewIndex + 1_200)];
        Assert.Contains("Background=\"#1F73FF\"", overviewBlock);
        Assert.DoesNotContain("Background=\"#16A34A\"", overviewBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_UsesOverviewUnderlineAndSectionSwitches()
    {
        var xaml = ReadPulseMeterMarkup();
        var underlineStyleStart = xaml.IndexOf("x:Key=\"NavigationUnderlineStyle\"", StringComparison.Ordinal);
        var underlineStyleEnd = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", underlineStyleStart, StringComparison.Ordinal);
        var navigationStyleStart = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", StringComparison.Ordinal);
        var navigationStyleEnd = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", navigationStyleStart, StringComparison.Ordinal);
        var overviewUnderlineStart = xaml.IndexOf("x:Name=\"OverviewUnderline\"", StringComparison.Ordinal);
        var overviewTextStart = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);
        var overviewStart = xaml.LastIndexOf("<Border Height=\"64\"", overviewTextStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, underlineStyleStart);
        Assert.NotEqual(-1, navigationStyleStart);
        Assert.NotEqual(-1, overviewUnderlineStart);
        Assert.NotEqual(-1, overviewStart);
        Assert.True(underlineStyleEnd > underlineStyleStart);
        Assert.True(navigationStyleEnd > navigationStyleStart);

        var underlineStyle = xaml[underlineStyleStart..underlineStyleEnd];
        var navigationStyle = xaml[navigationStyleStart..navigationStyleEnd];
        var overviewUnderline = xaml[overviewUnderlineStart..Math.Min(xaml.Length, overviewUnderlineStart + 500)];
        var overviewBlock = xaml[overviewStart..Math.Min(xaml.Length, overviewStart + 2_000)];
        var longLabelStart = xaml.IndexOf("Text=\"Rate Limits Daily\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, longLabelStart);

        var longLabelBlock = xaml[longLabelStart..Math.Min(xaml.Length, longLabelStart + 500)];

        Assert.Contains("<Setter Property=\"Padding\" Value=\"18,34,14,28\" />", xaml);
        Assert.DoesNotContain("<Setter Property=\"Padding\" Value=\"18,34,12,28\" />", xaml);
        Assert.DoesNotContain("<Setter Property=\"Margin\" Value=\"-16,24,0,0\" />", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"14,0,14,4\" />", underlineStyle);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", underlineStyle);
        Assert.Contains("<Setter Property=\"Width\" Value=\"14\" />", underlineStyle);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", underlineStyle);
        Assert.DoesNotContain("ActiveUnderline", navigationStyle);
        Assert.DoesNotContain("Style=\"{StaticResource NavigationUnderlineStyle}\"", navigationStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"8,0,0,0\" />", navigationStyle);
        Assert.Contains("x:Name=\"SectionSwitchTrack\"", navigationStyle);
        Assert.Contains("x:Name=\"SectionSwitchThumb\"", navigationStyle);
        Assert.Contains("Grid.Column=\"0\"", navigationStyle);
        Assert.Contains("<ColumnDefinition Width=\"*\" />", navigationStyle);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", navigationStyle);
        Assert.Contains("Width=\"28\"", navigationStyle);
        Assert.Contains("Height=\"15\"", navigationStyle);
        Assert.Contains("CornerRadius=\"7.5\"", navigationStyle);
        Assert.Contains("Width=\"13\"", navigationStyle);
        Assert.Contains("Height=\"13\"", navigationStyle);
        Assert.Contains("Margin=\"4,0,0,0\"", navigationStyle);
        Assert.DoesNotContain("Width=\"42\"", navigationStyle);
        Assert.DoesNotContain("Height=\"24\"", navigationStyle);
        Assert.Contains("<Setter TargetName=\"SectionSwitchTrack\" Property=\"Background\" Value=\"#65D75F\" />", navigationStyle);
        Assert.Contains("HorizontalAlignment=\"Right\"", navigationStyle);
        Assert.Contains("Style=\"{StaticResource NavigationUnderlineStyle}\"", overviewUnderline);
        Assert.DoesNotContain("Width=\"14\"", overviewUnderline);
        Assert.Contains("Background=\"#1F73FF\"", overviewUnderline);
        Assert.Contains("VerticalAlignment=\"Center\"", longLabelBlock);
        Assert.DoesNotContain("<Setter Property=\"Padding\" Value=\"14,0\" />", overviewBlock);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", overviewBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_UsesFullWidthRowsSoLongLabelsStayReadable()
    {
        var xaml = ReadPulseMeterMarkup();
        var navigationStyleStart = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", StringComparison.Ordinal);
        var navigationStyleEnd = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", navigationStyleStart, StringComparison.Ordinal);
        var longLabelStart = xaml.IndexOf("Text=\"Rate Limits Daily\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, navigationStyleStart);
        Assert.True(navigationStyleEnd > navigationStyleStart);
        Assert.NotEqual(-1, longLabelStart);

        var navigationStyle = xaml[navigationStyleStart..navigationStyleEnd];
        var longLabelBlock = xaml[longLabelStart..Math.Min(xaml.Length, longLabelStart + 500)];

        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", navigationStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"8,0,0,0\" />", navigationStyle);
        Assert.Contains("Width=\"28\"", navigationStyle);
        Assert.Contains("Height=\"15\"", navigationStyle);
        Assert.Contains("Width=\"13\"", navigationStyle);
        Assert.Contains("Height=\"13\"", navigationStyle);
        Assert.Contains("FontSize=\"13.5\"", longLabelBlock);
        Assert.DoesNotContain("TextTrimming", longLabelBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_KeepsSectionSwitchesAwayFromRightEdge()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("<Setter Property=\"Padding\" Value=\"18,34,14,28\" />", xaml);
        Assert.DoesNotContain("<Setter Property=\"Padding\" Value=\"18,34,4,28\" />", xaml);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_MovesIconAndLabelGroupLeftWithoutMovingSwitches()
    {
        var xaml = ReadPulseMeterMarkup();
        var navigationStyleStart = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", StringComparison.Ordinal);
        var navigationStyleEnd = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", navigationStyleStart, StringComparison.Ordinal);
        var overviewTextStart = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);
        var overviewStart = xaml.LastIndexOf("<Border Height=\"64\"", overviewTextStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, navigationStyleStart);
        Assert.True(navigationStyleEnd > navigationStyleStart);
        Assert.NotEqual(-1, overviewStart);

        var navigationStyle = xaml[navigationStyleStart..navigationStyleEnd];
        var overviewBlock = xaml[overviewStart..Math.Min(xaml.Length, overviewStart + 3_000)];

        Assert.Contains("<Setter Property=\"Padding\" Value=\"8,0,0,0\" />", navigationStyle);
        Assert.DoesNotContain("<Setter Property=\"Padding\" Value=\"14,0,0,0\" />", navigationStyle);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", overviewBlock);
        Assert.Contains("Width=\"28\"", navigationStyle);
        Assert.Contains("Margin=\"4,0,0,0\"", navigationStyle);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_CentersOverviewIconAndLabelOverUnderline()
    {
        var xaml = ReadPulseMeterMarkup();
        var overviewTextStart = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);
        var overviewStart = xaml.LastIndexOf("<Border Height=\"64\"", overviewTextStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, overviewStart);

        var overviewBlock = xaml[overviewStart..Math.Min(xaml.Length, overviewStart + 3_000)];

        Assert.Contains("x:Name=\"OverviewUnderline\"", overviewBlock);
        Assert.Contains("Style=\"{StaticResource NavigationUnderlineStyle}\"", overviewBlock);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", overviewBlock);
        Assert.DoesNotContain("<Setter Property=\"Margin\" Value=\"8,0\" />", overviewBlock);
        Assert.DoesNotContain("<Setter Property=\"Margin\" Value=\"14,0\" />", overviewBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_CentersCollapsedIconCellsWithoutClippingOffsets()
    {
        var xaml = ReadPulseMeterMarkup();
        var navigationStyleStart = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", StringComparison.Ordinal);
        var navigationStyleEnd = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", navigationStyleStart, StringComparison.Ordinal);
        var overviewTextStart = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);
        var overviewStart = xaml.LastIndexOf("<Border Height=\"64\"", overviewTextStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, navigationStyleStart);
        Assert.True(navigationStyleEnd > navigationStyleStart);
        Assert.NotEqual(-1, overviewStart);

        var navigationStyle = xaml[navigationStyleStart..navigationStyleEnd];
        var overviewBlock = xaml[overviewStart..Math.Min(xaml.Length, overviewStart + 1_800)];

        Assert.DoesNotContain("<Setter Property=\"Margin\" Value=\"-8,24,0,0\" />", xaml);
        Assert.DoesNotContain("<Setter Property=\"Margin\" Value=\"-16,24,0,0\" />", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0,24,0,0\" />", xaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"36\" />", navigationStyle);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"0\" />", navigationStyle);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", navigationStyle);
        Assert.Contains("<Setter Property=\"Width\" Value=\"36\" />", overviewBlock);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Center\" />", overviewBlock);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0\" />", overviewBlock);
        Assert.DoesNotContain("<Setter Property=\"Padding\" Value=\"14,0\" />", overviewBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_HidesSectionSwitchesWhenNavigationIsCollapsed()
    {
        var xaml = ReadPulseMeterMarkup();
        var navigationStyleStart = xaml.IndexOf("x:Key=\"NavigationSectionToggleStyle\"", StringComparison.Ordinal);
        var navigationStyleEnd = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", navigationStyleStart, StringComparison.Ordinal);
        var overviewTextStart = xaml.IndexOf("Text=\"Overview\"", StringComparison.Ordinal);
        var overviewStart = xaml.LastIndexOf("<Border Height=\"64\"", overviewTextStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, navigationStyleStart);
        Assert.True(navigationStyleEnd > navigationStyleStart);
        Assert.NotEqual(-1, overviewStart);

        var navigationStyle = xaml[navigationStyleStart..navigationStyleEnd];
        var overviewBlock = xaml[overviewStart..Math.Min(xaml.Length, overviewStart + 4_200)];

        Assert.Contains("x:Name=\"SectionSwitch\"", navigationStyle);
        Assert.Contains("TargetName=\"SectionSwitch\"", navigationStyle);
        Assert.Contains("<Setter TargetName=\"SectionSwitch\" Property=\"Visibility\" Value=\"Collapsed\" />", navigationStyle);
        Assert.DoesNotContain("ActiveUnderline", navigationStyle);
        Assert.DoesNotContain("Style=\"{StaticResource NavigationUnderlineStyle}\"", navigationStyle);
        Assert.Contains("x:Name=\"OverviewUnderline\"", overviewBlock);
        Assert.Contains("Style=\"{StaticResource NavigationUnderlineStyle}\"", overviewBlock);
    }

    [Fact(Skip = "Replaced by navigation button contract.")]
    public void ExpandedSidebar_UsesSharedCollapsedNavIconCellStyles()
    {
        var xaml = ReadPulseMeterMarkup();
        var contentStyleStart = xaml.IndexOf("x:Key=\"NavigationItemContentStyle\"", StringComparison.Ordinal);
        var iconCellStyleStart = xaml.IndexOf("x:Key=\"NavigationIconCellStyle\"", StringComparison.Ordinal);
        var glyphStyleStart = xaml.IndexOf("x:Key=\"NavigationIconGlyphStyle\"", StringComparison.Ordinal);

        Assert.NotEqual(-1, contentStyleStart);
        Assert.NotEqual(-1, iconCellStyleStart);
        Assert.NotEqual(-1, glyphStyleStart);

        var contentStyle = xaml[contentStyleStart..Math.Min(xaml.Length, contentStyleStart + 900)];
        var iconCellStyle = xaml[iconCellStyleStart..Math.Min(xaml.Length, iconCellStyleStart + 700)];
        var glyphStyle = xaml[glyphStyleStart..Math.Min(xaml.Length, glyphStyleStart + 900)];

        Assert.Contains("Setter Property=\"HorizontalAlignment\" Value=\"Left\"", contentStyle);
        Assert.Contains("Setter Property=\"HorizontalAlignment\" Value=\"Center\"", contentStyle);
        Assert.Contains("Setter Property=\"Width\" Value=\"22\"", iconCellStyle);
        Assert.Contains("Setter Property=\"Height\" Value=\"22\"", iconCellStyle);
        Assert.Contains("Setter Property=\"HorizontalAlignment\" Value=\"Center\"", iconCellStyle);
        Assert.Contains("Setter Property=\"TextAlignment\" Value=\"Center\"", glyphStyle);
        var directContentStyleUses = CountOccurrences(xaml, "Style=\"{StaticResource NavigationItemContentStyle}\"");
        var basedOnContentStyleUses = CountOccurrences(xaml, "BasedOn=\"{StaticResource NavigationItemContentStyle}\"");
        Assert.Equal(8, directContentStyleUses + basedOnContentStyleUses);
        Assert.Equal(8, CountOccurrences(xaml, "Style=\"{StaticResource NavigationIconCellStyle}\""));
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
        var autoRefreshBlock = xaml[autoRefreshStart..Math.Min(xaml.Length, autoRefreshStart + 1_400)];
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
    public void NavigationRail_UsesNavigationButtonsAndSeparateCustomizePopup()
    {
        var xaml = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml"));
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "NavigationRail", "UI", "NavigationRail.xaml.cs"));

        Assert.Contains("x:Key=\"NavigationSectionButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"CustomizeDashboardButton\"", xaml);
        Assert.Contains("x:Name=\"CustomizeDashboardPopup\"", xaml);
        Assert.Contains("StaysOpen=\"False\"", xaml);
        Assert.Contains("Choose visible sections", xaml);
        Assert.Contains("CommandParameter=\"{x:Static navModels:NavigationSection.RateLimits}\"", xaml);
        Assert.Contains("IsRateLimitsVisible", xaml);
        Assert.Contains("IsUsageAttributionVisible", xaml);
        Assert.Contains("SectionRequested", code);
        Assert.Contains("SectionButton_Click", code);
        Assert.Contains("CustomizeDashboardPopup_KeyDown", code);
        Assert.DoesNotContain("NavigationSectionToggleStyle", xaml);
    }

    [Fact]
    public void NavigationRail_PlacesRequestedSectionAtTheTopOfTheViewport()
    {
        var code = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml.cs"));
        var handlerStart = code.IndexOf("private void NavigationRail_SectionRequested", StringComparison.Ordinal);
        var handler = code[handlerStart..Math.Min(code.Length, handlerStart + 1_400)];

        Assert.Contains("ScrollToVerticalOffset", handler);
        Assert.Contains("TransformToAncestor(ExpandedContentScrollViewer)", handler);
        Assert.DoesNotContain("target.BringIntoView()", handler);
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
    public void ExpandedPulseMeter_ShowsEstimatedProjectUsageSection()
    {
        var xaml = ReadPulseMeterMarkup();
        var projectUsageXaml = ReadXamlFile("src", "PulseMeter", "Slices", "ProjectUsage", "ProjectUsageSection.xaml");

        Assert.Contains("Text=\"Project usage\"", xaml);
        Assert.Contains("IsProjectUsageVisible", xaml);
        Assert.Contains("ShouldShowProjectUsage", xaml);
        Assert.Contains("Project usage - last 30 days", projectUsageXaml);
        Assert.Contains("ProjectUsageRows", projectUsageXaml);
        Assert.Contains("Estimated from local sessions, scaled to account usage", projectUsageXaml);
        Assert.DoesNotContain("ThreadCountText", projectUsageXaml);
        Assert.DoesNotContain("RawLocalTokens", projectUsageXaml);
    }

    [Fact]
    public void RateLimitsPanel_ShowsStatusMessageWhenCodexIsUnavailable()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("Text=\"{Binding DataContext.StatusMessage, RelativeSource={RelativeSource AncestorType=Window}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding DataContext.HasStatusMessage, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml);
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

    [Fact(Skip = "Navigation rail no longer owns sync feedback.")]
    public void SyncFooter_UsesSingleTimestampFeedbackLine()
    {
        var xaml = ReadPulseMeterMarkup();

        Assert.Contains("SyncFeedbackText", xaml);
        Assert.DoesNotContain("LastUpdatedText", xaml);
        Assert.DoesNotContain("SourceText", xaml);
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
