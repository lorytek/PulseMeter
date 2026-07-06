using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PulseMeter.Platform.Codex;

public sealed record CodexExecutableResolution(string ExecutablePath, string Source);

public static class CodexExecutableResolver
{
    private const string OverrideEnvName = "PULSEMETER_CODEX_PATH";

    public static CodexExecutableResolution? Resolve()
    {
        var candidates = new List<(string? Path, string Source)>
        {
            (Environment.GetEnvironmentVariable(OverrideEnvName), OverrideEnvName),
            (TryReadConfiguredCliPath(), "app config"),
            (Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAI",
                "Codex",
                "bin",
                "codex.exe"), "stable install"),
            (Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex",
                "bin",
                "codex.cmd"), "user wrapper")
        };

        candidates.AddRange(GetPathCandidates().Select(path => (Path: (string?)path, Source: "PATH")));
        candidates.AddRange(GetRecursiveLocalCodexCandidates().Select(path => (Path: (string?)path, Source: "local install")));
        candidates.AddRange(GetRunningDesktopCodexCandidates().Select(path => (Path: (string?)path, Source: "running desktop app")));

        return ResolveFromCandidates(candidates);
    }

    public static CodexExecutableResolution? ResolveFromCandidates(
        IEnumerable<string?> candidates,
        Func<string, bool> exists)
    {
        return ResolveFromCandidates(candidates.Select(candidate => (Path: candidate, Source: "test")), exists);
    }

    public static string? TryReadConfiguredCliPath(string configText)
    {
        var match = Regex.Match(configText, @"(?m)^\s*CODEX_CLI_PATH\s*=\s*'([^']+)'\s*$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static CodexExecutableResolution? ResolveFromCandidates(
        IEnumerable<(string? Path, string Source)> candidates,
        Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Path))
            {
                continue;
            }

            var expanded = Environment.ExpandEnvironmentVariables(candidate.Path);
            if (exists(expanded))
            {
                return new CodexExecutableResolution(expanded, candidate.Source);
            }
        }

        return null;
    }

    private static string? TryReadConfiguredCliPath()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "config.toml");

        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            return TryReadConfiguredCliPath(File.ReadAllText(configPath));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> GetPathCandidates()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows()
            ? [".exe", ".cmd", ".bat"]
            : new[] { string.Empty };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                yield return Path.Combine(directory, "codex" + extension);
            }
        }
    }

    private static IEnumerable<string> GetRecursiveLocalCodexCandidates()
    {
        var binRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenAI",
            "Codex",
            "bin");

        if (!Directory.Exists(binRoot))
        {
            yield break;
        }

        IEnumerable<FileInfo> files;
        try
        {
            files = new DirectoryInfo(binRoot)
                .EnumerateFiles("codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file.FullName;
        }
    }

    private static IEnumerable<string> GetRunningDesktopCodexCandidates()
    {
        foreach (var processName in new[] { "codex", "Codex" })
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    string? processPath = null;
                    try
                    {
                        processPath = process.MainModule?.FileName;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }

                    if (string.IsNullOrWhiteSpace(processPath))
                    {
                        continue;
                    }

                    if (processPath.EndsWith("resources\\codex.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return processPath;
                    }

                    var appDirectory = Path.GetDirectoryName(processPath);
                    if (!string.IsNullOrWhiteSpace(appDirectory))
                    {
                        yield return Path.Combine(appDirectory, "resources", "codex.exe");
                    }
                }
            }
        }
    }
}
