using System.IO;

namespace PulseMeter.Shared.Projects;

public static class LocalProjectPathNormalizer
{
    private static readonly string[] GeneratedRunDirectoryNames = [".codex-benchmark-runs", ".runs"];

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(unknown project)";
        }

        var normalized = path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)
            ? @"\\" + path[8..]
            : path.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? path[4..]
                : path;

        try
        {
            normalized = Path.GetFullPath(normalized);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "(unknown project)";
        }

        normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (TryResolveCodexWorktreeOwner(normalized, out var worktreeOwner))
        {
            normalized = worktreeOwner;
        }

        var generatedRunIndex = GeneratedRunDirectoryNames
            .Select(directoryName => FindDirectorySegment(normalized, directoryName))
            .Where(index => index > 0)
            .DefaultIfEmpty(-1)
            .Min();
        return generatedRunIndex > 0
            ? normalized[..generatedRunIndex].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : normalized;
    }

    public static bool IsUserProjectPath(string path)
    {
        var normalized = Normalize(path);
        if (normalized == "(unknown project)")
        {
            return false;
        }

        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsPathWithin(normalized, tempRoot))
        {
            return false;
        }

        var root = Path.GetPathRoot(normalized)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        var relativeToRoot = normalized[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var firstDirectory = relativeToRoot.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return !firstDirectory.StartsWith("hrblind", StringComparison.OrdinalIgnoreCase)
            && !firstDirectory.StartsWith("hrdiag", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDisplayName(string path)
    {
        var normalized = Normalize(path);
        if (normalized == "(unknown project)")
        {
            return "Unknown project";
        }

        return Path.GetFileName(normalized) is { Length: > 0 } name
            ? name
            : normalized;
    }

    internal static bool TryGetCodexWorktreeProjectName(string path, out string projectName)
    {
        projectName = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index <= segments.Length - 4; index++)
        {
            if (!segments[index].Equals(".codex", StringComparison.OrdinalIgnoreCase)
                || !segments[index + 1].Equals("worktrees", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(segments[index + 2])
                || string.IsNullOrWhiteSpace(segments[index + 3]))
            {
                continue;
            }

            projectName = segments[index + 3];
            return true;
        }

        return false;
    }

    private static int FindDirectorySegment(string path, string directoryName)
    {
        var marker = $"{Path.DirectorySeparatorChar}{directoryName}{Path.DirectorySeparatorChar}";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return index;
        }

        marker = $"{Path.AltDirectorySeparatorChar}{directoryName}{Path.AltDirectorySeparatorChar}";
        return path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveCodexWorktreeOwner(string path, out string ownerPath)
    {
        ownerPath = string.Empty;
        if (FindDirectorySegment(path, ".codex") < 0
            || FindDirectorySegment(path, "worktrees") < 0)
        {
            return false;
        }

        try
        {
            var directory = new DirectoryInfo(path);
            for (var depth = 0; directory is not null && depth < 16; depth++, directory = directory.Parent)
            {
                var gitFile = Path.Combine(directory.FullName, ".git");
                if (!File.Exists(gitFile))
                {
                    continue;
                }

                var gitDirectory = ResolveGitPath(directory.FullName, File.ReadLines(gitFile).FirstOrDefault(), "gitdir:");
                if (gitDirectory is null)
                {
                    return false;
                }

                var commonDirectoryFile = Path.Combine(gitDirectory, "commondir");
                if (!File.Exists(commonDirectoryFile))
                {
                    return false;
                }

                var commonGitDirectory = ResolveGitPath(gitDirectory, File.ReadLines(commonDirectoryFile).FirstOrDefault());
                if (commonGitDirectory is null
                    || !string.Equals(Path.GetFileName(commonGitDirectory), ".git", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var owner = Directory.GetParent(commonGitDirectory)?.FullName;
                if (string.IsNullOrWhiteSpace(owner) || !Directory.Exists(owner))
                {
                    return false;
                }

                ownerPath = Path.GetFullPath(owner)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }

        return false;
    }

    private static string? ResolveGitPath(string baseDirectory, string? value, string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var path = value.Trim();
        if (prefix is not null)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            path = path[prefix.Length..].Trim();
        }

        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathWithin(string path, string parent)
    {
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
