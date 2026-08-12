using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class QuickAccessWindowControllerTests
{
    [Theory]
    [InlineData(false, System.Windows.WindowState.Normal)]
    [InlineData(true, System.Windows.WindowState.Minimized)]
    public void ToggleQuickAccess_ShowsAndActivatesHiddenOrMinimizedWindow(bool isVisible, System.Windows.WindowState windowState)
    {
        var window = new TestWindow { IsVisible = isVisible, WindowState = windowState };
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        viewModel.MarkHiddenByUser();

        new QuickAccessWindowController(window, viewModel).ToggleQuickAccess();

        Assert.False(viewModel.IsHiddenByUser);
        Assert.Equal(1, window.ShowAndActivateCount);
    }

    [Fact]
    public void ToggleQuickAccess_ActivatesVisibleInactiveWindowWithoutChangingVisibility()
    {
        var window = new TestWindow { IsVisible = true, IsActive = false };
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        new QuickAccessWindowController(window, viewModel).ToggleQuickAccess();

        Assert.Equal(1, window.ActivateCount);
        Assert.Equal(0, window.HideCount);
        Assert.False(viewModel.IsHiddenByUser);
    }

    [Fact]
    public void ToggleQuickAccess_HidesVisibleActiveWindowAndMarksItUserHidden()
    {
        var window = new TestWindow { IsVisible = true, IsActive = true };
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        new QuickAccessWindowController(window, viewModel).ToggleQuickAccess();

        Assert.Equal(1, window.HideCount);
        Assert.True(viewModel.IsHiddenByUser);
    }

    private sealed class TestWindow : IPulseMeterWindow
    {
        public IntPtr Handle => IntPtr.Zero;

        public bool IsVisible { get; set; }

        public bool IsActive { get; set; }

        public System.Windows.WindowState WindowState { get; set; }

        public int ShowAndActivateCount { get; private set; }

        public int ActivateCount { get; private set; }

        public int HideCount { get; private set; }

        public void Invoke(Action action) => action();

        public void Show() => IsVisible = true;

        public void ShowWithoutActivation() => IsVisible = true;

        public void ShowAndActivate()
        {
            ShowAndActivateCount++;
            IsVisible = true;
            IsActive = true;
        }

        public void Hide()
        {
            HideCount++;
            IsVisible = false;
        }

        public void CloseForShutdown() => IsVisible = false;

        public bool Activate()
        {
            ActivateCount++;
            IsActive = true;
            return true;
        }
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new UsageSnapshot());
    }
}
