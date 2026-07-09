using System.Runtime.InteropServices;

namespace PulseMeter.Platform.Windows;

public interface IUserIdleTimeProvider
{
    TimeSpan GetIdleTime();
}

public sealed class UserIdleTimeProvider : IUserIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var elapsedMs = GetTickCount64() - info.Time;
        return TimeSpan.FromMilliseconds(elapsedMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;

        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
