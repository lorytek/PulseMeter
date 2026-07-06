using PulseMeter.Slices.UsageCollection;
using Microsoft.Data.Sqlite;

namespace PulseMeter.Tests;

public sealed class ProjectUsageServiceTests
{
    [Fact]
    public async Task GetProjectUsageAsync_GroupsThreadsByProjectAndScalesToAccountUsage()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var wpfRollout = WriteRollout(codexHome, "wpf.jsonl", now, 600);
        var l2Rollout = WriteRollout(codexHome, "l2.jsonl", now, 400);
        CreateStateDatabase(
            codexHome,
            Thread("thread-wpf", @"\\?\C:\Projects\PulseMeter", wpfRollout, now),
            Thread("thread-l2", @"C:\Projects\L2Engine", l2Rollout, now));
        var service = new ProjectUsageService(codexHome);

        var rows = await service.GetProjectUsageAsync(
            [new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 5_000 }],
            now);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("PulseMeter", row.DisplayName);
                Assert.Equal(@"C:\Projects\PulseMeter", row.FullPath);
                Assert.Equal(3_000, row.EstimatedTokens);
                Assert.Equal(600, row.RawLocalTokens);
                Assert.Equal(60, row.SharePercent);
                Assert.Equal(1, row.ThreadCount);
            },
            row =>
            {
                Assert.Equal("L2Engine", row.DisplayName);
                Assert.Equal(2_000, row.EstimatedTokens);
                Assert.Equal(400, row.RawLocalTokens);
                Assert.Equal(40, row.SharePercent);
            });
    }

    [Fact]
    public async Task GetProjectUsageAsync_UsesOnlyLastThirtyLocalCalendarDays()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(
            codexHome,
            "project.jsonl",
            (now, 100),
            (now.AddDays(-29), 300),
            (now.AddDays(-30), 9_000));
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);

        var rows = await service.GetProjectUsageAsync(
            [
                new DailyUsageBucket { StartDate = "2026-06-03", Tokens = 9_000 },
                new DailyUsageBucket { StartDate = "2026-06-05", Tokens = 3_000 },
                new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 }
            ],
            now);

        var row = Assert.Single(rows);
        Assert.Equal(400, row.RawLocalTokens);
        Assert.Equal(4_000, row.EstimatedTokens);
    }

    [Fact]
    public async Task GetProjectUsageAsync_DisambiguatesDuplicateFolderNames()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var firstRollout = WriteRollout(codexHome, "first.jsonl", now, 100);
        var secondRollout = WriteRollout(codexHome, "second.jsonl", now, 100);
        CreateStateDatabase(
            codexHome,
            Thread("thread-first", @"C:\Repos\Alpha\WPF", firstRollout, now),
            Thread("thread-second", @"C:\Repos\Beta\WPF", secondRollout, now));
        var service = new ProjectUsageService(codexHome);

        var rows = await service.GetProjectUsageAsync(
            [new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 2_000 }],
            now);

        Assert.Equal(["WPF (Alpha)", "WPF (Beta)"], rows.Select(row => row.DisplayName).Order());
    }

    [Fact]
    public async Task GetProjectUsageAsync_ReturnsEmptyWhenAccountUsageOrStateIsUnavailable()
    {
        var codexHome = CreateCodexHome();
        var service = new ProjectUsageService(codexHome);

        var rowsWithoutState = await service.GetProjectUsageAsync(
            [new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 }],
            new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero));
        CreateStateDatabase(codexHome);
        var rowsWithoutAccountTotal = await service.GetProjectUsageAsync(
            [],
            new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(rowsWithoutState);
        Assert.Empty(rowsWithoutAccountTotal);
    }

    private static string CreateCodexHome()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ThreadRow Thread(string id, string cwd, string rolloutPath, DateTimeOffset updatedAt)
    {
        return new ThreadRow(id, cwd, rolloutPath, updatedAt.ToUnixTimeSeconds());
    }

    private static string WriteRollout(string codexHome, string fileName, DateTimeOffset timestamp, long totalTokens)
    {
        return WriteRollout(codexHome, fileName, (timestamp, totalTokens));
    }

    private static string WriteRollout(string codexHome, string fileName, params (DateTimeOffset Timestamp, long TotalTokens)[] events)
    {
        var sessions = Path.Combine(codexHome, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, fileName);
        var lines = events.Select(item =>
            "{" +
            $"\"timestamp\":\"{item.Timestamp:O}\"," +
            "\"type\":\"event_msg\"," +
            "\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{" +
            $"\"input_tokens\":{item.TotalTokens}," +
            "\"cached_input_tokens\":0," +
            "\"output_tokens\":0," +
            $"\"total_tokens\":{item.TotalTokens}" +
            "}}}}");
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
                insert into threads (id, rollout_path, updated_at, cwd, tokens_used)
                values ($id, $rollout_path, $updated_at, $cwd, 0);
                """;
            command.Parameters.AddWithValue("$id", thread.Id);
            command.Parameters.AddWithValue("$rollout_path", thread.RolloutPath);
            command.Parameters.AddWithValue("$updated_at", thread.UpdatedAtUnixSeconds);
            command.Parameters.AddWithValue("$cwd", thread.Cwd);
            command.ExecuteNonQuery();
        }
    }

    private sealed record ThreadRow(string Id, string Cwd, string RolloutPath, long UpdatedAtUnixSeconds);
}
