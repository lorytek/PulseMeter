namespace PulseMeter.Tests;

public sealed class PublishScriptTests
{
    [Fact]
    public void LocalShortcut_UsesTheDotnetHostForUnsignedLocalBuilds()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("$localHostOutput = Join-Path $artifactsRoot \"PulseMeter-local-host-$timestamp\"", script);
        Assert.Contains("$localHostDll = Join-Path $localHostOutput \"PulseMeter.dll\"", script);
        Assert.Contains("$dotnetExe = Join-Path $env:ProgramFiles \"dotnet\\dotnet.exe\"", script);
        Assert.Contains("$shortcut.TargetPath = $dotnetExe", script);
        Assert.Contains("$shortcut.Arguments = [string]::Concat('\"', $localHostDll, '\"')", script);
        Assert.Contains("$shortcut.WorkingDirectory = $localHostOutput", script);
        Assert.DoesNotContain("launch-pulsemeter.vbs", script);
        Assert.DoesNotContain("wscript.exe", script);
        Assert.DoesNotContain("CreateObject(\"WScript.Shell\")", script);
    }

    [Fact]
    public void LocalPublish_UsesPulseMeterBrandingForArtifactsAndShortcut()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("src\\PulseMeter\\PulseMeter.csproj", script);
        Assert.Contains("\"artifacts\"", script);
        Assert.Contains("PulseMeter-win-x64-$timestamp", script);
        Assert.Contains("PulseMeter.exe", script);
        Assert.Contains("PulseMeter.ico", script);
        Assert.Contains("PulseMeter.lnk", script);
        Assert.Contains("GetFolderPath(\"Desktop\")", script);
        Assert.Contains("Save-PulseMeterShortcut $desktopShortcutPath", script);
        Assert.DoesNotContain(string.Concat("Codex", "Usage", (char)72, "ud"), script);
        Assert.DoesNotContain(string.Concat("Codex ", "Usage ", (char)72, (char)85, (char)68, ".lnk"), script);
    }

    [Fact]
    public void LocalPublish_CreatesTimestampedSelfContainedArtifactForShortcut()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("$timestamp = Get-Date -Format \"yyyyMMdd-HHmmss\"", script);
        Assert.Contains("--self-contained true", script);
        Assert.Contains("/p:UseAppHost=true", script);
        Assert.Contains("$appExe = Join-Path $output \"PulseMeter.exe\"", script);
        Assert.Contains("$icon = Join-Path $root \"src\\PulseMeter\\Assets\\PulseMeter.ico\"", script);
        Assert.Contains("/p:PublishSingleFile=true", script);
        Assert.Contains("/p:IncludeNativeLibrariesForSelfExtract=true", script);
        Assert.Contains("/p:EnableCompressionInSingleFile=true", script);
        Assert.Contains("Published local self-contained app", script);
        Assert.Contains("--self-contained false", script);
        Assert.Contains("/p:UseAppHost=false", script);
        Assert.Contains("Published local Smart App Control-compatible launcher", script);
    }

    [Fact]
    public void PublishLocal_UpdatesExistingPinnedTaskbarShortcut()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("User Pinned\\TaskBar\\PulseMeter.lnk", script);
        Assert.Contains("if (Test-Path -LiteralPath $taskbarShortcutPath)", script);
        Assert.Contains("Save-PulseMeterShortcut $taskbarShortcutPath", script);
        Assert.Contains("PulseMeter is not pinned to the taskbar", script);
    }

    [Fact]
    public void PulseMeterProject_IsWindowsExeSoDirectShortcutDoesNotOpenConsole()
    {
        var project = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "PulseMeter.csproj"));

        Assert.Contains("<OutputType>WinExe</OutputType>", project);
        Assert.Contains("<UseWPF>true</UseWPF>", project);
    }

    [Fact]
    public void LocalTestScript_RunsTestsFromLocalAppDataAndSetsRepoRoot()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "test-local.ps1"));

        Assert.Contains("PULSEMETER_REPO_ROOT", script);
        Assert.Contains("LOCALAPPDATA", script);
        Assert.Contains("--artifacts-path", script);
        Assert.Contains("dotnet test", script);
    }

    private static string FindWorkspaceFile(params string[] segments)
    {
        return TestWorkspace.FindFile(segments);
    }
}
