using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ProjectUsage.Business;
using PulseMeter.Slices.ProjectUsage.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

[Collection(UsageTrendWpfCollection.Name)]
public sealed class ProjectLocationUiTests
{
    [Fact]
    public void SectionViewModel_OnlyOpensLocationFromExplicitCommandAndSnapshotsTheSelectedRow()
    {
        var locationPresenter = new RecordingLocationPresenter();
        var viewModel = new ProjectUsageSectionViewModel(new ProjectUsagePresenter(), locationPresenter);

        viewModel.ApplyRows([Project("PulseMeter", @"C:\Projects\PulseMeter"), Project("Unknown", "")]);
        viewModel.SelectedProjectRow = viewModel.ProjectUsageRows.Single(row => row.DisplayName == "Unknown");
        Assert.False(viewModel.CanShowProjectLocation);
        Assert.False(viewModel.ProjectLocationCommand.CanExecute(null));
        Assert.Empty(locationPresenter.Calls);

        viewModel.SelectedProjectRow = viewModel.ProjectUsageRows.Single(row => row.DisplayName == "PulseMeter");
        Assert.True(viewModel.CanShowProjectLocation);
        viewModel.ProjectLocationCommand.Execute(null);

        Assert.Equal([("PulseMeter", @"C:\Projects\PulseMeter")], locationPresenter.Calls);
        viewModel.ApplyRows([Project("PulseMeter", @"C:\Projects\PulseMeter")]);
        Assert.Single(locationPresenter.Calls);
    }

    [Fact]
    public void LocationViewModel_CancelKeepsObservedCandidate_OverrideIsWindowOnly_AndReopenStartsObservedAgain()
    {
        var service = new RecordingActionService();
        var viewModel = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", service);

        viewModel.ApplyBrowsedFolder(null);
        Assert.Equal(@"C:\Observed", viewModel.CandidatePath);
        Assert.False(viewModel.HasWindowOnlyChoice);

        viewModel.ApplyBrowsedFolder(@"D:\Override");
        Assert.Equal(@"D:\Override", viewModel.CandidatePath);
        Assert.True(viewModel.HasWindowOnlyChoice);
        Assert.Equal("Chosen for this window only — not saved", viewModel.ChoiceStatusText);
        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal([@"D:\Override"], service.OpenFolderPaths);
        Assert.Equal("Folder-open request sent.", viewModel.Feedback);

        viewModel.OpenCodexCommand.Execute(null);
        Assert.Equal([@"D:\Override"], service.OpenCodexPaths);
        Assert.Equal("Codex launch requested.", viewModel.Feedback);

        viewModel.OpenWindowsPowerShellCommand.Execute(null);
        Assert.Equal("Windows PowerShell launch requested.", viewModel.Feedback);

        var reopened = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", service);
        Assert.Equal(@"C:\Observed", reopened.CandidatePath);
        Assert.False(reopened.HasWindowOnlyChoice);
    }

