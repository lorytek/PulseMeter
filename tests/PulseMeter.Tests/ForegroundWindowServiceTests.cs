using System.ComponentModel;

using PulseMeter.Platform.Windows;

namespace PulseMeter.Tests;

public sealed class ForegroundWindowServiceTests
{
    [Theory]
    [InlineData("Codex", true)]
    [InlineData("codex-desktop", true)]
    [InlineData("ChatGPT", true)]
    [InlineData("chatgpt.exe", true)]
    [InlineData("notepad", false)]
    public void IsCodexProcess_MatchesProcessNameWithoutCaseSensitivity(string processName, bool expected)
    {
        var result = ForegroundWindowService.IsCodexProcess(42, processId =>
        {
            Assert.Equal(42, processId);
            return processName;
        });

        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(UnavailableProcessExceptions))]
    public void IsCodexProcess_WhenProcessCannotBeInspectedFallsBackWithoutThrowing(Exception exception)
    {
        var result = ForegroundWindowService.IsCodexProcess(42, _ => throw exception);

        Assert.False(result);
    }

    public static TheoryData<Exception> UnavailableProcessExceptions => new()
    {
        new ArgumentException("Process disappeared."),
        new InvalidOperationException("Process exited."),
        new Win32Exception(5, "Access denied.")
    };
}
