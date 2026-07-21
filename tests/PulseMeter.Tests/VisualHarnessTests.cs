using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Platform.Codex;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ResetCredits.Business;
using PulseMeter.Slices.ResetCredits.Models;
using PulseMeter.Slices.UsageAttribution.Business;
using PulseMeter.Slices.UsageCollection.Business;
using PulseMeter.Slices.UsageCollection.Models;
using PulseMeter.Slices.UsageSignals.Business;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.VisualHarness;

namespace PulseMeter.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class VisualHarnessEnvironmentCollection
{
    public const string Name = "Visual harness environment";
}

[Collection(VisualHarnessEnvironmentCollection.Name)]
public sealed class VisualHarnessTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "PulseMeter.VisualHarness.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidateRoot_ReturnsOnlyFixedWorktreePaths_WithoutCreatingState()
    {
        var workspace = CreateWorkspace();

        var paths = VisualHarnessWorkspace.ValidateRoot(workspace);

        Assert.Equal(Path.GetFullPath(workspace), paths.WorkspaceRoot);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(workspace), "artifacts", "visual-harness", "state"),
            paths.StateRoot);
        Assert.Equal(Path.Combine(paths.StateRoot, "settings.json"), paths.AppSettingsPath);
        Assert.Equal(Path.Combine(paths.StateRoot, "window-state.json"), paths.WindowStatePath);
        Assert.Equal(Path.Combine(paths.StateRoot, "reset-credits.json"), paths.ResetCreditStatePath);
        Assert.Equal(Path.Combine(paths.StateRoot, "runway-observations.json"), paths.RunwayObservationsPath);
        Assert.False(Directory.Exists(paths.StateRoot));
    }

    [Fact]
    public void LocateFrom_FindsTheNearestMarkedWorktree()
    {
        var workspace = CreateWorkspace();
        var nested = Directory.CreateDirectory(Path.Combine(workspace, "one", "two")).FullName;

        var paths = VisualHarnessWorkspace.LocateFrom(nested);

        Assert.Equal(Path.GetFullPath(workspace), paths.WorkspaceRoot);
    }

    [Fact]
    public void InvalidOrOutsideRoot_IsRejectedBeforeStateCreation()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_testRoot, "outside")).FullName;

        Assert.Throws<InvalidOperationException>(() => VisualHarnessWorkspace.ValidateRoot(outside));
        Assert.Throws<InvalidOperationException>(() => VisualHarnessWorkspace.LocateFrom(outside));
        Assert.False(Directory.Exists(Path.Combine(outside, "artifacts")));
    }

    [Fact]
    public void ReparsePointComponent_IsRejectedBeforeStateCreation()
    {
        var workspace = CreateWorkspace();
        var canonicalWorkspace = Path.GetFullPath(workspace);

        FileAttributes Attributes(string path)
        {
            var attributes = File.GetAttributes(path);
            return string.Equals(
                Path.TrimEndingDirectorySeparator(path),
                Path.TrimEndingDirectorySeparator(canonicalWorkspace),
                StringComparison.OrdinalIgnoreCase)
                ? attributes | FileAttributes.ReparsePoint
                : attributes;
        }

        Assert.Throws<InvalidOperationException>(
            () => VisualHarnessWorkspace.ValidateRoot(workspace, Attributes));
        Assert.False(Directory.Exists(Path.Combine(workspace, "artifacts")));
    }

    [Fact]
    public void Composition_IsMockOnly_AndOmitsLiveCollectionServices()
    {
        var paths = VisualHarnessWorkspace.ValidateRoot(CreateWorkspace());
        using var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { });

        var usage = provider.GetRequiredService<IUsageService>();
        var mock = provider.GetRequiredService<IMockUsageService>();

        Assert.IsType<VisualHarnessUsageService>(usage);
        Assert.Same(usage, mock);
        Assert.True(mock.UseMockMode);
        Assert.Null(provider.GetService<CodexUsageService>());
        Assert.Null(provider.GetService<ICodexResetCreditService>());
        Assert.Null(provider.GetService<IProjectUsageService>());
        Assert.Null(provider.GetService<IUsageAttributionService>());
        Assert.Null(provider.GetService<IAppServerProcessFactory>());
        Assert.Null(provider.GetService<IJsonRpcClientFactory>());
        Assert.NotNull(provider.GetRequiredService<PulseMeterWindowViewModel>());
    }

    [Theory]
    [InlineData(new string[0], VisualHarnessScenario.Healthy)]
    [InlineData(new[] { "--scenario", "healthy" }, VisualHarnessScenario.Healthy)]
    [InlineData(new[] { "--scenario=mock" }, VisualHarnessScenario.Healthy)]
    [InlineData(new[] { "--scenario", "unavailable" }, VisualHarnessScenario.Unavailable)]
    [InlineData(new[] { "--scenario=stale" }, VisualHarnessScenario.Stale)]
    public void ScenarioParser_AcceptsOnlyExplicitSafeDemoStates(
        string[] args,
        VisualHarnessScenario expected)
    {
        Assert.Equal(expected, VisualHarnessScenarioParser.Parse(args));
    }

    [Theory]
    [InlineData("--scenario")]
    [InlineData("--scenario=live")]
    [InlineData("--scenario=unknown")]
    public void ScenarioParser_RejectsMissingOrUnsafeValues(string argument)
    {
        Assert.Throws<ArgumentException>(() => VisualHarnessScenarioParser.Parse([argument]));
    }

    [Theory]
    [InlineData(VisualHarnessScenario.Unavailable, SyncStatus.Unavailable, "The monitored app is not running")]
    [InlineData(VisualHarnessScenario.Stale, SyncStatus.Stale, "Cached usage is older than expected")]
    public async Task ScenarioUsageService_ProvidesDeterministicNonLiveSnapshots(
        VisualHarnessScenario scenario,
        SyncStatus expectedStatus,
        string expectedMessage)
    {
        var service = new VisualHarnessUsageService(scenario);
        UsageSnapshot? announcedSnapshot = null;
        service.SnapshotUpdated += (_, snapshot) => announcedSnapshot = snapshot;

        var snapshot = await service.GetSnapshotAsync();

        Assert.False(service.UseMockMode);
        Assert.Equal(expectedStatus, snapshot.SyncStatus);
        Assert.Equal("VisualHarness", snapshot.Source);
        Assert.Contains(expectedMessage, snapshot.StatusMessage);
        Assert.NotEmpty(snapshot.Buckets);
        Assert.Same(snapshot, announcedSnapshot);
    }

    [Fact]
    public void Composition_UsesNoOpOsServices_AndLeavesLocalAppDataSentinelUntouched()
    {
        var workspace = CreateWorkspace();
        var paths = VisualHarnessWorkspace.ValidateRoot(workspace);
        var fakeLocalAppData = Directory.CreateDirectory(Path.Combine(_testRoot, "local-app-data")).FullName;
        var sentinel = Path.Combine(fakeLocalAppData, "sentinel.txt");
        File.WriteAllText(sentinel, "unchanged");
        var previousLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");

        try
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", fakeLocalAppData);
            using var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { });

            Assert.IsType<VisualHarnessForegroundWindowService>(
                provider.GetRequiredService<IForegroundWindowService>());
            Assert.IsType<VisualHarnessIdleTimeProvider>(
                provider.GetRequiredService<IUserIdleTimeProvider>());
            Assert.IsType<VisualHarnessClipboardService>(
                provider.GetRequiredService<IClipboardService>());
            Assert.IsType<VisualHarnessTrayIconService>(
                provider.GetRequiredService<ITrayIconService>());
            Assert.Equal(TimeSpan.Zero, provider.GetRequiredService<IUserIdleTimeProvider>().GetIdleTime());
            Assert.Equal(
                new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false),
                provider.GetRequiredService<IForegroundWindowService>()
                    .GetCodexForegroundState(IntPtr.Zero));

            provider.GetRequiredService<IPulseMeterAppSettingsStore>().Save(new PulseMeterAppSettings());
            provider.GetRequiredService<IPulseMeterWindowStateStore>()
                .Save(new PulseMeterWindowState(true, 960, 720));
            provider.GetRequiredService<IResetCreditStateStore>()
                .Save(new ResetCreditTrackerState(false, 1, []));
            provider.GetRequiredService<IRunwayObservationStateStore>()
                .Save(new RunwayObservationState(RunwayObservationStateStore.CurrentSchemaVersion, []));
            provider.GetRequiredService<IClipboardService>().SetText("must not reach Windows clipboard");

            Assert.Equal("unchanged", File.ReadAllText(sentinel));
            Assert.False(Directory.Exists(Path.Combine(fakeLocalAppData, "PulseMeter")));
            Assert.True(File.Exists(paths.AppSettingsPath));
            Assert.True(File.Exists(paths.WindowStatePath));
            Assert.True(File.Exists(paths.ResetCreditStatePath));
            Assert.True(File.Exists(paths.RunwayObservationsPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", previousLocalAppData);
        }
    }

    [Fact]
    public void HarnessProject_IsIncludedAndCompositionSourceAvoidsForbiddenRegistrations()
    {
        var startDirectory = Environment.GetEnvironmentVariable("PULSEMETER_REPO_ROOT")
            ?? AppContext.BaseDirectory;
        var workspace = VisualHarnessWorkspace.LocateFrom(startDirectory).WorkspaceRoot;
        var solution = File.ReadAllText(Path.Combine(workspace, "PulseMeter.slnx"));
        var composition = File.ReadAllText(
            Path.Combine(workspace, "tools", "PulseMeter.VisualHarness", "VisualHarnessComposition.cs"));
        var project = File.ReadAllText(
            Path.Combine(workspace, "tools", "PulseMeter.VisualHarness", "PulseMeter.VisualHarness.csproj"));
        var app = File.ReadAllText(
            Path.Combine(workspace, "tools", "PulseMeter.VisualHarness", "App.xaml.cs"));

        Assert.Contains("tools/PulseMeter.VisualHarness/PulseMeter.VisualHarness.csproj", solution);
        Assert.Contains("Link=\"Assets\\PulseMeter.ico\"", project);
        Assert.Contains("Link=\"Assets\\PulseMeterLogo.png\"", project);
        Assert.DoesNotContain("AddUsageCollection", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("new CodexUsageService", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("IUsageService, CodexUsageService", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("CodexResetCreditService", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectUsageService", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("UsageAttributionService", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("CodexExecutableResolver", composition, StringComparison.Ordinal);
        Assert.Contains("window.IsVisibleChanged += Window_IsVisibleChanged", app, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _shutdownRequested, 1)", app, StringComparison.Ordinal);
    }

    private string CreateWorkspace()
    {
        var workspace = Directory.CreateDirectory(
            Path.Combine(_testRoot, Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(workspace, ".git"));
        File.WriteAllText(Path.Combine(workspace, "PulseMeter.slnx"), "<Solution />");
        return workspace;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
