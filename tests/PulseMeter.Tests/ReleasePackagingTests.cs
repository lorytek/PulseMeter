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
        Assert.Contains("PulseMeter-$version-win-x64-portable.zip", script);
        Assert.Contains("/p:PublishSingleFile=true", script);
        Assert.Contains("/p:IncludeNativeLibrariesForSelfExtract=true", script);
        Assert.Contains("/p:EnableCompressionInSingleFile=true", script);
        Assert.Contains("INSTALL.txt", script);
        Assert.Contains("LICENSE", script);
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

    private static string FindWorkspaceFile(params string[] segments)
    {
        return TestWorkspace.FindFile(segments);
    }
}
