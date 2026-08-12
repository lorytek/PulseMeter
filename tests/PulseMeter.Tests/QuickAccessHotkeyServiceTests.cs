using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class QuickAccessHotkeyServiceTests
{
    [Fact]
    public void DefaultViewModel_DoesNotRequestQuickAccessHotkey()
    {
        Assert.False(new PulseMeterWindowViewModel(new StubUsageService()).IsQuickAccessHotkeyRequested);
    }

    [Fact]
    public void RequestedHotkey_RegistersFixedChord_AndUnregistersExactlyOnce()
    {
        var window = new TestWindow();
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), isQuickAccessHotkeyRequested: true);
        var platform = new TestHotkeyPlatform(HotkeyRegistrationResult.Registered);
        var controller = new CountingQuickAccessController();
        var tray = new TestTrayIconService();
        using var service = new QuickAccessHotkeyService(window, viewModel, controller, platform, tray);

        service.Start();
        service.Start();

        Assert.Equal(1, platform.RegisterCount);
        Assert.Equal(0x4007u, platform.Modifiers);
        Assert.Equal(0x50u, platform.VirtualKey);

        Assert.True(window.DispatchMessage(0x0312, new IntPtr(0x504D)));
        Assert.Equal(1, controller.ToggleCount);

        viewModel.IsQuickAccessHotkeyRequested = false;
        service.HandleWindowClosed();
        service.Dispose();

        Assert.Equal(1, platform.UnregisterCount);
        Assert.False(window.DispatchMessage(0x0312, new IntPtr(0x504D)));
        Assert.Equal(1, controller.ToggleCount);
    }

    [Fact]
    public void RegistrationFailure_KeepsRequestedSetting_AndShowsGenericNotification()
    {
        var window = new TestWindow();
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), isQuickAccessHotkeyRequested: true);
        var platform = new TestHotkeyPlatform(HotkeyRegistrationResult.Unavailable);
        var tray = new TestTrayIconService();
        using var service = new QuickAccessHotkeyService(
            window,
            viewModel,
            new CountingQuickAccessController(),
            platform,
            tray);

        service.Start();

        Assert.True(viewModel.IsQuickAccessHotkeyRequested);
        Assert.Equal(1, platform.RegisterCount);
        Assert.Equal(0, platform.UnregisterCount);
        Assert.Equal(("Quick access unavailable", "The system-wide quick-access shortcut could not be enabled. It may already be in use."), Assert.Single(tray.Notifications));
    }

    private sealed class TestWindow : IPulseMeterWindow
    {
        private Func<int, IntPtr, bool>? _messageHandler;

        public IntPtr Handle { get; } = new(42);

        public bool IsVisible { get; private set; }

        public System.Windows.WindowState WindowState { get; set; }

        public void Invoke(Action action) => action();

        public void Show() => IsVisible = true;

        public void ShowWithoutActivation() => IsVisible = true;

        public void ShowAndActivate() => IsVisible = true;

        public void Hide() => IsVisible = false;

        public void CloseForShutdown() => IsVisible = false;

        public bool Activate() => true;

        public void SetWindowMessageHandler(Func<int, IntPtr, bool>? handler) => _messageHandler = handler;

        public bool DispatchMessage(int message, IntPtr wParam) => _messageHandler?.Invoke(message, wParam) == true;
    }

    private sealed class TestHotkeyPlatform(HotkeyRegistrationResult registrationResult) : IHotkeyRegistrationPlatform
    {
        public int RegisterCount { get; private set; }

        public int UnregisterCount { get; private set; }

        public uint Modifiers { get; private set; }

        public uint VirtualKey { get; private set; }

        public HotkeyRegistrationResult Register(IntPtr windowHandle, int hotkeyId, uint modifiers, uint virtualKey)
        {
            RegisterCount++;
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            return registrationResult;
        }

        public void Unregister(IntPtr windowHandle, int hotkeyId) => UnregisterCount++;
    }

    private sealed class CountingQuickAccessController : IQuickAccessWindowController
    {
        public int ToggleCount { get; private set; }

        public void ToggleQuickAccess() => ToggleCount++;
    }

    private sealed class TestTrayIconService : ITrayIconService
    {
        public List<(string Title, string Message)> Notifications { get; } = [];

        public void ShowNotification(string title, string message) => Notifications.Add((title, message));

        public void Dispose()
        {
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
