using System.IO;

namespace PulseMeter.VisualHarness;

public sealed class VisualHarnessPaths
{
    internal VisualHarnessPaths(string workspaceRoot, string stateRoot)
    {
        WorkspaceRoot = workspaceRoot;
        StateRoot = stateRoot;
    }

    public string WorkspaceRoot { get; }

    public string StateRoot { get; }

    public string AppSettingsPath => Path.Combine(StateRoot, "settings.json");

    public string WindowStatePath => Path.Combine(StateRoot, "window-state.json");

    public string ResetCreditStatePath => Path.Combine(StateRoot, "reset-credits.json");

    public string RunwayObservationsPath => Path.Combine(StateRoot, "runway-observations.json");
}

public static class VisualHarnessWorkspace
{
    private static readonly string ExpectedStateRelativePath = Path.Combine("artifacts", "visual-harness", "state");

    public static VisualHarnessPaths LocateFrom(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (HasWorkspaceMarkers(current.FullName))
            {
                return ValidateRoot(current.FullName);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "PulseMeter visual harness requires a worktree containing PulseMeter.slnx and .git.");
    }

    public static VisualHarnessPaths ValidateRoot(
        string candidateRoot,
        Func<string, FileAttributes>? attributesReader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRoot);

        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidateRoot));
        if (!Directory.Exists(canonicalRoot) || !HasWorkspaceMarkers(canonicalRoot))
        {
            throw new InvalidOperationException(
                "PulseMeter visual harness root must be an existing worktree containing PulseMeter.slnx and .git.");
        }

        var readAttributes = attributesReader ?? File.GetAttributes;
        RejectReparsePointComponents(canonicalRoot, readAttributes);

        var stateRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine(canonicalRoot, ExpectedStateRelativePath)));
        EnsureExactStateRoot(canonicalRoot, stateRoot);
        RejectReparsePointComponents(stateRoot, readAttributes);

        return new VisualHarnessPaths(canonicalRoot, stateRoot);
    }

    internal static void CreateStateRoot(VisualHarnessPaths paths)
    {
        EnsureExactStateRoot(paths.WorkspaceRoot, paths.StateRoot);
        RejectReparsePointComponents(paths.WorkspaceRoot, File.GetAttributes);

        var current = paths.WorkspaceRoot;
        foreach (var segment in ExpectedStateRelativePath.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            Directory.CreateDirectory(current);
            RejectReparsePoint(current, File.GetAttributes);
        }

        RejectReparsePointComponents(paths.StateRoot, File.GetAttributes);
    }

    private static bool HasWorkspaceMarkers(string directory)
    {
        return File.Exists(Path.Combine(directory, "PulseMeter.slnx"))
            && (File.Exists(Path.Combine(directory, ".git")) || Directory.Exists(Path.Combine(directory, ".git")));
    }

    private static void EnsureExactStateRoot(string workspaceRoot, string stateRoot)
    {
        var expected = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine(workspaceRoot, ExpectedStateRelativePath)));
        if (!string.Equals(expected, stateRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PulseMeter visual harness state must remain under the validated worktree.");
        }

        var relative = Path.GetRelativePath(workspaceRoot, stateRoot);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PulseMeter visual harness state escaped the validated worktree.");
        }
    }

    private static void RejectReparsePointComponents(
        string path,
        Func<string, FileAttributes> attributesReader)
    {
        var canonicalPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(canonicalPath)
            ?? throw new InvalidOperationException("PulseMeter visual harness path has no filesystem root.");
        var current = pathRoot;
        var remainder = canonicalPath[pathRoot.Length..];

        foreach (var segment in remainder.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
            {
                break;
            }

            RejectReparsePoint(current, attributesReader);
        }
    }

    private static void RejectReparsePoint(
        string path,
        Func<string, FileAttributes> attributesReader)
    {
        if ((attributesReader(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"PulseMeter visual harness rejects reparse-point path components: {path}");
        }
    }
}
