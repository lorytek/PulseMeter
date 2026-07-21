using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PulseMeter.Platform.Codex;

public interface IAppServerProcess : IDisposable
{
    StreamReader Output { get; }

    StreamWriter Input { get; }

    bool HasExited { get; }
}

public interface IAppServerProcessFactory
{
    IAppServerProcess Start(string? executable = null);
}

public sealed class AppServerLaunchException : Exception
{
    public AppServerLaunchException(Exception? innerException = null)
        : base("The monitored CLI could not be started.", innerException)
    {
    }
}

public sealed class AppServerProcessFactory : IAppServerProcessFactory
{
    public IAppServerProcess Start(string? executable = null)
    {
        return AppServerProcess.Start(executable);
    }
}

public sealed class AppServerProcess : IAppServerProcess
{
    private readonly Process _process;
    private bool _disposed;

    private AppServerProcess(Process process)
    {
        _process = process;
    }

    public StreamReader Output => _process.StandardOutput;

    public StreamWriter Input => _process.StandardInput;

    public bool HasExited => _process.HasExited;

    public static AppServerProcess Start(string? executable = null)
    {
        var resolution = string.IsNullOrWhiteSpace(executable)
            ? CodexExecutableResolver.Resolve()
            : new CodexExecutableResolution(executable, "configured");

        if (resolution is null)
        {
            throw new AppServerLaunchException();
        }

        var startInfo = BuildStartInfo(resolution.ExecutablePath);

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new AppServerLaunchException();

            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    Debug.WriteLine("[app-server] " + args.Data);
                }
            };
            process.BeginErrorReadLine();

            return new AppServerProcess(process);
        }
        catch (Win32Exception ex)
        {
            throw new AppServerLaunchException(ex);
        }
    }

    public static ProcessStartInfo BuildStartInfo(string executable)
    {
        var extension = Path.GetExtension(executable);
        var isCommandScript = extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);

        return new ProcessStartInfo
        {
            FileName = isCommandScript
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : executable,
            Arguments = isCommandScript
                ? $"/d /c \"\"{executable}\" app-server\""
                : "app-server",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }

        _process.Dispose();
    }
}
