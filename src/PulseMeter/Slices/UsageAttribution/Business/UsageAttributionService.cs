using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PulseMeter.Shared.Projects;
using PulseMeter.Slices.UsageCollection.Models;
using PulseMeter.Slices.UsageAttribution.Models;

namespace PulseMeter.Slices.UsageAttribution.Business;

public interface IUsageAttributionService
{
    Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed class UsageAttributionService : IUsageAttributionService
{
    private const int UsageWindowDays = 30;
    private readonly string _codexHome;
    private readonly int _maxSessions;
    private readonly object _rolloutCacheLock = new();
    private readonly Dictionary<string, RolloutCacheEntry> _rolloutCache = new(StringComparer.OrdinalIgnoreCase);

    public UsageAttributionService(string? codexHome = null, int maxSessions = 5)
    {
        _codexHome = codexHome ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
        _maxSessions = Math.Max(1, maxSessions);
    }

    public Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetUsageAttribution(dailyBuckets, now, cancellationToken));
    }

    private UsageAttributionSnapshot GetUsageAttribution(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoffDate = GetCutoffDate(now);
        var accountTotal = GetAccountTotalForWindow(dailyBuckets, cutoffDate);
        if (accountTotal <= 0)
        {
            return UsageAttributionSnapshot.Empty;
        }

        var databasePath = Path.Combine(_codexHome, "state_5.sqlite");
        if (!File.Exists(databasePath))
        {
            return UsageAttributionSnapshot.Empty;
        }

        var sessionAggregates = new List<SessionAggregate>();
        foreach (var thread in ReadThreads(databasePath, cutoffDate))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!LocalProjectPathNormalizer.IsUserProjectPath(thread.Cwd))
            {
                continue;
            }

            var rolloutPath = ResolveRolloutPath(thread.RolloutPath);
            var events = ReadRolloutEvents(rolloutPath, cutoffDate, cancellationToken);
            if (events.Count == 0)
            {
                continue;
            }

            sessionAggregates.Add(new SessionAggregate(thread, events));
        }
        var rawTotal = sessionAggregates.Sum(session => session.RawLocalTokens);
        if (rawTotal <= 0)
        {
            return UsageAttributionSnapshot.Empty;
        }

        var scale = accountTotal / (double)rawTotal;
        var sessions = sessionAggregates
            .Select(session => ToSessionRow(session, scale, rawTotal))
            .OrderByDescending(row => row.EstimatedTokens)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(_maxSessions)
            .ToList();

        return new UsageAttributionSnapshot
        {
            Sessions = sessions,
            AccountWindowTokens = accountTotal,
            RawLocalTokens = rawTotal,
            EstimatedAttributedTokens = sessions.Sum(row => row.EstimatedTokens),
            LastUpdatedUtc = now
        };
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
            var columns = ReadThreadColumns(connection);
            var titleExpression = columns.Contains("title") ? "title" : "null";
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                select id, rollout_path, cwd, updated_at, {titleExpression}
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
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4)));
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

    private static HashSet<string> ReadThreadColumns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "pragma table_info(threads);";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (!reader.IsDBNull(1))
            {
                columns.Add(reader.GetString(1));
            }
        }

        return columns;
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

    private IReadOnlyList<UsageAttributionEvent> ReadRolloutEvents(
        string rolloutPath,
        DateOnly cutoffDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rolloutPath) || !File.Exists(rolloutPath))
        {
            return Array.Empty<UsageAttributionEvent>();
        }

        var fileInfo = new FileInfo(rolloutPath);
        lock (_rolloutCacheLock)
        {
            if (_rolloutCache.TryGetValue(rolloutPath, out var cached)
                && cached.CutoffDate == cutoffDate
                && cached.Length == fileInfo.Length
                && cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc)
            {
                return cached.Events;
            }
        }

        var events = new List<UsageAttributionEvent>();
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

                if (ReadTokenCountLine(line, cutoffDate) is { } usageEvent
                    && (usageEvent.CumulativeTotalTokens is not long cumulativeTotal
                        || cumulativeTotals.Add(cumulativeTotal)))
                {
                    events.Add(usageEvent);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        lock (_rolloutCacheLock)
        {
            _rolloutCache[rolloutPath] = new RolloutCacheEntry(
                cutoffDate,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                events);
        }

        return events;
    }

    private static UsageAttributionEvent? ReadTokenCountLine(string line, DateOnly cutoffDate)
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

            var totalTokens = ReadLong(usage, "total_tokens") ?? ReadLong(usage, "totalTokens");
            if (totalTokens is null or <= 0)
            {
                return null;
            }

            var cumulativeTotalTokens = TryGetObject(info, "total_token_usage", out var totalUsage)
                ? ReadLong(totalUsage, "total_tokens") ?? ReadLong(totalUsage, "totalTokens")
                : null;

            return new UsageAttributionEvent(
                timestamp.Value.ToUniversalTime(),
                totalTokens.Value,
                ReadLong(usage, "input_tokens") ?? ReadLong(usage, "inputTokens"),
                ReadLong(usage, "output_tokens") ?? ReadLong(usage, "outputTokens"),
                ReadLong(usage, "cached_input_tokens") ?? ReadLong(usage, "cachedInputTokens"),
                ReadLong(usage, "reasoning_tokens") ?? ReadLong(usage, "reasoningTokens") ?? ReadLong(usage, "reasoning_output_tokens"),
                cumulativeTotalTokens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UsageAttributionSessionRow ToSessionRow(
        SessionAggregate session,
        double scale,
        long rawTotal)
    {
        return new UsageAttributionSessionRow(
            DisplayNameFor(session.Thread),
            session.Thread.Id,
            GetProjectDisplayName(session.Thread.Cwd),
            NormalizeProjectPath(session.Thread.Cwd),
            session.RawLocalTokens,
            ScaleTokens(session.RawLocalTokens, scale),
            Math.Round(session.RawLocalTokens / (double)rawTotal * 100, 1),
            SumNullable(session.Events.Select(item => item.InputTokens)),
            SumNullable(session.Events.Select(item => item.OutputTokens)),
            SumNullable(session.Events.Select(item => item.CachedInputTokens)),
            SumNullable(session.Events.Select(item => item.ReasoningTokens)),
            session.Thread.UpdatedAtUtc,
            session.Events.Max(item => item.TimestampUtc));
    }

    private static long ScaleTokens(long rawTokens, double scale)
    {
        return (long)Math.Round(rawTokens * scale);
    }

    private static long? SumNullable(IEnumerable<long?> values)
    {
        var hasAny = false;
        long total = 0;
        foreach (var value in values)
        {
            if (value is not long tokens)
            {
                continue;
            }

            hasAny = true;
            total += tokens;
        }

        return hasAny ? total : null;
    }

    private static string DisplayNameFor(ThreadRow thread)
    {
        return $"{FallbackChatPrefix(thread)} · {FormatChatSuffix(thread)}";
    }

    private static string FallbackChatPrefix(ThreadRow thread)
    {
        var project = GetProjectDisplayName(thread.Cwd);
        return string.Equals(project, "Unknown project", StringComparison.Ordinal)
            ? "Local chat"
            : $"{project} chat";
    }


    private static string FormatChatSuffix(ThreadRow thread)
    {
        if (thread.UpdatedAtUtc is DateTimeOffset updatedAt)
        {
            return updatedAt.ToUniversalTime().ToString("dd MMM HH:mm", CultureInfo.InvariantCulture);
        }

        return "time unknown";
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

    private static string GetProjectDisplayName(string path)
    {
        return LocalProjectPathNormalizer.GetDisplayName(path);
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

    private sealed record ThreadRow(
        string Id,
        string RolloutPath,
        string Cwd,
        DateTimeOffset? UpdatedAtUtc,
        string Title);

    private sealed record UsageAttributionEvent(
        DateTimeOffset TimestampUtc,
        long TotalTokens,
        long? InputTokens,
        long? OutputTokens,
        long? CachedInputTokens,
        long? ReasoningTokens,
        long? CumulativeTotalTokens);

    private sealed record RolloutCacheEntry(
        DateOnly CutoffDate,
        long Length,
        DateTime LastWriteTimeUtc,
        IReadOnlyList<UsageAttributionEvent> Events);

    private sealed class SessionAggregate
    {
        public SessionAggregate(ThreadRow thread, IReadOnlyList<UsageAttributionEvent> events)
        {
            Thread = thread;
            Events = events;
            RawLocalTokens = events.Sum(item => item.TotalTokens);
        }

        public ThreadRow Thread { get; }

        public IReadOnlyList<UsageAttributionEvent> Events { get; }

        public long RawLocalTokens { get; }
    }
}
