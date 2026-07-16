namespace PulseMeter.Tests;

public sealed class ReleasePackagingTests
{
    private static readonly string OldFreewarePhrase = string.Concat("proprietary ", "freeware");
    private static readonly string OldNotOpenSourcePhrase = string.Concat("not open ", "source");

    [Fact]
    public void PublicReleasePackageScript_CreatesZipWithoutDesktopShortcutSideEffects()
    {
        var script = File.ReadAllText(FindWorkspaceFile("scripts", "package-release.ps1"));

        Assert.Contains("dotnet test", script);
        Assert.Contains("dotnet publish", script);
        Assert.Contains("PulseMeter-win-x64", script);
        Assert.Contains("Compress-Archive", script);
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("$zipPath.sha256", script);
        Assert.Contains("PulseMeter-$version-win-x64-portable.zip", script);
        Assert.Contains("/p:PublishSingleFile=true", script);
        Assert.Contains("/p:IncludeNativeLibrariesForSelfExtract=true", script);
        Assert.Contains("/p:EnableCompressionInSingleFile=true", script);
        Assert.Contains("INSTALL.txt", script);
        Assert.Contains("LICENSE", script);
        Assert.Contains("RELEASE_NOTES.md", script);
        Assert.Contains("RELEASE_NOTES_v$version.md", script);
        Assert.Contains("$assetsSource = Join-Path $root \"assets\"", script);
        Assert.Contains("Copy-Item -LiteralPath $assetsSource -Destination (Join-Path $output \"assets\") -Recurse -Force", script);
        Assert.Contains("Apache License 2.0", script);
        Assert.Contains("PulseMeter is open source under the Apache License 2.0.", script);
        Assert.Contains("See LICENSE in this folder for the full Apache-2.0 terms.", script);
        Assert.DoesNotContain(OldFreewarePhrase, script);
        Assert.DoesNotContain(OldNotOpenSourcePhrase, script);
        Assert.DoesNotContain("may not be copied, modified, reused, or redistributed", script);
        Assert.DoesNotContain("AGPL-3.0-only", script);
        Assert.Contains("Windows 10 or Windows 11, 64-bit.", script);
        Assert.Contains("No .NET install required", script);
        Assert.Contains("Unsigned app notice:", script);
        Assert.Contains("unknown-publisher or SmartScreen warning", script);
        Assert.Contains("%LOCALAPPDATA%\\PulseMeter", script);
        Assert.DoesNotContain("GetFolderPath(\"Desktop\")", script);
        Assert.DoesNotContain("CreateShortcut", script);
    }

