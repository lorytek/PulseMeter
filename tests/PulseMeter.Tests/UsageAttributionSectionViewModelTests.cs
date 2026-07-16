namespace PulseMeter.Tests;

public sealed class UsageAttributionSectionViewModelTests
{
    [Fact]
    public void ApplySnapshot_UsesProjectUsageRowsWhenAvailable()
    {
        var viewModel = new UsageAttributionSectionViewModel(new UsageAttributionPresenter());
        var projectRows = new[]
        {
            new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 100, 250, 1, 40, ActiveDaysLast7: 1),
            new ProjectUsageRow("Docs", @"C:\Projects\Docs", 50, 250, 0, 20)
        };

        viewModel.ApplySnapshot(UsageAttributionSnapshot.Empty, DateTimeOffset.UtcNow, projectRows);

        var rows = viewModel.ProjectRows;
        Assert.True(viewModel.HasAttribution);
        Assert.Equal(["PulseMeter", "Docs"], rows.Select(row => row.DisplayName));
        Assert.Equal("1 active day in the last 7 days", rows[0].ActivityText);
        Assert.Equal("No activity in the last 7 days", rows[1].ActivityText);
    }

    [Fact]
    public void ApplySnapshot_AggregatesRawSessionsByProjectWithoutProjectUsageRows()
    {
        var snapshot = new UsageAttributionSnapshot
        {
            Sessions =
            [
                Session("PulseMeter", @"C:\Projects\PulseMeter", 600, 60),
                Session("PulseMeter", @"C:\Projects\PulseMeter", 300, 30),
                Session("Docs", @"C:\Projects\Docs", 100, 10)
            ],
            EstimatedAttributedTokens = 1_000
        };
        var viewModel = new UsageAttributionSectionViewModel(new UsageAttributionPresenter());

        viewModel.ApplySnapshot(snapshot, DateTimeOffset.UtcNow);

        Assert.True(viewModel.HasAttribution);
        var project = Assert.Single(viewModel.ProjectRows, row => row.DisplayName == "PulseMeter");
        Assert.Equal("900", project.EstimatedTokensText);
        Assert.Equal("90%", project.ShareText);
        Assert.Equal("Local project activity", project.ActivityText);
    }

    private static UsageAttributionSessionRow Session(string projectName, string projectPath, long estimatedTokens, double sharePercent)
    {
        return new UsageAttributionSessionRow(
            $"{projectName} chat",
            Guid.NewGuid().ToString("N"),
            projectName,
            projectPath,
            estimatedTokens,
            estimatedTokens,
            sharePercent,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
