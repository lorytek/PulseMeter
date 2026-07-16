using System.Windows;

namespace PulseMeter.Slices.PulseMeterWindow.UI;

public interface IPulseMeterWindow
{
    IntPtr Handle { get; }

    bool IsVisible { get; }

    WindowState WindowState { get; set; }

    void Invoke(Action action);

    void Show();

    void Hide();

    bool Activate();
}
