using PulseMeter.Slices.UsageCollection;
using Microsoft.Data.Sqlite;
using System.Globalization;

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
    public async Task GetProjectUsageAsync_SkipsRepeatedCumulativeSnapshotsWhenCalculatingShares()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var pulseMeterRollout = WriteRollout(
            codexHome,
            "pulsemeter.jsonl",
            (now.AddMinutes(-2), 700, 700),
            (now.AddMinutes(-1), 700, 700));
        var l2Rollout = WriteRollout(codexHome, "l2.jsonl", (now, 300, 300));
        CreateStateDatabase(
            codexHome,
            Thread("thread-pulsemeter", @"C:\Projects\PulseMeter", pulseMeterRollout, now),
            Thread("thread-l2", @"C:\Projects\L2Engine", l2Rollout, now));
        var service = new ProjectUsageService(codexHome);

        var rows = await service.GetProjectUsageAsync(
            [new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 10_000 }],
            now);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("PulseMeter", row.DisplayName);
                Assert.Equal(700, row.RawLocalTokens);
                Assert.Equal(70, row.SharePercent);
                Assert.Equal(7_000, row.EstimatedTokens);
            },
            row =>
            {
                Assert.Equal("L2Engine", row.DisplayName);
                Assert.Equal(300, row.RawLocalTokens);
                Assert.Equal(30, row.SharePercent);
                Assert.Equal(3_000, row.EstimatedTokens);
            });
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

    [Fact]
    public async Task GetProjectUsageAsync_ComputesRecentProjectHealthEvidence()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var firstPulseMeterRollout = WriteRollout(
            codexHome,
            "pulsemeter-first.jsonl",
            (now, 200),
            (now.AddDays(-8), 50));
        var secondPulseMeterRollout = WriteRollout(codexHome, "pulsemeter-second.jsonl", now.AddDays(-2), 100);
        var docsRollout = WriteRollout(
            codexHome,
            "docs.jsonl",
            (now, 30),
            (now.AddDays(-8), 100));
        CreateStateDatabase(
            codexHome,
            Thread("thread-pulse-primary", @"C:\Projects\PulseMeter", firstPulseMeterRollout, now),
            Thread("thread-pulse-secondary", @"C:\Projects\PulseMeter", secondPulseMeterRollout, now),
            Thread("thread-docs", @"C:\Projects\Docs", docsRollout, now));
        var service = new ProjectUsageService(codexHome);

        var rows = await service.GetProjectUsageAsync(
            [new DailyUsageBucket { StartDate = "2026-07-07", Tokens = 480 }],
            now);

        var pulseMeter = Assert.Single(rows, row => row.DisplayName == "PulseMeter");
        Assert.Equal(350, pulseMeter.EstimatedTokens);
        Assert.Equal(300, pulseMeter.EstimatedLast7Days);
        Assert.Equal(50, pulseMeter.EstimatedPrevious7Days);
        Assert.Equal(2, pulseMeter.ActiveDaysLast7);
        Assert.Equal(1, pulseMeter.SpikeDays);
        Assert.StartsWith("PulseMeter chat -", pulseMeter.LeadingChatDisplayName);
        Assert.Equal(200, pulseMeter.LeadingChatEstimatedTokens);
        Assert.StartsWith("PulseMeter chat -", pulseMeter.LargestBurnMomentChatDisplayName);
        Assert.Equal(200, pulseMeter.LargestBurnMomentEstimatedTokens);
        Assert.Equal(now, pulseMeter.LargestBurnMomentAtUtc);
    }

    [Fact]
    public async Task GetProjectUsageAsync_ReusesUnchangedRolloutCache()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "project.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        await service.GetProjectUsageAsync(buckets, now);
        await service.GetProjectUsageAsync(buckets, now);

        Assert.Equal(1, service.RolloutParseCount);
        Assert.Equal(1, service.RolloutCacheEntryCount);
    }

    [Fact]
    public async Task SharedRolloutSource_SharesOneReadOnlySnapshotAcrossProjectAndAttributionProjections()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "shared.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var source = new SharedRolloutAnalyticsSource(codexHome);
        var projects = new ProjectUsageService(source);
        var attribution = new UsageAttributionService(source);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        var sessionSummaries = await source.GetSessionSummariesAsync(DateOnly.FromDateTime(now.ToLocalTime().DateTime).AddDays(-29));
        var projectRows = await projects.GetProjectUsageAsync(buckets, now, sessionSummaries);
        AppendRollout(rollout, now.AddMinutes(1), 200);
        var projectRowsFromSameSnapshot = await projects.GetProjectUsageAsync(buckets, now, sessionSummaries);
        var attributionSnapshot = await attribution.GetUsageAttributionAsync(buckets, now, sessionSummaries);

        Assert.Equal(100, Assert.Single(projectRows).RawLocalTokens);
        Assert.Equal(100, Assert.Single(projectRowsFromSameSnapshot).RawLocalTokens);
        Assert.Equal(100, attributionSnapshot.RawLocalTokens);
        Assert.Equal(1, source.RolloutParseCount);
        Assert.Equal(1, source.RolloutCacheEntryCount);
        var mutableSummaries = Assert.IsAssignableFrom<IList<SharedRolloutSessionSummary>>(sessionSummaries);
        Assert.Throws<NotSupportedException>(() => mutableSummaries.Add(sessionSummaries[0]));
    }

    [Fact]
    public async Task SharedRolloutSource_ReadsExactRateLimitHistoryFromLocalTokenEvents()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
        var weeklyReset = now.AddDays(4);
        var rollout = WriteRateLimitRollout(
            codexHome,
            "rate-history.jsonl",
            (now.AddHours(-2), 15, 74, weeklyReset),
            (now.AddHours(-1), 35, 75, weeklyReset));
        CreateStateDatabase(codexHome, Thread("thread-rate-history", @"C:\Projects\PulseMeter", rollout, now));
        var source = new SharedRolloutAnalyticsSource(codexHome);

        var history = await source.GetRateLimitHistoryAsync(now.AddDays(-7));

        Assert.Equal(4, history.Count);
        Assert.Equal([15d, 35d], history
            .Where(point => point.WindowDurationMins == 300)
            .Select(point => point.UsedPercent));
        Assert.Equal([74d, 75d], history
            .Where(point => point.WindowDurationMins == 10_080)
            .Select(point => point.UsedPercent));
        Assert.All(history, point => Assert.Equal("codex", point.LimitKey));
        Assert.All(history.Where(point => point.WindowDurationMins == 10_080), point => Assert.Equal(weeklyReset, point.ResetsAtUtc));
    }

    [Fact]
    public async Task GetProjectUsageAsync_ReparsesAppendedRolloutAndUpdatesTotals()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "project.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        var first = Assert.Single(await service.GetProjectUsageAsync(buckets, now));
        AppendRollout(rollout, now.AddMinutes(1), 200);
        var second = Assert.Single(await service.GetProjectUsageAsync(buckets, now));

        Assert.Equal(100, first.RawLocalTokens);
        Assert.Equal(300, second.RawLocalTokens);
        Assert.Equal(2, service.RolloutParseCount);
    }

    [Fact]
    public async Task GetProjectUsageAsync_EvictsMissingRolloutCacheEntryBeforeRecreatedRolloutIsRead()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "project.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        await service.GetProjectUsageAsync(buckets, now);
        File.Delete(rollout);
        await service.GetProjectUsageAsync(buckets, now);
        WriteRollout(codexHome, "project.jsonl", now, 200);
        var recreated = Assert.Single(await service.GetProjectUsageAsync(buckets, now));

        Assert.Equal(200, recreated.RawLocalTokens);
        Assert.Equal(2, service.RolloutParseCount);
        Assert.Equal(1, service.RolloutCacheEntryCount);
    }

    [Fact]
    public async Task GetProjectUsageAsync_InvalidatesCachedRolloutWhenCutoffDayChanges()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "project.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        await service.GetProjectUsageAsync(buckets, now);
        await service.GetProjectUsageAsync(buckets, now.AddDays(1));

        Assert.Equal(2, service.RolloutParseCount);
    }

    [Fact]
    public async Task GetProjectUsageAsync_CancellationDoesNotPopulateRolloutCache()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var rollout = WriteRollout(codexHome, "project.jsonl", now, 100);
        CreateStateDatabase(codexHome, Thread("thread-project", @"C:\Projects\ProjectA", rollout, now));
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetProjectUsageAsync(buckets, now, cancellation.Token));
        Assert.Equal(0, service.RolloutCacheEntryCount);

        await service.GetProjectUsageAsync(buckets, now);
        Assert.Equal(1, service.RolloutParseCount);
    }

    [Fact]
    public async Task GetProjectUsageAsync_PrunesUnobservedRolloutsWithoutEvictingEligibleRollouts()
    {
        var codexHome = CreateCodexHome();
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        var threads = new List<ThreadRow>();
        for (var index = 0; index < 65; index++)
        {
            var rollout = WriteRollout(codexHome, $"project-{index}.jsonl", now, 10);
            threads.Add(Thread($"thread-{index}", $@"C:\Projects\Project{index}", rollout, now));
        }

        CreateStateDatabase(codexHome, [.. threads]);
        var service = new ProjectUsageService(codexHome);
        var buckets = new[] { new DailyUsageBucket { StartDate = "2026-07-03", Tokens = 1_000 } };

        await service.GetProjectUsageAsync(buckets, now);
        Assert.Equal(65, service.RolloutCacheEntryCount);

        DeleteThreads(codexHome);
        await service.GetProjectUsageAsync(buckets, now);
        Assert.Equal(0, service.RolloutCacheEntryCount);
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

    private static void AppendRollout(string path, DateTimeOffset timestamp, long totalTokens)
    {
        File.AppendAllText(
            path,
            Environment.NewLine +
            "{" +
            $"\"timestamp\":\"{timestamp:O}\"," +
            "\"type\":\"event_msg\"," +
            "\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{" +
            $"\"input_tokens\":{totalTokens}," +
            "\"cached_input_tokens\":0," +
            "\"output_tokens\":0," +
            $"\"total_tokens\":{totalTokens}" +
            "}}}}");
    }

    private static string WriteRateLimitRollout(
        string codexHome,
        string fileName,
        params (DateTimeOffset Timestamp, double PrimaryUsed, double WeeklyUsed, DateTimeOffset WeeklyReset)[] events)
    {
        var sessions = Path.Combine(codexHome, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, fileName);
        var lines = events.Select(item =>
            "{" +
            $"\"timestamp\":\"{item.Timestamp:O}\"," +
            "\"type\":\"event_msg\"," +
            "\"payload\":{\"type\":\"token_count\",\"rate_limits\":{" +
            "\"limit_id\":\"codex\"," +
            $"\"primary\":{{\"used_percent\":{item.PrimaryUsed.ToString(CultureInfo.InvariantCulture)},\"window_minutes\":300,\"resets_at\":{item.Timestamp.AddHours(5).ToUnixTimeSeconds()}}}," +
            $"\"secondary\":{{\"used_percent\":{item.WeeklyUsed.ToString(CultureInfo.InvariantCulture)},\"window_minutes\":10080,\"resets_at\":{item.WeeklyReset.ToUnixTimeSeconds()}}}" +
            "}}}");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string WriteRollout(
        string codexHome,
        string fileName,
        params (DateTimeOffset Timestamp, long TotalTokens, long? CumulativeTotalTokens)[] events)
    {
        var sessions = Path.Combine(codexHome, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, fileName);
        var lines = events.Select(item =>
        {
            var cumulativeUsage = item.CumulativeTotalTokens is long cumulative
                ? $",\"total_token_usage\":{{\"total_tokens\":{cumulative}}}"
                : string.Empty;

            return "{" +
                $"\"timestamp\":\"{item.Timestamp:O}\"," +
                "\"type\":\"event_msg\"," +
                "\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{" +
                $"\"input_tokens\":{item.TotalTokens}," +
                "\"cached_input_tokens\":0," +
                "\"output_tokens\":0," +
                $"\"total_tokens\":{item.TotalTokens}" +
                "}" + cumulativeUsage + "}}}";
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

    private static void DeleteThreads(string codexHome)
    {
        var path = Path.Combine(codexHome, "state_5.sqlite");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "delete from threads;";
        command.ExecuteNonQuery();
    }

    private sealed record ThreadRow(string Id, string Cwd, string RolloutPath, long UpdatedAtUnixSeconds);
}
