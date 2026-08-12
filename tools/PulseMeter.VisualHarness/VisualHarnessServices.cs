using PulseMeter.Platform.Windows;
using PulseMeter.Slices.SupportSnapshot.Business;
using PulseMeter.Slices.SupportSnapshot.Models;
using PulseMeter.Slices.ProjectUsage.Business;
using System.Windows;

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

public sealed class VisualHarnessDesktopProcessSnapshotService : ICodexDesktopProcessSnapshotService
{
    private readonly DesktopProcessSnapshotVisualScenario _scenario;

    public VisualHarnessDesktopProcessSnapshotService(DesktopProcessSnapshotVisualScenario scenario)
    {
        _scenario = scenario;
    }

    public int CaptureCount { get; private set; }

    public Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CaptureCount++;
        var status = _scenario == DesktopProcessSnapshotVisualScenario.Partial
            ? CodexDesktopProcessSnapshotStatus.Partial
            : CodexDesktopProcessSnapshotStatus.Complete;
        var snapshot = new CodexDesktopProcessSnapshot(
            CodexDesktopProcessSnapshot.CurrentSchemaVersion,
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
            status,
            status == CodexDesktopProcessSnapshotStatus.Partial ? 2 : 3,
            status == CodexDesktopProcessSnapshotStatus.Partial ? 1_610_612_736 : 2_684_354_560,
            true,
            status == CodexDesktopProcessSnapshotStatus.Partial ? 1 : 0,
            CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);
        return Task.FromResult(snapshot);
    }
}

public sealed class VisualHarnessTrayIconService : ITrayIconService
{
    public void Dispose()
    {
    }
}

public sealed class VisualHarnessProjectLocationActionService : IProjectLocationActionService
{
    private readonly ProjectLocationVisualScenario _scenario;
    public VisualHarnessProjectLocationActionService(ProjectLocationVisualScenario scenario) => _scenario = scenario;
    public int OpenFolderCount { get; private set; }
    public int OpenPowerShellCount { get; private set; }
    public ProjectLocationActionResult OpenFolder(string? observedPath) { OpenFolderCount++; return Result; }
    public ProjectLocationActionResult OpenWindowsPowerShell(string? observedPath) { OpenPowerShellCount++; return Result; }
    private ProjectLocationActionResult Result => _scenario switch
    {
        ProjectLocationVisualScenario.InvalidLocation => ProjectLocationActionResult.InvalidLocation,
        ProjectLocationVisualScenario.LaunchFailed => ProjectLocationActionResult.LaunchFailed,
        _ => ProjectLocationActionResult.Succeeded
    };
}

public sealed class VisualHarnessProjectFolderPicker : IProjectFolderPicker
{
    public int PickCount { get; private set; }
    public string? PickFolder(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        PickCount++;
        return @"D:\VisualHarness\window-only\override";
    }
}
