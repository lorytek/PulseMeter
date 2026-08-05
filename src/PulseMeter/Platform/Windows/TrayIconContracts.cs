namespace PulseMeter.Platform.Windows;

public interface ITrayIconService : IDisposable
{
    void ShowNotification(string title, string message)
    {
    }
}
