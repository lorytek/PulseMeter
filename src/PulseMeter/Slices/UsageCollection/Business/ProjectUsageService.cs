using System.IO;
using System.Globalization;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;
using Microsoft.Data.Sqlite;

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

            var rolloutPath = ResolveRolloutPath(thread.RolloutPath);
            var rawTokens = ReadRolloutTokens(rolloutPath, cutoffDate, cancellationToken);
            if (rawTokens <= 0)
            {
                continue;
            }

            var fullPath = NormalizeProjectPath(thread.Cwd);
            if (!aggregates.TryGetValue(fullPath, out var aggregate))
            {
                aggregate = new ProjectUsageAggregate(fullPath);
                aggregates.Add(fullPath, aggregate);
            }

            aggregate.RawLocalTokens += rawTokens;
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

    private static long ReadRolloutTokens(string rolloutPath, DateOnly cutoffDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rolloutPath) || !File.Exists(rolloutPath))
        {
            return 0;
        }

        long total = 0;
        try
        {
            foreach (var line in File.ReadLines(rolloutPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!line.Contains("\"token_count\"", StringComparison.Ordinal))
                {
                    continue;
                }

                total += ReadTokenCountLine(line, cutoffDate);
            }
        }
        catch (IOException)
        {
            return total;
        }
        catch (UnauthorizedAccessException)
        {
            return total;
        }

        return total;
    }

    private static long ReadTokenCountLine(string line, DateOnly cutoffDate)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!StringEquals(root, "type", "event_msg"))
            {
                return 0;
            }

            if (!TryGetObject(root, "payload", out var payload)
                || !StringEquals(payload, "type", "token_count")
                || !TryGetObject(payload, "info", out var info)
                || !TryGetObject(info, "last_token_usage", out var usage))
            {
                return 0;
            }

            var timestamp = ReadTimestamp(root);
            if (timestamp is null || DateOnly.FromDateTime(timestamp.Value.ToLocalTime().DateTime) < cutoffDate)
            {
                return 0;
            }

            return ReadLong(usage, "total_tokens") ?? 0;
        }
        catch (JsonException)
        {
            return 0;
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
        var baseDisplayName = GetBaseDisplayName(aggregate.FullPath);
        var displayName = baseNameCounts.TryGetValue(baseDisplayName, out var count) && count > 1
            ? $"{baseDisplayName} ({GetParentDisplayName(aggregate.FullPath)})"
            : baseDisplayName;

        return new ProjectUsageRow(
            displayName,
            aggregate.FullPath,
            estimatedTokens,
            aggregate.RawLocalTokens,
            aggregate.ThreadCount,
            Math.Round(sharePercent, 1));
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
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(unknown project)";
        }

        var normalized = path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path[4..]
            : path;

        try
        {
            normalized = Path.GetFullPath(normalized);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

    private sealed class ProjectUsageAggregate
    {
        public ProjectUsageAggregate(string fullPath)
        {
            FullPath = fullPath;
        }

        public string FullPath { get; }

        public long RawLocalTokens { get; set; }

        public int ThreadCount { get; set; }
    }
}
