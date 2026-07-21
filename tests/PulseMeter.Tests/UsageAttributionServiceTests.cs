using Microsoft.Data.Sqlite;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class UsageAttributionServiceTests
{
    [Fact]
    public async Task GetUsageAttributionAsync_BuildsScaledSessionAggregationForProjectAttribution()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var pulseMeter = WriteRollout(codexHome, "pulse.jsonl", Event(now.AddMinutes(-20), 800), Event(now.AddMinutes(-10), 200));
        var docs = WriteRollout(codexHome, "docs.jsonl", Event(now.AddMinutes(-5), 500));
        CreateStateDatabase(
            codexHome,
            Thread("pulse", @"C:\Projects\PulseMeter", pulseMeter, now),
            Thread("docs", @"C:\Projects\Docs", docs, now));
        var service = new UsageAttributionService(codexHome, maxSessions: 5);

        var snapshot = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 15_000 }],
            now);

        Assert.Equal(15_000, snapshot.AccountWindowTokens);
        Assert.Equal(1_500, snapshot.RawLocalTokens);
        Assert.Equal(15_000, snapshot.EstimatedAttributedTokens);
        Assert.Collection(
            snapshot.Sessions,
            row =>
            {
                Assert.Equal("PulseMeter", row.ProjectDisplayName);
                Assert.Equal("PulseMeter chat · 07 Jul 12:00", row.DisplayName);
                Assert.Equal(1_000, row.RawLocalTokens);
                Assert.Equal(10_000, row.EstimatedTokens);
                Assert.Equal(66.7, row.SharePercent, precision: 1);
            },
            row =>
            {
                Assert.Equal("Docs", row.ProjectDisplayName);
                Assert.Equal(500, row.RawLocalTokens);
                Assert.Equal(5_000, row.EstimatedTokens);
            });
    }

    [Fact]
    public async Task GetUsageAttributionAsync_SkipsRepeatedCumulativeSnapshots()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 1, 0, TimeSpan.Zero);
        var rollout = WriteRollout(
            codexHome,
            "deduplicated.jsonl",
            Event(now.AddSeconds(-55), 600, 600),
            Event(now.AddSeconds(-40), 500, 1_100),
            Event(now.AddSeconds(-35), 500, 1_100));
        CreateStateDatabase(codexHome, Thread("pulse", @"C:\Projects\PulseMeter", rollout, now));

        var snapshot = await new UsageAttributionService(codexHome).GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 1_100 }],
            now);

        Assert.Equal(1_100, snapshot.RawLocalTokens);
        Assert.Equal(1_100, Assert.Single(snapshot.Sessions).EstimatedTokens);
    }

    [Fact]
    public async Task GetUsageAttributionAsync_UsesOnlyLastThirtyDaysAndReturnsEmptyWithoutAccountUsage()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "recent.jsonl", Event(now.AddDays(-29), 400), Event(now.AddDays(-30), 9_000));
        CreateStateDatabase(codexHome, Thread("pulse", @"C:\Projects\PulseMeter", rollout, now));
        var service = new UsageAttributionService(codexHome);

        var attributed = await service.GetUsageAttributionAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 4_000 }],
            now);
        var empty = await service.GetUsageAttributionAsync([], now);

        Assert.Equal(400, attributed.RawLocalTokens);
        Assert.Equal(4_000, Assert.Single(attributed.Sessions).EstimatedTokens);
        Assert.Empty(empty.Sessions);
    }

    private static string CreateCodexHome()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteRollout(string codexHome, string fileName, params UsageEvent[] events)
    {
        var sessions = Path.Combine(codexHome, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, fileName);
        File.WriteAllLines(path, events.Select(item =>
        {
            var cumulativeUsage = item.CumulativeTotalTokens is long cumulative
                ? $",\"total_token_usage\":{{\"total_tokens\":{cumulative}}}"
                : string.Empty;
            return "{" +
                $"\"timestamp\":\"{item.Timestamp:O}\"," +
                "\"type\":\"event_msg\"," +
                "\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{" +
                $"\"input_tokens\":{item.TotalTokens},\"output_tokens\":0,\"cached_input_tokens\":0,\"reasoning_tokens\":0,\"total_tokens\":{item.TotalTokens}" +
                "}" + cumulativeUsage + "}}}";
        }));
        return path;
    }

    private static void CreateStateDatabase(string codexHome, params ThreadRow[] threads)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(codexHome, "state_5.sqlite")}");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "create table threads (id text, title text, rollout_path text, updated_at integer, cwd text);";
            command.ExecuteNonQuery();
        }

        foreach (var thread in threads)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "insert into threads (id, title, rollout_path, updated_at, cwd) values ($id, '', $rollout, $updated, $cwd);";
            command.Parameters.AddWithValue("$id", thread.Id);
            command.Parameters.AddWithValue("$rollout", thread.RolloutPath);
            command.Parameters.AddWithValue("$updated", thread.UpdatedAt.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$cwd", thread.Cwd);
            command.ExecuteNonQuery();
        }
    }

    private static ThreadRow Thread(string id, string cwd, string rolloutPath, DateTimeOffset updatedAt) => new(id, cwd, rolloutPath, updatedAt);

    private static UsageEvent Event(DateTimeOffset timestamp, long totalTokens, long? cumulativeTotalTokens = null) => new(timestamp, totalTokens, cumulativeTotalTokens);

    private sealed record ThreadRow(string Id, string Cwd, string RolloutPath, DateTimeOffset UpdatedAt);

    private sealed record UsageEvent(DateTimeOffset Timestamp, long TotalTokens, long? CumulativeTotalTokens);
}
