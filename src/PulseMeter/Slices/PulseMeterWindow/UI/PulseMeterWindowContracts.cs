using System.Windows;

namespace PulseMeter.Slices.PulseMeterWindow.UI;

public interface IPulseMeterWindow
{
    bool IsVisible { get; }

    WindowState WindowState { get; set; }

    void Invoke(Action action);

    void Show();

    void Hide();

    bool Activate();
}
