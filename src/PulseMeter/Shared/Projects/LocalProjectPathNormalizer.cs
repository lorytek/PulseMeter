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
        }

        normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

    private static bool IsPathWithin(string path, string parent)
    {
        return path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
