namespace PulseMeter.Tests;

public sealed class DiagnosticsPrivacyTests
{
    [Fact]
    public void Diagnostics_AreCentralizedAndNeverLogRawPayloadsOrExceptionText()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src", "PulseMeter");
        var diagnosticsPath = Path.Combine(
            sourceRoot,
            "Platform",
            "Diagnostics",
            "PrivacySafeDiagnostics.cs");
        var diagnosticsSource = File.ReadAllText(diagnosticsPath);

        var directDebugWriters = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Equals(diagnosticsPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("Debug.WriteLine", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(directDebugWriters);
        Assert.Contains("GetBaseException().GetType().Name", diagnosticsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.Message", diagnosticsSource, StringComparison.OrdinalIgnoreCase);

        var usageService = File.ReadAllText(Path.Combine(
            sourceRoot,
            "Slices",
            "UsageCollection",
            "Business",
            "CodexUsageService.cs"));
        var appServerProcess = File.ReadAllText(Path.Combine(
            sourceRoot,
            "Platform",
            "Codex",
            "AppServerProcess.cs"));
        Assert.DoesNotContain("rateLimits.GetRawText()", usageService, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug.WriteLine(\"[app-server] \" + args.Data)", appServerProcess, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PulseMeter.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PulseMeter repository root.");
    }
}
