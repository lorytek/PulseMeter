using System.Windows;

namespace PulseMeter.Platform.Windows;

public interface IClipboardService
{
    void SetText(string text);
}

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        System.Windows.Clipboard.SetText(text);
    }
}
