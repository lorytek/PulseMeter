using PulseMeter.Slices.ProjectUsage.Business;
using PulseMeter.Slices.ProjectUsage.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

[Collection(UsageTrendWpfCollection.Name)]
public sealed class ProjectUsageSectionViewModelTests
{
    [Fact]
    public void ApplyRows_SelectsMostRecentlyActiveProjectAndShowsHealthEvidence()
    {
        var viewModel = new ProjectUsageSectionViewModel(new ProjectUsagePresenter());

        viewModel.ApplyRows(
        [
            new ProjectUsageRow(
                "PulseMeter", @"C:\Projects\PulseMeter", 1_000_000, 1_000_000, 3, 60,
                EstimatedLast7Days: 600_000,
                EstimatedPrevious7Days: 200_000,
                ActiveDaysLast7: 4,
                SpikeDays: 2,
                LeadingChatDisplayName: "PulseMeter chat - 07 Jul 10:15",
                LeadingChatEstimatedTokens: 400_000,
                LargestBurnMomentChatDisplayName: "PulseMeter chat - 07 Jul 10:15",
                LargestBurnMomentEstimatedTokens: 210_000,
                LargestBurnMomentAtUtc: new DateTimeOffset(2026, 7, 7, 10, 15, 0, TimeSpan.Zero)),
            new ProjectUsageRow(
                "Docs", @"C:\Projects\Docs", 500_000, 500_000, 2, 40,
                EstimatedLast7Days: 100_000,
                EstimatedPrevious7Days: 320_000,
                ActiveDaysLast7: 2,
                SpikeDays: 0)
        ]);

        Assert.True(viewModel.HasProjectUsage);
        Assert.True(viewModel.HasSelectedProject);
        Assert.Equal("PulseMeter", viewModel.SelectedProjectTitle);
        Assert.Equal(@"C:\Projects\PulseMeter", viewModel.SelectedProjectPathText);
        Assert.Contains("600.0K", viewModel.SelectedProjectSummary);
        Assert.Contains("4 active days", viewModel.SelectedProjectSummary);
        Assert.Contains("PulseMeter", viewModel.SelectedProjectChatsText);
        Assert.Contains("Largest moment", viewModel.SelectedProjectMomentText);
        Assert.Contains("PulseMeter +400.0K", viewModel.LargestIncreaseText);
        Assert.Contains("Docs -220.0K", viewModel.LargestDropText);
        Assert.Equal("PulseMeter", viewModel.LargestIncreaseProjectText);
        Assert.Equal("+400.0K", viewModel.LargestIncreaseValueText);
        Assert.Equal("Docs", viewModel.LargestDropProjectText);
        Assert.Equal("-220.0K", viewModel.LargestDropValueText);
    }

    [Fact]
    public void ApplyRows_WithNoProjects_ClearsSelectedProjectIdentity()
    {
        var viewModel = new ProjectUsageSectionViewModel(new ProjectUsagePresenter());

        viewModel.ApplyRows([]);

        Assert.False(viewModel.HasSelectedProject);
        Assert.Equal("Select a project", viewModel.SelectedProjectTitle);
        Assert.Equal(string.Empty, viewModel.SelectedProjectPathText);
    }

    [Fact]
    public void BoundProjectList_DoesNotPublishNullSelectionDuringRefresh()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            System.Windows.Window? window = null;
            try
            {
                var viewModel = new ProjectUsageSectionViewModel(new ProjectUsagePresenter());
                viewModel.ApplyRows(
                [
                    Project("PulseMeter", @"C:\Projects\PulseMeter", 600_000),
                    Project("Docs", @"C:\Projects\Docs", 100_000)
                ]);
                viewModel.SelectedProjectRow = viewModel.ProjectUsageRows.Single(row => row.DisplayName == "Docs");

                var section = new PulseMeter.Slices.ProjectUsage.UI.ProjectUsageSection
                {
                    DataContext = viewModel
                };
                window = new System.Windows.Window
                {
                    Content = section,
                    Width = 900,
                    Height = 600,
                    Left = -20_000,
                    Top = -20_000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };
                window.Show();
                section.UpdateLayout();

                var selections = new List<ProjectUsageDisplayRow?>();
                viewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ProjectUsageSectionViewModel.SelectedProjectRow))
                    {
                        selections.Add(viewModel.SelectedProjectRow);
                    }
                };

                viewModel.ApplyRows(
                [
                    Project("PulseMeter", @"C:\Projects\PulseMeter", 650_000),
                    Project("Docs", @"C:\Projects\Docs", 125_000)
                ]);

                Assert.NotEmpty(selections);
                Assert.DoesNotContain(null, selections);
                Assert.Equal("Docs", viewModel.SelectedProjectRow?.DisplayName);
                Assert.Equal(125_000, viewModel.SelectedProjectRow?.EstimatedLast7Days);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(8)), "The bound project-list refresh test did not finish.");
        if (threadFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    private static ProjectUsageRow Project(string name, string path, long estimatedLast7Days) =>
        new(
            name,
            path,
            estimatedLast7Days,
            estimatedLast7Days,
            1,
            50,
            EstimatedLast7Days: estimatedLast7Days,
            EstimatedPrevious7Days: estimatedLast7Days / 2,
            ActiveDaysLast7: 2,
            SpikeDays: 0);
}
