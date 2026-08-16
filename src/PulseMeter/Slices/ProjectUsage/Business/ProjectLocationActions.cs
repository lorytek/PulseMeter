namespace PulseMeter.Slices.ProjectUsage.Business;

/// <summary>Starts a narrowly-scoped action for an already-observed project folder.</summary>
public interface IProjectLocationActionService
{
    ProjectLocationActionResult OpenFolder(string? observedPath);

    ProjectLocationActionResult OpenCodex(string? observedPath);

    ProjectLocationActionResult OpenWindowsPowerShell(string? observedPath);
}

/// <summary>Bounded outcome suitable for user-facing action feedback.</summary>
public enum ProjectLocationActionResult
{
    Succeeded,
    InvalidLocation,
    LauncherUnavailable,
    LaunchFailed
}
