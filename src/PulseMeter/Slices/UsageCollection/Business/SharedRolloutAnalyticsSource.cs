using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PulseMeter.Shared.Projects;

namespace PulseMeter.Slices.UsageCollection.Business;

/// <summary>
/// Reads compact token summaries shared by the usage projections. Raw rollout JSON is never retained.
/// </summary>
public sealed class SharedRolloutAnalyticsSource
{
    private readonly string _codexHome;
    private readonly object _generationLock = new();
    private readonly Dictionary<string, RolloutCacheEntry> _rolloutCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RateLimitRolloutCacheEntry> _rateLimitRolloutCache = new(StringComparer.OrdinalIgnoreCase);
    private DatabaseSnapshot? _databaseSnapshot;
    private int _rolloutParseCount;

    public SharedRolloutAnalyticsSource(string? codexHome = null)
    {
        _codexHome = codexHome ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
    }

    internal int RolloutParseCount => Volatile.Read(ref _rolloutParseCount);

    internal int RolloutCacheEntryCount
    {
        get
        {
            lock (_generationLock)
            {
                return _rolloutCache.Count;
            }
        }
    }

    public Task<IReadOnlyList<SharedRolloutSessionSummary>> GetSessionSummariesAsync(
        DateOnly cutoffDate,
        CancellationToken cancellationToken = default)
    {
        lock (_generationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetSessionSummaries(cutoffDate, cancellationToken));
        }
    }

    public Task<IReadOnlyList<RateLimitHistoryPoint>> GetRateLimitHistoryAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_generationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetRateLimitHistory(cutoffUtc.ToUniversalTime(), cancellationToken));
        }
    }

    private IReadOnlyList<RateLimitHistoryPoint> GetRateLimitHistory(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var cutoffDate = DateOnly.FromDateTime(cutoffUtc.ToLocalTime().DateTime);
        var databasePath = Path.Combine(_codexHome, "state_5.sqlite");
        if (!TryGetFileSignature(databasePath, cutoffDate, out var databaseSignature))
        {
            _rateLimitRolloutCache.Clear();
            return Array.Empty<RateLimitHistoryPoint>();
        }

        var threads = _databaseSnapshot is { Signature: var cachedSignature, Threads: var cachedThreads }
            && cachedSignature == databaseSignature
            ? cachedThreads
            : ReadAndCacheThreads(databasePath, databaseSignature, cutoffDate);

        var observedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var history = new List<RateLimitHistoryPoint>();
        foreach (var thread in threads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rolloutPath = ResolveRolloutPath(thread.RolloutPath);
            if (TryGetRateLimitHistory(
                    rolloutPath,
                    cutoffDate,
                    cancellationToken,
                    observedPaths,
                    out var observations))
            {
                history.AddRange(observations.Where(point => point.ObservedAtUtc >= cutoffUtc));
            }
        }

        foreach (var path in _rateLimitRolloutCache.Keys
                     .Where(path => !observedPaths.Contains(path))
                     .ToArray())
        {
            _rateLimitRolloutCache.Remove(path);
        }

        return Array.AsReadOnly(history
            .Distinct()
            .OrderBy(point => point.ObservedAtUtc)
            .ToArray());
    }

    private IReadOnlyList<SharedRolloutSessionSummary> GetSessionSummaries(
        DateOnly cutoffDate,
        CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(_codexHome, "state_5.sqlite");
        if (!TryGetFileSignature(databasePath, cutoffDate, out var databaseSignature))
        {
            _databaseSnapshot = null;
            _rolloutCache.Clear();
            return Array.Empty<SharedRolloutSessionSummary>();
        }

        var threads = _databaseSnapshot is { Signature: var cachedSignature, Threads: var cachedThreads }
            && cachedSignature == databaseSignature
            ? cachedThreads
            : ReadAndCacheThreads(databasePath, databaseSignature, cutoffDate);

        var observedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<SharedRolloutSessionSummary>(threads.Count);
        foreach (var thread in threads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LocalProjectPathNormalizer.IsUserProjectPath(thread.Cwd))
            {
                continue;
            }

            var rolloutPath = ResolveRolloutPath(thread.RolloutPath);
            if (!TryGetRolloutSummaries(rolloutPath, cutoffDate, cancellationToken, observedPaths, out var tokenSummaries)
                || tokenSummaries.Count == 0)
            {
                continue;
            }

            summaries.Add(new SharedRolloutSessionSummary(
                thread.Id,
                thread.Cwd,
                thread.UpdatedAtUtc,
                thread.Title,
                tokenSummaries));
        }

        PruneUnobservedRollouts(observedPaths);
        return Array.AsReadOnly(summaries.ToArray());
    }

    private IReadOnlyList<ThreadRow> ReadAndCacheThreads(
        string databasePath,
        FileSignature databaseSignature,
        DateOnly cutoffDate)
    {
        var threads = ReadThreads(databasePath, cutoffDate);
        _databaseSnapshot = new DatabaseSnapshot(databaseSignature, threads);
        return threads;
    }

    private static IReadOnlyList<ThreadRow> ReadThreads(string databasePath, DateOnly cutoffDate)
    {
        try
        {
            var rows = new List<ThreadRow>();
            var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly };
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
            command.Parameters.AddWithValue("$cutoff", GetCutoffStartUtc(cutoffDate).ToUnixTimeSeconds());
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

            return rows.ToArray();
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

    private bool TryGetRolloutSummaries(
        string rolloutPath,
        DateOnly cutoffDate,
        CancellationToken cancellationToken,
        ISet<string> observedPaths,
        out IReadOnlyList<SharedRolloutTokenSummary> summaries)
    {
        summaries = Array.Empty<SharedRolloutTokenSummary>();
        if (string.IsNullOrWhiteSpace(rolloutPath))
        {
            return false;
        }

        string normalizedPath;
        try { normalizedPath = Path.GetFullPath(rolloutPath); }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }

        if (!TryGetFileSignature(normalizedPath, cutoffDate, out var signature))
        {
            _rolloutCache.Remove(normalizedPath);
            return false;
        }

        observedPaths.Add(normalizedPath);

        if (_rolloutCache.TryGetValue(normalizedPath, out var cached) && cached.Signature == signature)
        {
            summaries = cached.Summaries;
            return true;
        }

        // A live rollout can change while being read. Retry once, otherwise omit it rather than
        // publishing a mixed parse; a later generation will pick it up.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (!TryParseRollout(normalizedPath, cutoffDate, cancellationToken, out var parsed))
            {
                return false;
            }

            if (TryGetFileSignature(normalizedPath, cutoffDate, out var currentSignature)
                && currentSignature == signature)
            {
                summaries = parsed;
                _rolloutCache[normalizedPath] = new RolloutCacheEntry(signature, parsed);
                return true;
            }

            signature = currentSignature;
        }

        return false;
    }

    private bool TryParseRollout(
        string rolloutPath,
        DateOnly cutoffDate,
        CancellationToken cancellationToken,
        out IReadOnlyList<SharedRolloutTokenSummary> summaries)
    {
        var parsed = new List<SharedRolloutTokenSummary>();
        var cumulativeTotals = new HashSet<long>();
        try
        {
            Interlocked.Increment(ref _rolloutParseCount);
            foreach (var line in File.ReadLines(rolloutPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!line.Contains("\"token_count\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReadTokenCountLine(line, cutoffDate) is { } summary
                    && (summary.CumulativeTotalTokens is not long total || cumulativeTotals.Add(total)))
                {
                    parsed.Add(summary);
                }
            }
        }
        catch (IOException)
        {
            summaries = Array.Empty<SharedRolloutTokenSummary>();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            summaries = Array.Empty<SharedRolloutTokenSummary>();
            return false;
        }

        summaries = Array.AsReadOnly(parsed.ToArray());
        return true;
    }

    private bool TryGetRateLimitHistory(
        string rolloutPath,
        DateOnly cutoffDate,
        CancellationToken cancellationToken,
        ISet<string> observedPaths,
        out IReadOnlyList<RateLimitHistoryPoint> observations)
    {
        observations = Array.Empty<RateLimitHistoryPoint>();
        if (string.IsNullOrWhiteSpace(rolloutPath))
        {
            return false;
        }

        string normalizedPath;
        try { normalizedPath = Path.GetFullPath(rolloutPath); }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }

        if (!TryGetFileSignature(normalizedPath, cutoffDate, out var signature))
        {
            _rateLimitRolloutCache.Remove(normalizedPath);
            return false;
        }

        observedPaths.Add(normalizedPath);
        if (_rateLimitRolloutCache.TryGetValue(normalizedPath, out var cached)
            && cached.Signature == signature)
        {
            observations = cached.Observations;
            return true;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (!TryParseRateLimitHistory(normalizedPath, cutoffDate, cancellationToken, out var parsed))
            {
                return false;
            }

            if (TryGetFileSignature(normalizedPath, cutoffDate, out var currentSignature)
                && currentSignature == signature)
            {
                observations = parsed;
                _rateLimitRolloutCache[normalizedPath] = new RateLimitRolloutCacheEntry(signature, parsed);
                return true;
            }

            signature = currentSignature;
        }

        return false;
    }

    private static bool TryParseRateLimitHistory(
        string rolloutPath,
        DateOnly cutoffDate,
        CancellationToken cancellationToken,
        out IReadOnlyList<RateLimitHistoryPoint> observations)
    {
        var parsed = new List<RateLimitHistoryPoint>();
        try
        {
            foreach (var line in File.ReadLines(rolloutPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!line.Contains("\"rate_limits\"", StringComparison.Ordinal))
                {
                    continue;
                }

                parsed.AddRange(ReadRateLimitLine(line, cutoffDate));
            }
        }
        catch (IOException)
        {
            observations = Array.Empty<RateLimitHistoryPoint>();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            observations = Array.Empty<RateLimitHistoryPoint>();
            return false;
        }

        observations = Array.AsReadOnly(parsed.ToArray());
        return true;
    }

    private static IReadOnlyList<RateLimitHistoryPoint> ReadRateLimitLine(string line, DateOnly cutoffDate)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!StringEquals(root, "type", "event_msg")
                || !TryGetObject(root, "payload", out var payload)
                || !StringEquals(payload, "type", "token_count")
                || !TryGetObject(payload, "rate_limits", out var rateLimits))
            {
                return Array.Empty<RateLimitHistoryPoint>();
            }

            var timestamp = ReadTimestamp(root);
            if (timestamp is null || DateOnly.FromDateTime(timestamp.Value.ToLocalTime().DateTime) < cutoffDate)
            {
                return Array.Empty<RateLimitHistoryPoint>();
            }

            var limitKey = ReadString(rateLimits, "limit_id") ?? ReadString(rateLimits, "limitId");
            if (string.IsNullOrWhiteSpace(limitKey))
            {
                return Array.Empty<RateLimitHistoryPoint>();
            }

            var points = new List<RateLimitHistoryPoint>(2);
            AddRateLimitPoint(points, rateLimits, "primary", limitKey, timestamp.Value);
            AddRateLimitPoint(points, rateLimits, "secondary", limitKey, timestamp.Value);
            return points;
        }
        catch (JsonException)
        {
            return Array.Empty<RateLimitHistoryPoint>();
        }
    }

    private static void AddRateLimitPoint(
        ICollection<RateLimitHistoryPoint> points,
        JsonElement rateLimits,
        string propertyName,
        string limitKey,
        DateTimeOffset observedAtUtc)
    {
        if (!TryGetObject(rateLimits, propertyName, out var window))
        {
            return;
        }

        var usedPercent = ReadDouble(window, "used_percent") ?? ReadDouble(window, "usedPercent");
        var windowMinutes = ReadLong(window, "window_minutes") ?? ReadLong(window, "windowMinutes");
        var resetsAtUnix = ReadLong(window, "resets_at") ?? ReadLong(window, "resetsAt");
        if (usedPercent is not double used
            || !double.IsFinite(used)
            || used is < 0 or > 100
            || windowMinutes is not long minutes
            || minutes is <= 0 or > int.MaxValue
            || resetsAtUnix is not long resetSeconds)
        {
            return;
        }

        DateTimeOffset resetsAtUtc;
        try { resetsAtUtc = DateTimeOffset.FromUnixTimeSeconds(resetSeconds); }
        catch (ArgumentOutOfRangeException) { return; }

        points.Add(new RateLimitHistoryPoint(
            limitKey,
            (int)minutes,
            used,
            resetsAtUtc,
            observedAtUtc.ToUniversalTime()));
    }

    private static SharedRolloutTokenSummary? ReadTokenCountLine(string line, DateOnly cutoffDate)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!StringEquals(root, "type", "event_msg")
                || !TryGetObject(root, "payload", out var payload)
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

            return new SharedRolloutTokenSummary(
                timestamp.Value.ToUniversalTime(), totalTokens.Value,
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

    private static bool TryGetFileSignature(string path, DateOnly cutoffDate, out FileSignature signature)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) { signature = default; return false; }
            signature = new FileSignature(file.Length, file.LastWriteTimeUtc, cutoffDate);
            return true;
        }
        catch (IOException) { signature = default; return false; }
        catch (UnauthorizedAccessException) { signature = default; return false; }
    }

    private void PruneUnobservedRollouts(IReadOnlySet<string> observedPaths)
    {
        foreach (var path in _rolloutCache.Keys.Where(path => !observedPaths.Contains(path)).ToArray())
        {
            _rolloutCache.Remove(path);
        }
    }

    private string ResolveRolloutPath(string rolloutPath) => string.IsNullOrWhiteSpace(rolloutPath)
        ? string.Empty
        : Path.IsPathRooted(rolloutPath) ? rolloutPath : Path.Combine(_codexHome, rolloutPath);

    private static HashSet<string> ReadThreadColumns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "pragma table_info(threads);";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) if (!reader.IsDBNull(1)) columns.Add(reader.GetString(1));
        return columns;
    }

    private static DateTimeOffset GetCutoffStartUtc(DateOnly cutoffDate)
    {
        var localDateTime = cutoffDate.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime)).ToUniversalTime();
    }

    private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object) return true;
        value = default;
        return false;
    }

    private static bool StringEquals(JsonElement element, string name, string expected) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String && string.Equals(property.GetString(), expected, StringComparison.Ordinal);

    private static DateTimeOffset? ReadTimestamp(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("timestamp", out var property)
        && property.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp : null;

    private static long? ReadLong(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value) ? value : null;

    private static double? ReadDouble(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value) ? value : null;

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private sealed record ThreadRow(string Id, string RolloutPath, string Cwd, DateTimeOffset? UpdatedAtUtc, string Title);
    private readonly record struct FileSignature(long Length, DateTime LastWriteTimeUtc, DateOnly CutoffDate);
    private sealed record DatabaseSnapshot(FileSignature Signature, IReadOnlyList<ThreadRow> Threads);
    private sealed record RolloutCacheEntry(FileSignature Signature, IReadOnlyList<SharedRolloutTokenSummary> Summaries);
    private sealed record RateLimitRolloutCacheEntry(
        FileSignature Signature,
        IReadOnlyList<RateLimitHistoryPoint> Observations);
}
