using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ProjectUsage.Business;

namespace PulseMeter.Tests;

[Collection("ProjectLocationEnvironment")]
public sealed class WindowsProjectLocationActionServiceTests
{
    private const string ProjectPath = "C:\\Projects\\Caf\u00E9 & spaces\\release (2026)";

    [Fact]
    public void OpenFolder_UsesOnlyTheValidatedPathAsOneExplorerArgument()
    {
        var fileSystem = new FakeFileSystem();
        var starter = new RecordingProcessStarter();
        var service = new WindowsProjectLocationActionService(fileSystem, starter);

        var result = service.OpenFolder(ProjectPath);

        Assert.Equal(ProjectLocationActionResult.Succeeded, result);
        var startInfo = Assert.Single(starter.StartInfos);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(string.Empty, startInfo.WorkingDirectory);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(new[] { ProjectPath }, startInfo.ArgumentList);
        Assert.True(starter.LastStartedProcess!.Disposed);
    }

    [Fact]
    public void OpenWindowsPowerShell_UsesDedicatedNewConsoleWithExactWorkingDirectory()
    {
        var fileSystem = new FakeFileSystem();
        var starter = new RecordingProcessStarter();
        var service = new WindowsProjectLocationActionService(fileSystem, starter);

        var result = service.OpenWindowsPowerShell(ProjectPath);

        Assert.Equal(ProjectLocationActionResult.Succeeded, result);
        Assert.Empty(starter.StartInfos);
        var request = Assert.Single(starter.NewConsoleStartRequests);
        Assert.Equal(ProjectPath, request.ValidatedWorkingDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(unknown project)")]
    [InlineData("relative\\folder")]
    [InlineData(@"C:drive-relative")]
    [InlineData(@"\\server\share\project")]
    [InlineData("file:///C:/Projects/project")]
    [InlineData(@"\\.\C:\Projects\project")]
    [InlineData(@"\\?\C:\Projects\project")]
    [InlineData(@"C:\")]
    public void Actions_RejectEachNonLocalOrNonProjectPathClass(string? observedPath)
    {
        var starter = new RecordingProcessStarter();
        var service = new WindowsProjectLocationActionService(new FakeFileSystem(), starter);

        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenFolder(observedPath));
        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenWindowsPowerShell(observedPath));
        Assert.Empty(starter.StartInfos);
    }

    [Fact]
    public void Actions_RejectMappedRemovableOrMissingFolders()
    {
        var starter = new RecordingProcessStarter();
        var fileSystem = new FakeFileSystem { IsFixedDrive = false };
        var service = new WindowsProjectLocationActionService(fileSystem, starter);

        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenFolder(ProjectPath));

        fileSystem.IsFixedDrive = true;
        fileSystem.MissingDirectories.Add(ProjectPath);

        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenWindowsPowerShell(ProjectPath));
        Assert.Empty(starter.StartInfos);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Projects")]
    [InlineData(ProjectPath)]
    public void Actions_RejectReparsePointsInTheDriveRootAncestorOrLeaf(string reparsePoint)
    {
        var starter = new RecordingProcessStarter();
        var fileSystem = new FakeFileSystem();
        fileSystem.ReparseDirectories.Add(reparsePoint);
        var service = new WindowsProjectLocationActionService(fileSystem, starter);

        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenFolder(ProjectPath));
        Assert.Empty(starter.StartInfos);
    }

    [Fact]
    public void EveryAction_RevalidatesTheFolderToRejectStaleDeletionAndNewReparsePoints()
    {
        var starter = new RecordingProcessStarter();
        var fileSystem = new FakeFileSystem();
        var service = new WindowsProjectLocationActionService(fileSystem, starter);

        Assert.Equal(ProjectLocationActionResult.Succeeded, service.OpenFolder(ProjectPath));

        fileSystem.MissingDirectories.Add(ProjectPath);
        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenFolder(ProjectPath));

