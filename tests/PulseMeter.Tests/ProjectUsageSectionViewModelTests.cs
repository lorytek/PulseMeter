using PulseMeter.Slices.ProjectUsage.Business;
using PulseMeter.Slices.ProjectUsage.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

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
        Assert.Equal("PulseMeter", viewModel.SelectedProjectTitle);
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
}