    [Theory]
    [InlineData(ProjectLocationActionResult.InvalidLocation, "This folder is unavailable.")]
    [InlineData(ProjectLocationActionResult.LauncherUnavailable, "Windows PowerShell is unavailable")]
    [InlineData(ProjectLocationActionResult.LaunchFailed, "Could not start Windows PowerShell.")]
    public void LocationViewModel_UsesBoundedActionFeedback(ProjectLocationActionResult result, string expected)
    {
        var service = new RecordingActionService { Result = result };
        var viewModel = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", service);

        viewModel.OpenWindowsPowerShellCommand.Execute(null);

        Assert.Contains(expected, viewModel.Feedback);
        Assert.DoesNotContain(@"C:\Observed", viewModel.Feedback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocationViewModel_UsesCodexSpecificFailureFeedbackWithoutExposingThePath()
    {
        var service = new RecordingActionService { Result = ProjectLocationActionResult.LaunchFailed };
        var viewModel = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", service);

        viewModel.OpenCodexCommand.Execute(null);

        Assert.Equal("Could not open Codex. Make sure Codex is installed and try again.", viewModel.Feedback);
        Assert.DoesNotContain(@"C:\Observed", viewModel.Feedback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocationViewModel_BlocksReentrantActionClick()
    {
        var service = new RecordingActionService();
        var viewModel = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", service);
        service.OnOpenFolder = () => viewModel.OpenFolderCommand.Execute(null);

        viewModel.OpenFolderCommand.Execute(null);

        Assert.Single(service.OpenFolderPaths);
    }

    [Fact]
    public void Presenter_AssignsOwnerReusesVisibleDialogAndRetriesAfterSetupFailure()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var owner = new Window();
                var actionService = new RecordingActionService();
                var picker = new RecordingFolderPicker();
                var factoryCalls = 0;
                FakeLocationDialog? dialog = null;
                ProjectLocationPresenter? presenter = null;
                presenter = new ProjectLocationPresenter(
                    actionService,
                    picker,
                    () => owner,
                    (viewModel, _) =>
                    {
                        factoryCalls++;
                        dialog = new FakeLocationDialog(() => presenter!.ShowLocation("Other", @"C:\Other"), viewModel);
                        return dialog;
                    });

                presenter.ShowLocation("PulseMeter", @"C:\Observed");
                Assert.Equal(1, factoryCalls);
                Assert.Same(owner, dialog!.Owner);
                Assert.Equal(1, dialog.ActivateCount);
                Assert.Equal("PulseMeter", dialog.ViewModel.ProjectDisplayName);
                Assert.Equal(@"C:\Observed", dialog.ViewModel.ObservedPath);

                var failOwnerAssignment = true;
                var retryCalls = 0;
                var retryPresenter = new ProjectLocationPresenter(
                    actionService,
                    picker,
                    () => owner,
                    (_, _) => { retryCalls++; return new FakeLocationDialog(() => { }, new ProjectLocationViewModel("PulseMeter", @"C:\Observed", actionService)); },
                    (candidate, ownerWindow) =>
                    {
                        if (failOwnerAssignment)
                        {
                            failOwnerAssignment = false;
                            throw new InvalidOperationException("setup failure");
                        }

                        candidate.Owner = ownerWindow;
                    });
                Assert.Throws<InvalidOperationException>(() => retryPresenter.ShowLocation("PulseMeter", @"C:\Observed"));
                retryPresenter.ShowLocation("PulseMeter", @"C:\Observed");
                Assert.Equal(2, retryCalls);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TestTimeouts.UiThread), "The project location presenter test did not finish.");
        if (threadFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void ProjectLocationModal_XamlIsAccessibleAndAvoidsUnboundedClaims()
    {
        var root = TestWorkspace.FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "ProjectUsage", "UI", "ProjectLocationWindow.xaml"));

        Assert.Contains("Title=\"Project location\"", xaml);
        Assert.Contains("Text=\"Observed path\"", xaml);
        Assert.Contains("PulseMeter attributed activity to this path. It may not be the repository root or the worktree you intend.", xaml);
        Assert.Contains("Chosen for this window only", File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "ProjectUsage", "UI", "ProjectLocationViewModel.cs")));
        Assert.Contains("Content=\"Open folder\"", xaml);
        Assert.Contains("Content=\"Open in Codex\"", xaml);
        Assert.Contains("Command=\"{Binding OpenCodexCommand}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Open project folder in Codex\"", xaml);
        Assert.Contains("Open in Codex starts a new task with this folder as its workspace.", xaml);
        Assert.Contains("Content=\"Open PowerShell\"", xaml);
        Assert.Contains("PulseMeter supplies no commands, and your normal PowerShell profile may run.", xaml);
        Assert.Contains("IsCancel=\"True\"", xaml);
        Assert.DoesNotContain("verified", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worktree ready", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, xaml.Split("MaxHeight=\"112\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("VerticalScrollBarVisibility=\"Auto\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ProjectUsageSection_ShowsStyledOpenSelectedProjectAction()
    {
        var root = TestWorkspace.FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "ProjectUsage", "UI", "ProjectUsageSection.xaml"));

