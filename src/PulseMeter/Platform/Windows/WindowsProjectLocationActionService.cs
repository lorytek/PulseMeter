using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PulseMeter.Slices.ProjectUsage.Business;

namespace PulseMeter.Platform.Windows;

/// <summary>Starts an approved Windows shell program without accepting commands or executable names from callers.</summary>
internal interface IProjectLocationProcessStarter
{
    IProjectLocationStartedProcess? Start(ProcessStartInfo startInfo);

    bool TryStartWindowsPowerShellInNewConsole(string validatedWorkingDirectory);
}

internal interface IProjectLocationStartedProcess : IDisposable
{
}

/// <summary>Provides the filesystem facts required to validate a project folder at action time.</summary>
internal interface IProjectLocationFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    bool IsFixedLocalDrive(string volumeRoot);

    bool TryGetDirectoryAttributes(string path, out FileAttributes attributes);
}

public sealed class WindowsProjectLocationActionService : IProjectLocationActionService
{
    private const string UnknownProjectSentinel = "(unknown project)";
    private readonly IProjectLocationFileSystem _fileSystem;
    private readonly IProjectLocationProcessStarter _processStarter;

    public WindowsProjectLocationActionService()
        : this(new WindowsProjectLocationFileSystem(), new WindowsProjectLocationProcessStarter())
    {
    }

    internal WindowsProjectLocationActionService(
        IProjectLocationFileSystem fileSystem,
        IProjectLocationProcessStarter processStarter)
    {
        _fileSystem = fileSystem;
        _processStarter = processStarter;
    }

    public ProjectLocationActionResult OpenFolder(string? observedPath)
    {
        if (!TryValidate(observedPath, out var validatedPath))
        {
            return ProjectLocationActionResult.InvalidLocation;
        }

        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = explorerPath,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(validatedPath);

        return TryStart(startInfo);
    }

    public ProjectLocationActionResult OpenWindowsPowerShell(string? observedPath)
    {
        if (!TryValidate(observedPath, out var validatedPath))
        {
            return ProjectLocationActionResult.InvalidLocation;
        }

        var powerShellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        try
        {
            if (!_fileSystem.FileExists(powerShellPath))
            {
                return ProjectLocationActionResult.LauncherUnavailable;
            }
        }
        catch (Exception)
        {
            return ProjectLocationActionResult.LauncherUnavailable;
        }

        return TryStartWindowsPowerShellInNewConsole(validatedPath);
    }

