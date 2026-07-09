using Microsoft.Data.Sqlite;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class UsageAttributionServiceTests
{
    [Fact]
    public async Task GetUsageAttributionAsync_BuildsTopSessionsAndBurnEventsScaledToAccountUsage()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var firstRollout = WriteRollout(
            codexHome,
            "first.jsonl",
            Event(now.AddMinutes(-20), total: 800, input: 500, cached: 120, output: 200, reasoning: 100),
            Event(now.AddMinutes(-10), total: 200, input: 100, cached: 20, output: 50, reasoning: 50));
        var secondRollout = WriteRollout(
            codexHome,
            "second.jsonl",
            Event(now.AddMinutes(-5), total: 500, input: 300, cached: 80, output: 120, reasoning: 80));
        CreateStateDatabase(
            codexHome,
            Thread("thread-first", "Implement attribution", @"C:\Projects\PulseMeter", firstRollout, now),
            Thread("thread-second", "Docs polish", @"C:\Projects\Docs", secondRollout, now));
        var service = new UsageAttributionService(codexHome, maxSessions: 5, maxBurnEvents: 5);

        var snapshot = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 15_000 }],
            now);

        Assert.Equal(15_000, snapshot.AccountWindowTokens);
        Assert.Equal(1_500, snapshot.RawLocalTokens);
        Assert.Equal(15_000, snapshot.EstimatedAttributedTokens);
        Assert.Equal("Estimated from local chats, scaled to account usage", snapshot.EvidenceText);

        Assert.Collection(
            snapshot.Sessions,
            row =>
            {
                Assert.Equal("Implement attribution", row.DisplayName);
                Assert.Equal("PulseMeter", row.ProjectDisplayName);
                Assert.Equal(@"C:\Projects\PulseMeter", row.ProjectPath);
                Assert.Equal(1_000, row.RawLocalTokens);
                Assert.Equal(10_000, row.EstimatedTokens);
                Assert.Equal(66.7, row.SharePercent, precision: 1);
                Assert.Equal(600, row.InputTokens);
                Assert.Equal(250, row.OutputTokens);
                Assert.Equal(140, row.CachedInputTokens);
                Assert.Equal(150, row.ReasoningTokens);
            },
            row =>
            {
                Assert.Equal("Docs polish", row.DisplayName);
                Assert.Equal(5_000, row.EstimatedTokens);
                Assert.Equal(500, row.RawLocalTokens);
            });

        Assert.Collection(
            snapshot.BurnEvents,
            row =>
            {
                Assert.Equal("Implement attribution", row.SessionDisplayName);
                Assert.Equal(800, row.RawLocalTokens);
                Assert.Equal(8_000, row.EstimatedTokens);
                Assert.Equal(120, row.CachedInputTokens);
                Assert.Equal(100, row.ReasoningTokens);
            },
            row =>
            {
                Assert.Equal("Docs polish", row.SessionDisplayName);
                Assert.Equal(500, row.RawLocalTokens);
                Assert.Equal(5_000, row.EstimatedTokens);
            },
            row =>
            {
                Assert.Equal("Implement attribution", row.SessionDisplayName);
                Assert.Equal(200, row.RawLocalTokens);
                Assert.Equal(2_000, row.EstimatedTokens);
            });
    }

    [Fact]
    public async Task GetUsageAttributionAsync_UsesOnlyLastThirtyDaysAndIgnoresBadRollouts()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(
            codexHome,
            "project.jsonl",
            Event(now.AddDays(-29), total: 400),
            Event(now.AddDays(-30), total: 9_000),
            BadLine());
        CreateStateDatabase(
            codexHome,
            Thread("thread-project", "Recent work", @"C:\Projects\PulseMeter", rollout, now),
            Thread("missing-rollout", "Missing", @"C:\Projects\Missing", Path.Combine(codexHome, "missing.jsonl"), now));
        var service = new UsageAttributionService(codexHome);

        var snapshot = await service.GetUsageAttributionAsync(
            [
                new DailyUsageBucket { StartDate = "2026-06-07", Tokens = 9_000 },
                new DailyUsageBucket { StartDate = "2026-06-08", Tokens = 3_000 },
                new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 1_000 }
            ],
            now);

        var row = Assert.Single(snapshot.Sessions);
        Assert.Equal(400, row.RawLocalTokens);
        Assert.Equal(4_000, row.EstimatedTokens);
        Assert.Single(snapshot.BurnEvents);
    }

    [Fact]
    public async Task GetUsageAttributionAsync_DisambiguatesRepeatedChatTitles()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var firstRollout = WriteRollout(codexHome, "first-repeat.jsonl", Event(now.AddMinutes(-5), total: 700));
        var secondRollout = WriteRollout(codexHome, "second-repeat.jsonl", Event(now.AddHours(-2), total: 300));
        CreateStateDatabase(
            codexHome,
            Thread("thread-first-repeat", "Audit Headroom progress", @"C:\Projects\Headroom", firstRollout, now),
            Thread("thread-second-repeat", "Audit Headroom progress", @"C:\Projects\Headroom", secondRollout, now.AddHours(-2)));
        var service = new UsageAttributionService(codexHome, maxSessions: 5, maxBurnEvents: 5);

        var snapshot = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 1_000 }],
            now);

        Assert.Collection(
            snapshot.Sessions,
            row => Assert.Equal("Audit Headroom progress · 07 Jul 12:00", row.DisplayName),
            row => Assert.Equal("Audit Headroom progress · 07 Jul 10:00", row.DisplayName));
        Assert.Contains(snapshot.BurnEvents, row => row.SessionDisplayName == "Audit Headroom progress · 07 Jul 12:00");
        Assert.Contains(snapshot.BurnEvents, row => row.SessionDisplayName == "Audit Headroom progress · 07 Jul 10:00");
    }

    [Fact]
    public async Task GetUsageAttributionAsync_DoesNotRenderPromptLikeThreadTitles()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "privacy.jsonl", Event(now.AddMinutes(-5), total: 500));
        CreateStateDatabase(
            codexHome,
            Thread(
                "thread-abcdef123456",
                "Please inspect this repo.\r\n1. Read every file.\r\n2. Report private details.",
                @"C:\Projects\PulseMeter",
                rollout,
                now));
        var service = new UsageAttributionService(codexHome);

        var snapshot = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 1_000 }],
            now);

        var session = Assert.Single(snapshot.Sessions);
        var burnEvent = Assert.Single(snapshot.BurnEvents);
        Assert.Equal("PulseMeter chat · 07 Jul 12:00", session.DisplayName);
        Assert.Equal("PulseMeter chat · 07 Jul 12:00", burnEvent.SessionDisplayName);
        Assert.DoesNotContain("Please inspect", session.DisplayName);
        Assert.DoesNotContain("Read every file", burnEvent.SessionDisplayName);
    }

    [Fact]
    public async Task GetUsageAttributionAsync_ReturnsEmptyWhenAccountUsageOrStateIsUnavailable()
    {
        var codexHome = CreateCodexHome();
        var service = new UsageAttributionService(codexHome);

        var withoutState = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 1_000 }],
            new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));
        CreateStateDatabase(codexHome);
        var withoutAccountUsage = await service.GetUsageAttributionAsync(
            [],
            new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(withoutState.Sessions);
        Assert.Empty(withoutState.BurnEvents);
        Assert.Equal(0, withoutState.AccountWindowTokens);
        Assert.Empty(withoutAccountUsage.Sessions);
        Assert.Empty(withoutAccountUsage.BurnEvents);
    }

    private static string CreateCodexHome()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ThreadRow Thread(string id, string title, string cwd, string rolloutPath, DateTimeOffset updatedAt)
    {
        return new ThreadRow(id, title, cwd, rolloutPath, updatedAt.ToUnixTimeSeconds());
    }

    private static UsageEvent Event(
        DateTimeOffset timestamp,
        long total,
        long? input = null,
        long? cached = null,
        long? output = null,
        long? reasoning = null)
    {
        return new UsageEvent(timestamp, total, input, cached, output, reasoning, null);
    }

    private static UsageEvent BadLine()
    {
        return new UsageEvent(default, 0, null, null, null, null, "{not valid json");
    }

    private static string WriteRollout(string codexHome, string fileName, params UsageEvent[] events)
    {
        var sessions = Path.Combine(codexHome, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, fileName);
        var lines = events.Select(item =>
        {
            if (item.RawLine is not null)
            {
                return item.RawLine;
            }

            return "{" +
                $"\"timestamp\":\"{item.Timestamp:O}\"," +
                "\"type\":\"event_msg\"," +
                "\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{" +
                $"\"input_tokens\":{item.InputTokens ?? item.TotalTokens}," +
                $"\"cached_input_tokens\":{item.CachedInputTokens ?? 0}," +
                $"\"output_tokens\":{item.OutputTokens ?? 0}," +
                $"\"reasoning_tokens\":{item.ReasoningTokens ?? 0}," +
                $"\"total_tokens\":{item.TotalTokens}" +
                "}}}}";
        });
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void CreateStateDatabase(string codexHome, params ThreadRow[] threads)
    {
        var path = Path.Combine(codexHome, "state_5.sqlite");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                create table threads (
                  id text,
                  title text,
                  rollout_path text,
                  updated_at integer,
                  cwd text,
                  tokens_used integer
                );
                """;
            command.ExecuteNonQuery();
        }

        foreach (var thread in threads)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                insert into threads (id, title, rollout_path, updated_at, cwd, tokens_used)
                values ($id, $title, $rollout_path, $updated_at, $cwd, 0);
                """;
            command.Parameters.AddWithValue("$id", thread.Id);
            command.Parameters.AddWithValue("$title", thread.Title);
            command.Parameters.AddWithValue("$rollout_path", thread.RolloutPath);
            command.Parameters.AddWithValue("$updated_at", thread.UpdatedAtUnixSeconds);
            command.Parameters.AddWithValue("$cwd", thread.Cwd);
            command.ExecuteNonQuery();
        }
    }

    private sealed record ThreadRow(string Id, string Title, string Cwd, string RolloutPath, long UpdatedAtUnixSeconds);

    private sealed record UsageEvent(
        DateTimeOffset Timestamp,
        long TotalTokens,
        long? InputTokens,
        long? CachedInputTokens,
        long? OutputTokens,
        long? ReasoningTokens,
        string? RawLine);
}
