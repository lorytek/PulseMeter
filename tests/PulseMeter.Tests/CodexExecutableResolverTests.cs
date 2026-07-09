using PulseMeter.Platform.Codex;
using PulseMeter.Slices.UsageCollection;
using System.Text;

namespace PulseMeter.Tests;

public sealed class CodexExecutableResolverTests
{
    [Fact]
    public void Resolve_PrefersPulseMeterCliPathEnvironmentVariable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        var executable = Path.Combine(directory, "codex.exe");
        Directory.CreateDirectory(directory);
        File.WriteAllText(executable, string.Empty);
        var previousValue = Environment.GetEnvironmentVariable("PULSEMETER_CODEX_PATH");

        try
        {
            Environment.SetEnvironmentVariable("PULSEMETER_CODEX_PATH", executable);

            var result = CodexExecutableResolver.Resolve();

            Assert.NotNull(result);
            Assert.Equal(executable, result.ExecutablePath);
            Assert.Equal("PULSEMETER_CODEX_PATH", result.Source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PULSEMETER_CODEX_PATH", previousValue);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryReadConfiguredCliPath_ReadsSingleQuotedConfigValue()
    {
        const string config = "CODEX_CLI_PATH = 'C:\\Users\\example\\AppData\\Local\\OpenAI\\Codex\\bin\\abc\\codex.exe'";

        var path = CodexExecutableResolver.TryReadConfiguredCliPath(config);

        Assert.Equal("C:\\Users\\example\\AppData\\Local\\OpenAI\\Codex\\bin\\abc\\codex.exe", path);
    }

    [Fact]
    public void ResolveFromCandidates_ReturnsFirstExistingCandidate()
    {
        var candidates = new[]
        {
            "C:\\missing\\codex.exe",
            "C:\\good\\codex.exe",
            "C:\\later\\codex.exe"
        };

        var result = CodexExecutableResolver.ResolveFromCandidates(candidates, path => path == "C:\\good\\codex.exe");

        Assert.NotNull(result);
        Assert.Equal("C:\\good\\codex.exe", result.ExecutablePath);
    }

    [Fact]
    public void BuildStartInfo_RunsCmdFilesThroughCommandProcessor()
    {
        var startInfo = AppServerProcess.BuildStartInfo("C:\\Users\\ilina\\.codex\\bin\\codex.cmd");

        Assert.EndsWith("cmd.exe", startInfo.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("codex.cmd", startInfo.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app-server", startInfo.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
    }

    [Fact]
    public void BuildStartInfo_UsesBomlessUtf8ForJsonLineTransport()
    {
        var startInfo = AppServerProcess.BuildStartInfo("C:\\Users\\ilina\\AppData\\Local\\OpenAI\\Codex\\bin\\codex.exe");

        Assert.NotNull(startInfo.StandardInputEncoding);
        Assert.Equal("utf-8", startInfo.StandardInputEncoding.WebName);
        Assert.Empty(startInfo.StandardInputEncoding.GetPreamble());
        Assert.Equal(Encoding.UTF8.WebName, startInfo.StandardOutputEncoding?.WebName);
        Assert.Equal(Encoding.UTF8.WebName, startInfo.StandardErrorEncoding?.WebName);
    }
}
