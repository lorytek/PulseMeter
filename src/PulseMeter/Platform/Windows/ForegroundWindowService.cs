using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PulseMeter.Platform.Windows;

public interface IForegroundWindowService
{
    bool IsCodexForeground();
}

public sealed class ForegroundWindowService : IForegroundWindowService
{
    public bool IsCodexForeground()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(handle, out var processId);

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName.Contains("codex", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        var title = GetWindowTitle(handle);
        return title.Contains("codex", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var builder = new StringBuilder(256);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
}
