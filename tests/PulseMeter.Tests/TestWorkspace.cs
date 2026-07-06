namespace PulseMeter.Tests;

internal static class TestWorkspace
{
    private const string RepoRootVariable = "PULSEMETER_REPO_ROOT";

    public static string FindFile(params string[] segments)
    {
        var root = FindRoot();
        var candidate = Path.Combine([root, .. segments]);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(segments)} from workspace root {root}.");
    }

    public static string FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable(RepoRootVariable);
        if (IsWorkspaceRoot(configured))
        {
            return Path.GetFullPath(configured!);
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (IsWorkspaceRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find PulseMeter workspace root. Set {RepoRootVariable} to the directory containing PulseMeter.slnx.");
    }

    private static bool IsWorkspaceRoot(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && File.Exists(Path.Combine(path, "PulseMeter.slnx"))
            && Directory.Exists(Path.Combine(path, "src", "PulseMeter"));
    }
}
