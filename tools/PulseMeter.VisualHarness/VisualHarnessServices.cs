using PulseMeter.Platform.Windows;

namespace PulseMeter.VisualHarness;

public sealed class VisualHarnessForegroundWindowService : IForegroundWindowService
{
    public CodexForegroundState GetCodexForegroundState(IntPtr referenceWindowHandle)
    {
        return new CodexForegroundState(IsCodexForeground: true, IsOnSameMonitor: false);
    }
}

public sealed class VisualHarnessIdleTimeProvider : IUserIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        return TimeSpan.Zero;
    }
}

public sealed class VisualHarnessClipboardService : IClipboardService
{
    public void SetText(string text)
    {
    }
}

public sealed class VisualHarnessTrayIconService : ITrayIconService
{
    public void Dispose()
    {
    }
}
