using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class ThreadUsageParserTests
{
    [Fact]
    public void ParseThreadUsage_LabelsThreadAsRecentNotExactCurrent()
    {
        using var document = JsonDocument.Parse("""
            {
              "threadId": "thread-123",
              "threadName": "Payment refactor",
              "tokenUsage": {
                "inputTokens": 100,
                "outputTokens": 40,
                "contextUsedPercent": 59
              }
            }
            """);

        var thread = CodexUsageParser.ParseThreadUsage(document.RootElement, DateTimeOffset.FromUnixTimeSeconds(1_730_000_000));

        Assert.NotNull(thread);
        Assert.Equal("thread-123", thread.ThreadId);
        Assert.Equal("Payment refactor", thread.ThreadName);
        Assert.Equal(140, thread.TotalTokens);
        Assert.Equal(59, thread.ContextUsedPercent);
        Assert.Equal(41, thread.ContextLeftPercent);
        Assert.False(thread.IsExactCurrentDesktopThread);
    }
}
