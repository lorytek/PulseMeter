using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class UsageProviderSwitchViewModelTests
{
    [Fact]
    public void SwitchCommand_TogglesProviderAndUpdatesButtonText()
    {
        var router = new UsageProviderRouter(new SourceUsageService("Codex"), new SourceUsageService("Claude"));
        var viewModel = new PulseMeterWindowViewModel(router);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(viewModel.CanSwitchUsageProvider);
        Assert.Equal("Switch to Claude Code", viewModel.SwitchUsageProviderText);
        Assert.True(viewModel.SwitchUsageProviderCommand.CanExecute(null));

        viewModel.SwitchUsageProviderCommand.Execute(null);

        Assert.Equal(UsageProvider.Claude, viewModel.UsageProvider);
        Assert.Equal(UsageProvider.Claude, router.Provider);
        Assert.Equal("Switch to Codex", viewModel.SwitchUsageProviderText);
        Assert.StartsWith("Claude Code · ", viewModel.CompactTitleText);
        Assert.Contains(nameof(PulseMeterWindowViewModel.UsageProvider), changed);
        Assert.Contains(nameof(PulseMeterWindowViewModel.SwitchUsageProviderText), changed);

        viewModel.SwitchUsageProviderCommand.Execute(null);

        Assert.Equal(UsageProvider.Codex, router.Provider);
        Assert.Equal("Switch to Claude Code", viewModel.SwitchUsageProviderText);
    }

    [Fact]
    public void SwitchCommand_IsDisabledWhenUsageServiceCannotSwitch()
    {
        var viewModel = new PulseMeterWindowViewModel(new SourceUsageService("Codex"));

        Assert.False(viewModel.CanSwitchUsageProvider);
        Assert.False(viewModel.SwitchUsageProviderCommand.CanExecute(null));
    }

    private sealed class SourceUsageService(string source) : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new UsageSnapshot { Source = source, SyncStatus = SyncStatus.Live });
    }
}