    [Fact]
    public void PublicLicense_UsesApacheTwoPointZeroOpenSourceTerms()
    {
        var license = File.ReadAllText(FindWorkspaceFile("LICENSE"));
        var readme = File.ReadAllText(FindWorkspaceFile("README.md"));
        var appProject = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "PulseMeter.csproj"));

        Assert.Contains("Apache License", license);
        Assert.Contains("Version 2.0, January 2004", license);
        Assert.Contains("http://www.apache.org/licenses/", license);
        Assert.Contains("Apache License 2.0", readme);
        Assert.Contains("commercial use, modification, redistribution, and private use", readme);
        Assert.Contains("<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>", appProject);
        Assert.Contains("<RepositoryUrl>https://github.com/lorytek/PulseMeter</RepositoryUrl>", appProject);
        Assert.DoesNotContain("AGPL", license);
        Assert.DoesNotContain("AGPL", readme);
        Assert.DoesNotContain(OldFreewarePhrase, license);
        Assert.DoesNotContain(OldFreewarePhrase, readme);
        Assert.DoesNotContain("does not make the PulseMeter source code open source", license);
    }

    [Fact]
    public void PublicRepositoryMetadata_ContainsContributorAndSecurityGuidance()
    {
        var contributing = File.ReadAllText(FindWorkspaceFile("CONTRIBUTING.md"));
        var pullRequestTemplate = File.ReadAllText(FindWorkspaceFile(".github", "PULL_REQUEST_TEMPLATE.md"));
        var bugTemplate = File.ReadAllText(FindWorkspaceFile(".github", "ISSUE_TEMPLATE", "bug_report.md"));
        var featureTemplate = File.ReadAllText(FindWorkspaceFile(".github", "ISSUE_TEMPLATE", "feature_request.md"));
        var codeOwners = File.ReadAllText(FindWorkspaceFile(".github", "CODEOWNERS"));
        var codeOfConduct = File.ReadAllText(FindWorkspaceFile("CODE_OF_CONDUCT.md"));
        var dependabot = File.ReadAllText(FindWorkspaceFile(".github", "dependabot.yml"));
        var securityWorkflow = File.ReadAllText(FindWorkspaceFile(".github", "workflows", "security.yml"));
        var gitleaks = File.ReadAllText(FindWorkspaceFile(".gitleaks.toml"));
        var llms = File.ReadAllText(FindWorkspaceFile("llms.txt"));

        Assert.Contains("Small bug fixes and documentation fixes can open a pull request directly.", contributing);
        Assert.Contains("Real Behavior Proof", pullRequestTemplate);
        Assert.Contains("PulseMeter version", bugTemplate);
        Assert.Contains("Windows version", bugTemplate);
        Assert.Contains("Codex CLI status", bugTemplate);
        Assert.Contains("Problem Statement", featureTemplate);
        Assert.Contains("* @lorytek", codeOwners);
        Assert.Contains("Contributor Covenant Code of Conduct", codeOfConduct);
        Assert.Contains("package-ecosystem: \"github-actions\"", dependabot);
        Assert.Contains("package-ecosystem: \"nuget\"", dependabot);
        Assert.Contains("github/codeql-action/init", securityWorkflow);
        Assert.Contains("gitleaks detect", securityWorkflow);
        Assert.Contains("test-access-token", gitleaks);
        Assert.Contains("# PulseMeter", llms);
        Assert.Contains("No telemetry", llms);
    }

    [Fact]
    public void PublicReadme_IsUserFacingAndDoesNotIncludeDevelopmentCommands()
    {
        var readme = File.ReadAllText(FindWorkspaceFile("README.md"));

        Assert.Contains("## Quick Start", readme);
        Assert.Contains("## Minimum Requirements", readme);
        Assert.Contains("## Uninstall", readme);
        Assert.DoesNotContain("## Development", readme);
        Assert.DoesNotContain("dotnet run", readme);
        Assert.DoesNotContain("dotnet test", readme);
        Assert.DoesNotContain("package-release.ps1", readme);
        Assert.DoesNotContain("publish-local.ps1", readme);
        Assert.DoesNotContain("Release preparation notes", readme);
    }

    [Fact]
    public void PublicReadme_ListsBurnAnalysisAndAttentionSignalsAsPrivacySafeFeatures()
    {
        var readme = File.ReadAllText(FindWorkspaceFile("README.md"));
        var privacy = File.ReadAllText(FindWorkspaceFile("PRIVACY.md"));

        Assert.Contains("Burn Analysis", readme);
        Assert.Contains("Limit Runway", readme);
        Assert.Contains("Idle Drain Detector", readme);
        Assert.Contains("Needs Attention", readme);
        Assert.Contains("automatic alert signals", readme);
        Assert.Contains("local estimates and diagnostics, not billing-exact claims", readme);
        Assert.Contains("It does not parse or display Codex message text", readme);
        Assert.Contains("Idle Drain alerts do not read prompt text or Codex message content", readme);
        Assert.Contains("Automatic alert signals use local usage and rate-limit numbers", readme);
        Assert.Contains("Burn Analysis groups local session token metadata by project and does not display chat titles or prompt text.", privacy);
        Assert.Contains("Project paths and thread IDs remain local attribution metadata", privacy);
        Assert.Contains("do not require prompt text or Codex message content", privacy);
    }

    [Fact]
    public void Version020ReleaseNotes_DescribeBurnAnalysisAndAttentionSignals()
    {
        var changelog = File.ReadAllText(FindWorkspaceFile("CHANGELOG.md"));
        var releaseNotes = File.ReadAllText(FindWorkspaceFile("RELEASE_NOTES_v0.2.0.md"));

        Assert.Contains("## 0.2.0", changelog);
        Assert.Contains("Burn Analysis", changelog);
        Assert.Contains("Needs Attention", changelog);
        Assert.Contains("PulseMeter 0.2.0", releaseNotes);
        Assert.Contains("top local chats by estimated token burn", releaseNotes);
        Assert.Contains("Largest burn events", releaseNotes);
        Assert.Contains("It does not parse or render Codex prompt/message bodies.", releaseNotes);
        Assert.Contains("Apache License 2.0", releaseNotes);
    }

    [Fact]
    public void Version040ReleaseDocs_DescribeAnalyticsAndForecastRelease()
    {
        var project = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "PulseMeter.csproj"));
        var packageScript = File.ReadAllText(FindWorkspaceFile("scripts", "package-release.ps1"));
        var checklist = File.ReadAllText(FindWorkspaceFile("RELEASE_CHECKLIST.md"));
        var changelog = File.ReadAllText(FindWorkspaceFile("CHANGELOG.md"));
        var releaseNotes = File.ReadAllText(FindWorkspaceFile("RELEASE_NOTES_v0.4.0.md"));

        Assert.Contains("<Version>0.4.0</Version>", project);
        Assert.Contains("[string]$Version = \"0.4.0\"", packageScript);
        Assert.Contains("PulseMeter-0.4.0-win-x64-portable.zip", checklist);
        Assert.Contains("## 0.4.0", changelog);
        Assert.Contains("PulseMeter 0.4.0", releaseNotes);
        Assert.Contains("Project Health", releaseNotes);
        Assert.Contains("Runway Forecast", releaseNotes);
        Assert.Contains("project-level Burn Analysis", releaseNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Apache License 2.0", releaseNotes);
    }

    private static string FindWorkspaceFile(params string[] segments)
    {
        return TestWorkspace.FindFile(segments);
    }
}
