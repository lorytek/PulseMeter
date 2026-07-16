using System.IO;
using System.Globalization;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;
using Microsoft.Data.Sqlite;
using PulseMeter.Shared.Projects;

namespace PulseMeter.Slices.UsageCollection.Business;

public interface IProjectUsageService
{
    Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectUsageService : IProjectUsageService
{
    private const int UsageWindowDays = 30;
    private readonly string _codexHome;
    private readonly int _maxRows;

    public ProjectUsageService(string? codexHome = null, int maxRows = 8)
    {
        _codexHome = codexHome ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
        _maxRows = Math.Max(1, maxRows);
    }

    public Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var rows = GetProjectUsage(dailyBuckets, now, cancellationToken);
        return Task.FromResult(rows);
    }

    private IReadOnlyList<ProjectUsageRow> GetProjectUsage(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoffDate = GetCutoffDate(now);
        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var recentWeekStart = today.AddDays(-6);
        var previousWeekStart = today.AddDays(-13);
        var accountTotal = GetAccountTotalForWindow(dailyBuckets, cutoffDate);
        if (accountTotal <= 0)
        {
            return Array.Empty<ProjectUsageRow>();
        }

        var databasePath = Path.Combine(_codexHome, "state_5.sqlite");
        if (!File.Exists(databasePath))
        {
            return Array.Empty<ProjectUsageRow>();
        }

        var aggregates = new Dictionary<string, ProjectUsageAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var thread in ReadThreads(databasePath, cutoffDate))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!LocalProjectPathNormalizer.IsUserProjectPath(thread.Cwd))
            {
                continue;
            }

            var rolloutPath = ResolveRolloutPath(thread.RolloutPath);
            var tokenRecords = ReadRolloutTokens(rolloutPath, cutoffDate, cancellationToken);
            if (tokenRecords.Count == 0)
            {
                continue;
            }

            var fullPath = NormalizeProjectPath(thread.Cwd);
            if (!aggregates.TryGetValue(fullPath, out var aggregate))
            {
                aggregate = new ProjectUsageAggregate(fullPath);
                aggregates.Add(fullPath, aggregate);
            }

            aggregate.AddUsage(thread.Id, tokenRecords, recentWeekStart, previousWeekStart);
            aggregate.ThreadCount++;
        }

        var totalRawTokens = aggregates.Values.Sum(aggregate => aggregate.RawLocalTokens);
        if (totalRawTokens <= 0)
        {
            return Array.Empty<ProjectUsageRow>();
        }

        var baseNameCounts = aggregates.Values
            .GroupBy(aggregate => GetBaseDisplayName(aggregate.FullPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return aggregates.Values
            .Select(aggregate => ToProjectUsageRow(aggregate, baseNameCounts, totalRawTokens, accountTotal))
            .OrderByDescending(row => row.EstimatedTokens)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(_maxRows)
            .ToList();
    }

    private IReadOnlyList<ThreadRow> ReadThreads(string databasePath, DateOnly cutoffDate)
    {
        try
        {
            var rows = new List<ThreadRow>();
            var cutoffUnixSeconds = GetCutoffStartUtc(cutoffDate).ToUnixTimeSeconds();
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            };

            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                select id, rollout_path, cwd
                from threads
                where rollout_path is not null
                  and updated_at >= $cutoff
                """;
            command.Parameters.AddWithValue("$cutoff", cutoffUnixSeconds);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ThreadRow(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }

            return rows;
        }
        catch (SqliteException)
        {
            return Array.Empty<ThreadRow>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<ThreadRow>();
        }
    }

    private string ResolveRolloutPath(string rolloutPath)
    {
        if (string.IsNullOrWhiteSpace(rolloutPath))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(rolloutPath)
            ? rolloutPath
            : Path.Combine(_codexHome, rolloutPath);
    }

    private static IReadOnlyList<RolloutTokenRecord> ReadRolloutTokens(string rolloutPath, DateOnly cutoffDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rolloutPath) || !File.Exists(rolloutPath))
        {
            return Array.Empty<RolloutTokenRecord>();
        }

        var tokenRecords = new List<RolloutTokenRecord>();
        var cumulativeTotals = new HashSet<long>();
        try
        {
            foreach (var line in File.ReadLines(rolloutPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!line.Contains("\"token_count\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReadTokenCountLine(line, cutoffDate) is { } tokenRecord
                    && (tokenRecord.CumulativeTotalTokens is not long cumulativeTotal
                        || cumulativeTotals.Add(cumulativeTotal)))
                {
                    tokenRecords.Add(tokenRecord);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return tokenRecords;
    }

    private static RolloutTokenRecord? ReadTokenCountLine(string line, DateOnly cutoffDate)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!StringEquals(root, "type", "event_msg"))
            {
                return null;
            }

            if (!TryGetObject(root, "payload", out var payload)
                || !StringEquals(payload, "type", "token_count")
                || !TryGetObject(payload, "info", out var info)
                || !TryGetObject(info, "last_token_usage", out var usage))
            {
                return null;
            }

            var timestamp = ReadTimestamp(root);
            if (timestamp is null || DateOnly.FromDateTime(timestamp.Value.ToLocalTime().DateTime) < cutoffDate)
            {
                return null;
            }

            var lastTokenUsage = ReadLong(usage, "total_tokens") ?? ReadLong(usage, "totalTokens");
            if (lastTokenUsage is null or <= 0)
            {
                return null;
            }

            var cumulativeTotalTokens = TryGetObject(info, "total_token_usage", out var totalUsage)
                ? ReadLong(totalUsage, "total_tokens") ?? ReadLong(totalUsage, "totalTokens")
                : null;

            return new RolloutTokenRecord(timestamp.Value.ToUniversalTime(), lastTokenUsage.Value, cumulativeTotalTokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ProjectUsageRow ToProjectUsageRow(
        ProjectUsageAggregate aggregate,
        IReadOnlyDictionary<string, int> baseNameCounts,
        long totalRawTokens,
        long accountTotal)
    {
        var sharePercent = aggregate.RawLocalTokens / (double)totalRawTokens * 100;
        var estimatedTokens = (long)Math.Round(accountTotal * (aggregate.RawLocalTokens / (double)totalRawTokens));
        var scale = accountTotal / (double)totalRawTokens;
        var baseDisplayName = GetBaseDisplayName(aggregate.FullPath);
        var displayName = baseNameCounts.TryGetValue(baseDisplayName, out var count) && count > 1
            ? $"{baseDisplayName} ({GetParentDisplayName(aggregate.FullPath)})"
            : baseDisplayName;
        var leadingChats = aggregate.RecentThreadUsage.Values
            .OrderByDescending(item => item.RawTokens)
            .ThenBy(item => item.ThreadId, StringComparer.Ordinal)
            .Take(2)
            .ToList();
        var leadingChat = leadingChats.FirstOrDefault();
        var secondLeadingChat = leadingChats.Skip(1).FirstOrDefault();
        var largestMoment = aggregate.LargestRecentMoment;

        return new ProjectUsageRow(
            displayName,
            aggregate.FullPath,
            estimatedTokens,
            aggregate.RawLocalTokens,
            aggregate.ThreadCount,
            Math.Round(sharePercent, 1),
            ScaleTokens(aggregate.RawLast7Days, scale),
            ScaleTokens(aggregate.RawPrevious7Days, scale),
            aggregate.ActiveDaysLast7.Count,
            CountSpikeDays(aggregate.DailyTokens),
            leadingChat is null ? string.Empty : FormatChatDisplayName(displayName, leadingChat.LatestTimestampUtc ?? DateTimeOffset.UnixEpoch),
            leadingChat is null ? 0 : ScaleTokens(leadingChat.RawTokens, scale),
            secondLeadingChat is null ? string.Empty : FormatChatDisplayName(displayName, secondLeadingChat.LatestTimestampUtc ?? DateTimeOffset.UnixEpoch),
            secondLeadingChat is null ? 0 : ScaleTokens(secondLeadingChat.RawTokens, scale),
            largestMoment is null ? string.Empty : FormatChatDisplayName(displayName, largestMoment.TimestampUtc),
            largestMoment is null ? 0 : ScaleTokens(largestMoment.TotalTokens, scale),
            largestMoment?.TimestampUtc);
    }

    private static long ScaleTokens(long rawTokens, double scale)
    {
        return (long)Math.Round(rawTokens * scale);
    }

    private static string FormatChatDisplayName(string projectDisplayName, DateTimeOffset timestampUtc)
    {
        return $"{projectDisplayName} chat - {timestampUtc.ToLocalTime():dd MMM HH:mm}";
    }

    private static int CountSpikeDays(IReadOnlyDictionary<DateOnly, long> dailyTokens)
    {
        var activeDays = dailyTokens.Values
            .Where(tokens => tokens > 0)
            .OrderBy(tokens => tokens)
            .ToList();
        if (activeDays.Count == 0)
        {
            return 0;
        }

        var middle = activeDays.Count / 2;
        var median = activeDays.Count % 2 == 1
            ? activeDays[middle]
            : (long)Math.Round((activeDays[middle - 1] + activeDays[middle]) / 2d);
        return median <= 0
            ? 0
            : activeDays.Count(tokens => tokens >= median * 1.5);
    }

    private static long GetAccountTotalForWindow(IReadOnlyList<DailyUsageBucket> dailyBuckets, DateOnly cutoffDate)
    {
        return dailyBuckets
            .Where(bucket => DateOnly.TryParse(bucket.StartDate, out var date) && date >= cutoffDate)
            .Sum(bucket => bucket.TotalTokens ?? 0);
    }

    private static DateOnly GetCutoffDate(DateTimeOffset now)
    {
        return DateOnly.FromDateTime(now.ToLocalTime().DateTime).AddDays(-(UsageWindowDays - 1));
    }

    private static DateTimeOffset GetCutoffStartUtc(DateOnly cutoffDate)
    {
        var localDateTime = cutoffDate.ToDateTime(TimeOnly.MinValue);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }

    private static string NormalizeProjectPath(string path)
    {
        return LocalProjectPathNormalizer.Normalize(path);
    }

    private static string GetBaseDisplayName(string fullPath)
    {
        if (fullPath == "(unknown project)")
        {
            return "Unknown project";
        }

        return Path.GetFileName(fullPath) is { Length: > 0 } name
            ? name
            : fullPath;
    }

    private static string GetParentDisplayName(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrWhiteSpace(parent)
            ? "unknown"
            : Path.GetFileName(parent) is { Length: > 0 } name
                ? name
                : parent;
    }

    private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool StringEquals(JsonElement element, string name, string expected)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && string.Equals(property.GetString(), expected, StringComparison.Ordinal);
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("timestamp", out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            property.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var timestamp)
                ? timestamp
                : null;
    }

    private static long? ReadLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private sealed record ThreadRow(string Id, string RolloutPath, string Cwd);

    private sealed record RolloutTokenRecord(DateTimeOffset TimestampUtc, long LastTokenUsage, long? CumulativeTotalTokens);

    private sealed class ProjectUsageAggregate
    {
        public ProjectUsageAggregate(string fullPath)
        {
            FullPath = fullPath;
        }

        public string FullPath { get; }

        public long RawLocalTokens { get; set; }

        public int ThreadCount { get; set; }

        public long RawLast7Days { get; private set; }

        public long RawPrevious7Days { get; private set; }

        public HashSet<DateOnly> ActiveDaysLast7 { get; } = new();

        public Dictionary<DateOnly, long> DailyTokens { get; } = new();

        public Dictionary<string, ProjectThreadUsage> RecentThreadUsage { get; } = new(StringComparer.Ordinal);

        public ProjectBurnMoment? LargestRecentMoment { get; private set; }

        public void AddUsage(
            string threadId,
            IReadOnlyList<RolloutTokenRecord> tokenRecords,
            DateOnly recentWeekStart,
            DateOnly previousWeekStart)
        {
            foreach (var tokenRecord in tokenRecords)
            {
                RawLocalTokens += tokenRecord.LastTokenUsage;
                var localDate = DateOnly.FromDateTime(tokenRecord.TimestampUtc.ToLocalTime().DateTime);
                DailyTokens[localDate] = DailyTokens.GetValueOrDefault(localDate) + tokenRecord.LastTokenUsage;

                if (localDate >= recentWeekStart)
                {
                    RawLast7Days += tokenRecord.LastTokenUsage;
                    ActiveDaysLast7.Add(localDate);
                    if (!RecentThreadUsage.TryGetValue(threadId, out var threadUsage))
                    {
                        threadUsage = new ProjectThreadUsage(threadId);
                        RecentThreadUsage.Add(threadId, threadUsage);
                    }

                    threadUsage.RawTokens += tokenRecord.LastTokenUsage;
                    if (threadUsage.LatestTimestampUtc is null
                        || tokenRecord.TimestampUtc > threadUsage.LatestTimestampUtc.Value)
                    {
                        threadUsage.LatestTimestampUtc = tokenRecord.TimestampUtc;
                    }
                    if (LargestRecentMoment is null || tokenRecord.LastTokenUsage > LargestRecentMoment.TotalTokens)
                    {
                        LargestRecentMoment = new ProjectBurnMoment(threadId, tokenRecord.TimestampUtc, tokenRecord.LastTokenUsage);
                    }
                }
                else if (localDate >= previousWeekStart)
                {
                    RawPrevious7Days += tokenRecord.LastTokenUsage;
                }
            }
        }
    }

    private sealed class ProjectThreadUsage
    {
        public ProjectThreadUsage(string threadId)
        {
            ThreadId = threadId;
        }

        public string ThreadId { get; }

        public long RawTokens { get; set; }

        public DateTimeOffset? LatestTimestampUtc { get; set; }
    }

    private sealed record ProjectBurnMoment(string ThreadId, DateTimeOffset TimestampUtc, long TotalTokens);
}
