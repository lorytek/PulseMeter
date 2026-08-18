using System.Windows;

namespace PulseMeter.Slices.PulseMeterWindow.UI;

public interface IPulseMeterWindow
{
    IntPtr Handle { get; }

    bool IsVisible { get; }

    bool IsActive => false;

    bool IsMinimizedByUser => false;

    WindowState WindowState { get; set; }

    void Invoke(Action action);

    void Show();

    void ShowWithoutActivation();

    void ShowAndActivate();

    void Hide();

    void CloseForShutdown();

    bool Activate();

    void SetWindowMessageHandler(Func<int, IntPtr, bool>? handler)
    {
    }

    void SetWindowClosedHandler(Action? handler)
    {
    }
}
