namespace PulseMeter.Tests;

public sealed class PublishScriptTests
{
    [Fact]
    public void LocalShortcut_UsesHiddenLauncherInsteadOfDotnetConsoleHost()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("launch-pulsemeter.vbs", script);
        Assert.Contains("$shortcut.TargetPath = $wscript", script);
        Assert.Contains("$shortcut.Arguments = \"`\"$launcher`\"\"", script);
        Assert.DoesNotContain("$shortcut.TargetPath = $dotnet", script);
    }

    [Fact]
    public void LocalPublish_UsesPulseMeterBrandingForArtifactsAndShortcut()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("src\\PulseMeter\\PulseMeter.csproj", script);
        Assert.Contains("\"artifacts\"", script);
        Assert.Contains("\"PulseMeter-win-x64\"", script);
        Assert.Contains("PulseMeter.exe", script);
        Assert.Contains("PulseMeter.lnk", script);
        Assert.Contains("GetFolderPath(\"Desktop\")", script);
        Assert.Contains("Save-PulseMeterShortcut $desktopShortcutPath", script);
        Assert.DoesNotContain(string.Concat("Codex", "Usage", (char)72, "ud"), script);
        Assert.DoesNotContain(string.Concat("Codex ", "Usage ", (char)72, (char)85, (char)68, ".lnk"), script);
    }

    [Fact]
    public void LocalPublish_UsesSingleFileExecutableToAvoidSidecarDllPolicyBlocks()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("--self-contained true", script);
        Assert.Contains("/p:PublishSingleFile=true", script);
        Assert.Contains("/p:IncludeNativeLibrariesForSelfExtract=true", script);
        Assert.Contains("/p:EnableCompressionInSingleFile=true", script);
        Assert.DoesNotContain("$dll = Join-Path", script);
        Assert.DoesNotContain("Published framework-dependent app", script);
    }

    [Fact]
    public void HiddenLauncher_StartsDotnetWithoutShowingAConsoleWindow()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "publish-local.ps1"));

        Assert.Contains("Set shell = CreateObject(\"WScript.Shell\")", script);
        Assert.Contains("shell.Run commandLine, 0, False", script);
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