    private ProjectLocationActionResult TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = _processStarter.Start(startInfo);
            return process is null
                ? ProjectLocationActionResult.LaunchFailed
                : ProjectLocationActionResult.Succeeded;
        }
        catch (Exception)
        {
            return ProjectLocationActionResult.LaunchFailed;
        }
    }

    private ProjectLocationActionResult TryStartWindowsPowerShellInNewConsole(string validatedWorkingDirectory)
    {
        try
        {
            return _processStarter.TryStartWindowsPowerShellInNewConsole(validatedWorkingDirectory)
                ? ProjectLocationActionResult.Succeeded
                : ProjectLocationActionResult.LaunchFailed;
        }
        catch (Exception)
        {
            return ProjectLocationActionResult.LaunchFailed;
        }
    }

    private bool TryValidate(string? observedPath, out string validatedPath)
    {
        validatedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(observedPath)
            || observedPath.Trim().Equals(UnknownProjectSentinel, StringComparison.OrdinalIgnoreCase)
            || !IsPlainDriveAbsolutePath(observedPath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(observedPath);
            var volumeRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(volumeRoot)
                || fullPath.Equals(volumeRoot, StringComparison.OrdinalIgnoreCase)
                || !_fileSystem.IsFixedLocalDrive(volumeRoot)
                || !_fileSystem.DirectoryExists(fullPath)
                || HasReparsePointInPath(fullPath, volumeRoot))
            {
                return false;
            }

            validatedPath = fullPath;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool HasReparsePointInPath(string fullPath, string volumeRoot)
    {
        var current = volumeRoot;
        if (IsReparsePoint(current))
        {
            return true;
        }

        var remainingPath = fullPath[volumeRoot.Length..];
        foreach (var segment in remainingPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsReparsePoint(string path)
    {
        return !_fileSystem.TryGetDirectoryAttributes(path, out var attributes)
            || (attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static bool IsPlainDriveAbsolutePath(string path)
    {
        if (path.Length < 4
            || !((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'))
            || path[1] != ':'
            || path[2] != Path.DirectorySeparatorChar
            || path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.StartsWith(@"\??\", StringComparison.Ordinal)
            || path.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase)
            || path.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}

internal sealed class WindowsProjectLocationFileSystem : IProjectLocationFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public bool IsFixedLocalDrive(string volumeRoot)
    {
        try
        {
            var drive = new DriveInfo(volumeRoot);
            return drive.IsReady && drive.DriveType == DriveType.Fixed;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryGetDirectoryAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (IOException)
        {
            attributes = default;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            attributes = default;
            return false;
        }
    }
}

internal interface IWindowsProjectLocationNativeApi
{
    bool CreateProcess(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref WindowsProjectLocationStartupInfo startupInfo,
        out WindowsProjectLocationProcessInformation processInformation);

    bool CloseHandle(IntPtr handle);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WindowsProjectLocationStartupInfo
{
    public uint cb;
    public string? lpReserved;
    public string? lpDesktop;
    public string? lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public ushort wShowWindow;
    public ushort cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsProjectLocationProcessInformation
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
}

internal sealed class WindowsProjectLocationProcessStarter : IProjectLocationProcessStarter
{
    private const uint CreateNewConsole = 0x00000010;
    private readonly IWindowsProjectLocationNativeApi _nativeApi;

    public WindowsProjectLocationProcessStarter()
        : this(new WindowsProjectLocationNativeApi())
    {
    }

    internal WindowsProjectLocationProcessStarter(IWindowsProjectLocationNativeApi nativeApi)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
    }

    public IProjectLocationStartedProcess? Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        return process is null ? null : new StartedProcess(process);
    }

    public bool TryStartWindowsPowerShellInNewConsole(string validatedWorkingDirectory)
    {
        var applicationPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var startupInfo = new WindowsProjectLocationStartupInfo
        {
            cb = (uint)Marshal.SizeOf<WindowsProjectLocationStartupInfo>()
        };
        var processInformation = default(WindowsProjectLocationProcessInformation);

        try
        {
            return _nativeApi.CreateProcess(
                applicationPath,
                new StringBuilder($"\"{applicationPath}\""),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CreateNewConsole,
                IntPtr.Zero,
                validatedWorkingDirectory,
                ref startupInfo,
                out processInformation);
        }
        finally
        {
            if (processInformation.hThread != IntPtr.Zero)
            {
                _nativeApi.CloseHandle(processInformation.hThread);
            }

            if (processInformation.hProcess != IntPtr.Zero)
            {
                _nativeApi.CloseHandle(processInformation.hProcess);
            }
        }
    }

    private sealed class StartedProcess : IProjectLocationStartedProcess
    {
        private readonly Process _process;

        public StartedProcess(Process process)
        {
            _process = process;
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}

internal sealed class WindowsProjectLocationNativeApi : IWindowsProjectLocationNativeApi
{
    public bool CreateProcess(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref WindowsProjectLocationStartupInfo startupInfo,
        out WindowsProjectLocationProcessInformation processInformation)
    {
        return CreateProcessW(
            applicationName,
            commandLine,
            processAttributes,
            threadAttributes,
            inheritHandles,
            creationFlags,
            environment,
            currentDirectory,
            ref startupInfo,
            out processInformation);
    }

    public bool CloseHandle(IntPtr handle) => CloseHandleNative(handle);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        [MarshalAs(UnmanagedType.LPWStr)] string applicationName,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        [MarshalAs(UnmanagedType.LPWStr)] string currentDirectory,
        ref WindowsProjectLocationStartupInfo startupInfo,
        out WindowsProjectLocationProcessInformation processInformation);

    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandleNative(IntPtr handle);
}