        fileSystem.MissingDirectories.Remove(ProjectPath);
        fileSystem.ReparseDirectories.Add(@"C:\Projects");
        Assert.Equal(ProjectLocationActionResult.InvalidLocation, service.OpenWindowsPowerShell(ProjectPath));
        Assert.Single(starter.StartInfos);
    }

    [Fact]
    public void LaunchConfiguration_DoesNotUsePoisonedPathCurrentDirectoryOrComSpec()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalComSpec = Environment.GetEnvironmentVariable("ComSpec");
        var originalCurrentDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.SetEnvironmentVariable("PATH", @"Z:\poisoned");
            Environment.SetEnvironmentVariable("ComSpec", @"Z:\poisoned\cmd.exe");
            Environment.CurrentDirectory = Path.GetTempPath();

            var fileSystem = new FakeFileSystem();
            var starter = new RecordingProcessStarter();
            var service = new WindowsProjectLocationActionService(fileSystem, starter);

            Assert.Equal(ProjectLocationActionResult.Succeeded, service.OpenFolder(ProjectPath));
            Assert.Equal(ProjectLocationActionResult.Succeeded, service.OpenWindowsPowerShell(ProjectPath));

            Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), starter.StartInfos[0].FileName);
            var request = Assert.Single(starter.NewConsoleStartRequests);
            Assert.Equal(ProjectPath, request.ValidatedWorkingDirectory);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("ComSpec", originalComSpec);
        }
    }

    [Fact]
    public void MissingPowerShellAndProcessFailures_ReturnBoundedResultsWithoutLaunchingLiveProcesses()
    {
        var missingBinaryService = new WindowsProjectLocationActionService(
            new FakeFileSystem { PowerShellExists = false },
            new RecordingProcessStarter());
        Assert.Equal(ProjectLocationActionResult.LauncherUnavailable, missingBinaryService.OpenWindowsPowerShell(ProjectPath));

        var nullStarterService = new WindowsProjectLocationActionService(new FakeFileSystem(), new RecordingProcessStarter { ReturnNull = true });
        Assert.Equal(ProjectLocationActionResult.LaunchFailed, nullStarterService.OpenFolder(ProjectPath));

        var throwingStarterService = new WindowsProjectLocationActionService(new FakeFileSystem(), new RecordingProcessStarter { StartException = new InvalidOperationException("test only") });
        Assert.Equal(ProjectLocationActionResult.LaunchFailed, throwingStarterService.OpenFolder(ProjectPath));

        var failedNewConsoleService = new WindowsProjectLocationActionService(new FakeFileSystem(), new RecordingProcessStarter { NewConsoleResult = false });
        Assert.Equal(ProjectLocationActionResult.LaunchFailed, failedNewConsoleService.OpenWindowsPowerShell(ProjectPath));

        var throwingNewConsoleService = new WindowsProjectLocationActionService(new FakeFileSystem(), new RecordingProcessStarter { NewConsoleException = new InvalidOperationException("test only") });
        Assert.Equal(ProjectLocationActionResult.LaunchFailed, throwingNewConsoleService.OpenWindowsPowerShell(ProjectPath));

        var failingFileSystemService = new WindowsProjectLocationActionService(new FakeFileSystem { AttributeException = new IOException("test only") }, new RecordingProcessStarter());
        Assert.Equal(ProjectLocationActionResult.InvalidLocation, failingFileSystemService.OpenFolder(ProjectPath));

        var failedAttributeQueryService = new WindowsProjectLocationActionService(new FakeFileSystem { AttributeQuerySucceeds = false }, new RecordingProcessStarter());
        Assert.Equal(ProjectLocationActionResult.InvalidLocation, failedAttributeQueryService.OpenFolder(ProjectPath));

        var throwingLauncherQueryService = new WindowsProjectLocationActionService(new FakeFileSystem { FileException = new IOException("test only") }, new RecordingProcessStarter());
        Assert.Equal(ProjectLocationActionResult.LauncherUnavailable, throwingLauncherQueryService.OpenWindowsPowerShell(ProjectPath));
    }

    [Fact]
    public void WindowsPowerShellNativeLaunch_UsesFixedArgumentsAndClosesReturnedHandles()
    {
        var nativeApi = new RecordingNativeApi
        {
            ProcessInformation = new WindowsProjectLocationProcessInformation
            {
                hThread = new IntPtr(11),
                hProcess = new IntPtr(12)
            }
        };
        var starter = new WindowsProjectLocationProcessStarter(nativeApi);

        Assert.True(starter.TryStartWindowsPowerShellInNewConsole(ProjectPath));

        var request = Assert.Single(nativeApi.CreateProcessRequests);
        var powerShellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        Assert.Equal(powerShellPath, request.ApplicationName);
        Assert.Equal($"\"{powerShellPath}\"", request.CommandLine);
        Assert.Equal(IntPtr.Zero, request.ProcessAttributes);
        Assert.Equal(IntPtr.Zero, request.ThreadAttributes);
        Assert.False(request.InheritHandles);
        Assert.Equal(0x00000010u, request.CreationFlags);
        Assert.Equal(IntPtr.Zero, request.Environment);
        Assert.Equal(ProjectPath, request.CurrentDirectory);
        Assert.Equal((uint)Marshal.SizeOf<WindowsProjectLocationStartupInfo>(), request.StartupInfo.cb);
        Assert.Equal(0u, request.StartupInfo.dwFlags);
        Assert.Equal(new[] { new IntPtr(11), new IntPtr(12) }, nativeApi.ClosedHandles);
    }

    [Fact]
    public void WindowsPowerShellNativeLaunch_ClosesNoZeroHandlesAndBoundsNativeExceptions()
    {
        var falseNativeApi = new RecordingNativeApi { CreateResult = false };
        var falseStarter = new WindowsProjectLocationProcessStarter(falseNativeApi);

        Assert.False(falseStarter.TryStartWindowsPowerShellInNewConsole(ProjectPath));
        Assert.Empty(falseNativeApi.ClosedHandles);

        var throwingNativeApi = new RecordingNativeApi
        {
            CreateException = new InvalidOperationException("test only"),
            ProcessInformation = new WindowsProjectLocationProcessInformation
            {
                hThread = new IntPtr(21),
                hProcess = new IntPtr(22)
            }
        };
        var service = new WindowsProjectLocationActionService(
            new FakeFileSystem(),
            new WindowsProjectLocationProcessStarter(throwingNativeApi));

        Assert.Equal(ProjectLocationActionResult.LaunchFailed, service.OpenWindowsPowerShell(ProjectPath));
        Assert.Equal(new[] { new IntPtr(21), new IntPtr(22) }, throwingNativeApi.ClosedHandles);
    }

    [Fact]
    public void WindowsPowerShellNativeApi_UsesExplicitUnicodeCreateProcessWBoolMarshalling()
    {
        var method = typeof(WindowsProjectLocationNativeApi).GetMethod("CreateProcessW", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var import = method!.GetCustomAttribute<DllImportAttribute>();
        Assert.NotNull(import);
        Assert.Equal("CreateProcessW", import!.EntryPoint);
        Assert.Equal(CharSet.Unicode, import.CharSet);
        Assert.True(import.ExactSpelling);
        Assert.True(import.SetLastError);
        Assert.Equal(UnmanagedType.Bool, method.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()!.Value);
        Assert.Equal(UnmanagedType.Bool, method.GetParameters()[4].GetCustomAttribute<MarshalAsAttribute>()!.Value);

        var closeHandleMethod = typeof(WindowsProjectLocationNativeApi).GetMethod("CloseHandleNative", BindingFlags.NonPublic | BindingFlags.Static);
        var closeHandleImport = closeHandleMethod!.GetCustomAttribute<DllImportAttribute>();
        Assert.Equal("CloseHandle", closeHandleImport!.EntryPoint);
        Assert.True(closeHandleImport.ExactSpelling);
        Assert.True(closeHandleImport.SetLastError);
    }

    [Fact]
    public void FolderPicker_CreatesFreshDialogsUsesTheRequiredOwnerAndPreservesCancelSemantics()
    {
        Exception? testException = null;
        var sta = new Thread(() =>
        {
            try
            {
                var firstDialog = new RecordingFolderDialog { FolderName = ProjectPath, Accepted = true };
                var secondDialog = new RecordingFolderDialog { FolderName = @"C:\stale", Accepted = false };
                var factory = new RecordingFolderDialogFactory(firstDialog, secondDialog);
                var picker = new WindowsProjectFolderPicker(factory);
                var owner = new Window();

                Assert.Throws<ArgumentNullException>(() => picker.PickFolder(null!));
                Assert.Equal(ProjectPath, picker.PickFolder(owner));
                Assert.Null(picker.PickFolder(owner));
                Assert.Equal(2, factory.CreateCount);
                Assert.NotSame(firstDialog, secondDialog);
                Assert.Same(owner, firstDialog.Owner);
                Assert.Same(owner, secondDialog.Owner);
            }
            catch (Exception exception)
            {
                testException = exception;
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        Assert.True(sta.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(testException);
    }

    private sealed class FakeFileSystem : IProjectLocationFileSystem
    {
        public HashSet<string> MissingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ReparseDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsFixedDrive { get; set; } = true;

        public bool PowerShellExists { get; set; } = true;

        public Exception? AttributeException { get; set; }

        public bool AttributeQuerySucceeds { get; set; } = true;

        public Exception? FileException { get; set; }

        public bool DirectoryExists(string path) => !MissingDirectories.Contains(path);

        public bool FileExists(string path)
        {
            if (FileException is not null)
            {
                throw FileException;
            }

            return PowerShellExists;
        }

        public bool IsFixedLocalDrive(string volumeRoot) => IsFixedDrive && volumeRoot.Equals(@"C:\", StringComparison.OrdinalIgnoreCase);

        public bool TryGetDirectoryAttributes(string path, out FileAttributes attributes)
        {
            if (AttributeException is not null)
            {
                throw AttributeException;
            }

            if (!AttributeQuerySucceeds)
            {
                attributes = default;
                return false;
            }

            attributes = ReparseDirectories.Contains(path)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Directory;
            return true;
        }
    }

    private sealed class RecordingProcessStarter : IProjectLocationProcessStarter
    {
        public List<ProcessStartInfo> StartInfos { get; } = [];

        public List<NewConsoleStartRequest> NewConsoleStartRequests { get; } = [];

        public bool ReturnNull { get; set; }

        public Exception? StartException { get; set; }

        public bool NewConsoleResult { get; set; } = true;

        public Exception? NewConsoleException { get; set; }

        public RecordingStartedProcess? LastStartedProcess { get; private set; }

        public IProjectLocationStartedProcess? Start(ProcessStartInfo startInfo)
        {
            StartInfos.Add(startInfo);
            if (StartException is not null)
            {
                throw StartException;
            }

            if (ReturnNull)
            {
                return null;
            }

            LastStartedProcess = new RecordingStartedProcess();
            return LastStartedProcess;
        }

        public bool TryStartWindowsPowerShellInNewConsole(string validatedWorkingDirectory)
        {
            NewConsoleStartRequests.Add(new NewConsoleStartRequest(validatedWorkingDirectory));
            if (NewConsoleException is not null)
            {
                throw NewConsoleException;
            }

            return NewConsoleResult;
        }
    }

    private sealed record NewConsoleStartRequest(string ValidatedWorkingDirectory);

    private sealed class RecordingNativeApi : IWindowsProjectLocationNativeApi
    {
        public List<CreateProcessRequest> CreateProcessRequests { get; } = [];

        public List<IntPtr> ClosedHandles { get; } = [];

        public bool CreateResult { get; set; } = true;

        public Exception? CreateException { get; set; }

        public WindowsProjectLocationProcessInformation ProcessInformation { get; set; }

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
            CreateProcessRequests.Add(new CreateProcessRequest(
                applicationName,
                commandLine.ToString(),
                processAttributes,
                threadAttributes,
                inheritHandles,
                creationFlags,
                environment,
                currentDirectory,
                startupInfo));
            processInformation = ProcessInformation;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            return CreateResult;
        }

        public bool CloseHandle(IntPtr handle)
        {
            ClosedHandles.Add(handle);
            return true;
        }
    }

    private sealed record CreateProcessRequest(
        string ApplicationName,
        string CommandLine,
        IntPtr ProcessAttributes,
        IntPtr ThreadAttributes,
        bool InheritHandles,
        uint CreationFlags,
        IntPtr Environment,
        string CurrentDirectory,
        WindowsProjectLocationStartupInfo StartupInfo);

    private sealed class RecordingStartedProcess : IProjectLocationStartedProcess
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class RecordingFolderDialog : IWindowsProjectFolderDialog
    {
        public string FolderName { get; set; } = string.Empty;

        public bool? Accepted { get; set; }

        public Window? Owner { get; private set; }

        public bool? ShowDialog(Window owner)
        {
            Owner = owner;
            return Accepted;
        }
    }

    private sealed class RecordingFolderDialogFactory : IWindowsProjectFolderDialogFactory
    {
        private readonly Queue<IWindowsProjectFolderDialog> _dialogs;

        public RecordingFolderDialogFactory(params IWindowsProjectFolderDialog[] dialogs)
        {
            _dialogs = new Queue<IWindowsProjectFolderDialog>(dialogs);
        }

        public int CreateCount { get; private set; }

        public IWindowsProjectFolderDialog Create()
        {
            CreateCount++;
            return _dialogs.Dequeue();
        }
    }
}

[CollectionDefinition("ProjectLocationEnvironment", DisableParallelization = true)]
public sealed class ProjectLocationEnvironmentCollection
{
}
