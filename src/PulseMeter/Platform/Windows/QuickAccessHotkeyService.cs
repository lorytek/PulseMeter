using System.ComponentModel;
using System.Runtime.InteropServices;
using PulseMeter.Slices.PulseMeterWindow.Business;
using PulseMeter.Slices.PulseMeterWindow.UI;

namespace PulseMeter.Platform.Windows;

public enum HotkeyRegistrationResult
{
    Registered,
    Unavailable
}

public interface IHotkeyRegistrationPlatform
{
    HotkeyRegistrationResult Register(IntPtr windowHandle, int hotkeyId, uint modifiers, uint virtualKey);

    void Unregister(IntPtr windowHandle, int hotkeyId);
}

public interface IQuickAccessHotkeyService : IDisposable
{
    void Start();

    void HandleWindowClosed();
}

public sealed class QuickAccessHotkeyService : IQuickAccessHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x504D;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyP = 0x50;
    private const string RegistrationFailureTitle = "Quick access unavailable";
    private const string RegistrationFailureMessage = "The system-wide quick-access shortcut could not be enabled. It may already be in use.";

    private readonly IPulseMeterWindow _pulseMeterWindow;
    private readonly PulseMeterWindowViewModel _viewModel;
    private readonly IQuickAccessWindowController _quickAccessWindowController;
    private readonly IHotkeyRegistrationPlatform _platform;
    private readonly ITrayIconService _trayIconService;
    private bool _started;
    private bool _registered;
    private bool _disposed;

    public QuickAccessHotkeyService(
        IPulseMeterWindow pulseMeterWindow,
        PulseMeterWindowViewModel viewModel,
        IQuickAccessWindowController quickAccessWindowController,
        IHotkeyRegistrationPlatform platform,
        ITrayIconService trayIconService)
    {
        _pulseMeterWindow = pulseMeterWindow;
        _viewModel = viewModel;
        _quickAccessWindowController = quickAccessWindowController;
        _platform = platform;
        _trayIconService = trayIconService;
    }

    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _pulseMeterWindow.SetWindowMessageHandler(HandleWindowMessage);
        _pulseMeterWindow.SetWindowClosedHandler(HandleWindowClosed);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateRegistration();
    }

    public void HandleWindowClosed()
    {
        Unregister();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _pulseMeterWindow.SetWindowMessageHandler(null);
        _pulseMeterWindow.SetWindowClosedHandler(null);
        Unregister();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PulseMeterWindowViewModel.IsQuickAccessHotkeyRequested))
        {
            UpdateRegistration();
        }
    }

    private void UpdateRegistration()
    {
        if (_disposed || !_started)
        {
            return;
        }

        if (!_viewModel.IsQuickAccessHotkeyRequested)
        {
            Unregister();
            return;
        }

        if (_registered || _pulseMeterWindow.Handle == IntPtr.Zero)
        {
            return;
        }

        if (_platform.Register(
                _pulseMeterWindow.Handle,
                HotkeyId,
                ModControl | ModAlt | ModShift | ModNoRepeat,
                VirtualKeyP) == HotkeyRegistrationResult.Registered)
        {
            _registered = true;
            return;
        }

        _trayIconService.ShowNotification(RegistrationFailureTitle, RegistrationFailureMessage);
    }

    private bool HandleWindowMessage(int message, IntPtr wParam)
    {
        if (_disposed
            || !_registered
            || message != WmHotkey
            || wParam.ToInt64() != HotkeyId)
        {
            return false;
        }

        _quickAccessWindowController.ToggleQuickAccess();
        return true;
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        _registered = false;
        _platform.Unregister(_pulseMeterWindow.Handle, HotkeyId);
    }
}

public sealed class Win32HotkeyRegistrationPlatform : IHotkeyRegistrationPlatform
{
    public HotkeyRegistrationResult Register(IntPtr windowHandle, int hotkeyId, uint modifiers, uint virtualKey)
    {
        return RegisterHotKey(windowHandle, hotkeyId, modifiers, virtualKey)
            ? HotkeyRegistrationResult.Registered
            : HotkeyRegistrationResult.Unavailable;
    }

    public void Unregister(IntPtr windowHandle, int hotkeyId)
    {
        _ = UnregisterHotKey(windowHandle, hotkeyId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