        Assert.Contains("x:Key=\"OpenSelectedProjectButtonStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"#1F73FF\" />", xaml);
        Assert.Contains("Text=\"Open selected project\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Open selected project\"", xaml);
        Assert.Contains("<Trigger Property=\"IsEnabled\" Value=\"False\">", xaml);
        Assert.DoesNotContain("Project location…", xaml);
    }

    [Fact]
    public void ProjectLocationWindow_ConstructsOnSta()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new ProjectLocationWindow(
                    new ProjectLocationViewModel("PulseMeter", @"C:\Observed", new RecordingActionService()),
                    new RecordingFolderPicker());
                Assert.Equal("Project location", window.Title);
                window.Close();
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TestTimeouts.UiThread), "The project location window construction test did not finish.");
        if (threadFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void ProjectLocationWindow_ReportsPickerFailureWithoutChangingTheCandidate()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new ProjectLocationViewModel("PulseMeter", @"C:\Observed", new RecordingActionService());
                var window = new ProjectLocationWindow(viewModel, new ThrowingFolderPicker());
                window.ChooseFolder();

                Assert.Equal(@"C:\Observed", viewModel.CandidatePath);
                Assert.Equal("The folder picker could not open. Try again.", viewModel.Feedback);
                Assert.DoesNotContain(@"C:\Observed", viewModel.Feedback, StringComparison.OrdinalIgnoreCase);
                window.Close();
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TestTimeouts.UiThread), "The picker failure test did not finish.");
        if (threadFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    private static ProjectUsageRow Project(string displayName, string fullPath) => new(
        displayName, fullPath, 1_000, 1_000, 1, 50,
        EstimatedLast7Days: 1_000, EstimatedPrevious7Days: 500, ActiveDaysLast7: 1, SpikeDays: 0);

    private sealed class RecordingLocationPresenter : IProjectLocationPresenter
    {
        public List<(string DisplayName, string Path)> Calls { get; } = [];
        public void ShowLocation(string projectDisplayName, string observedPath) => Calls.Add((projectDisplayName, observedPath));
    }

    private sealed class RecordingActionService : IProjectLocationActionService
    {
        public List<string?> OpenFolderPaths { get; } = [];
        public List<string?> OpenCodexPaths { get; } = [];
        public ProjectLocationActionResult Result { get; set; } = ProjectLocationActionResult.Succeeded;
        public Action? OnOpenFolder { get; set; }
        public ProjectLocationActionResult OpenFolder(string? observedPath) { OpenFolderPaths.Add(observedPath); OnOpenFolder?.Invoke(); return Result; }
        public ProjectLocationActionResult OpenCodex(string? observedPath) { OpenCodexPaths.Add(observedPath); return Result; }
        public ProjectLocationActionResult OpenWindowsPowerShell(string? observedPath) => Result;
    }

    private sealed class RecordingFolderPicker : IProjectFolderPicker
    {
        public string? PickFolder(Window owner) => null;
    }

    private sealed class ThrowingFolderPicker : IProjectFolderPicker
    {
        public string? PickFolder(Window owner) => throw new InvalidOperationException(@"C:\private\picker");
    }

    private sealed class FakeLocationDialog(Action onShow, ProjectLocationViewModel viewModel) : IProjectLocationDialog
    {
        public event EventHandler? Closed;
        public Window? Owner { get; set; }
        public bool IsVisible { get; private set; }
        public int ActivateCount { get; private set; }
        public ProjectLocationViewModel ViewModel { get; } = viewModel;
        public bool Activate() { ActivateCount++; return true; }
        public bool? ShowDialog() { IsVisible = true; onShow(); return true; }
        public void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
    }
}
