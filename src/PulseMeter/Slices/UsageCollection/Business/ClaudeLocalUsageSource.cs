using System.Globalization;
using System.IO;
using System.Text.Json;
using PulseMeter.Platform.Diagnostics;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

/// <summary>
/// Token totals derived from local Claude Code session logs. Only token counts,
/// timestamps, session ids and working directories are retained; message content is
/// never kept or displayed.
/// </summary>
public sealed class ClaudeLocalUsage
{
    public static readonly ClaudeLocalUsage Empty = new();

    public IReadOnlyList<DailyUsageBucket> DailyBuckets { get; init; } = Array.Empty<DailyUsageBucket>();

    public IReadOnlyList<SharedRolloutSessionSummary> SessionSummaries { get; init; } =
        Array.Empty<SharedRolloutSessionSummary>();

    public ActivityEvidenceSnapshot ActivityEvidence { get; init; } = ActivityEvidenceSnapshot.Unavailable;

    public long? LifetimeTokens { get; init; }

    public long? PeakDailyTokens { get; init; }

    public int? CurrentStreakDays { get; init; }

    public int? LongestStreakDays { get; init; }

    public ThreadUsageSnapshot? RecentSession { get; init; }
}

public interface IClaudeLocalUsageSource
{
    Task<ClaudeLocalUsage> ReadAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads <c>~/.claude/projects/**/*.jsonl</c> session logs written by Claude Code.
/// Files are cached by size and last-write time so repeated syncs only re-read
/// sessions that changed.
/// </summary>
public sealed class ClaudeLocalUsageSource : IClaudeLocalUsageSource
{
    private const int AnalyticsWindowDays = 30;
    private const int DailyBucketDays = 90;

    private readonly string _projectsRoot;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, CachedSessionFile> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ClaudeLocalUsageSource(string? claudeHome = null)
    {
        _projectsRoot = Path.Combine(claudeHome ?? ClaudeHomeLocator.Resolve(), "projects");
    }

    public Task<ClaudeLocalUsage> ReadAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Read(now, cancellationToken), cancellationToken);
    }

    private ClaudeLocalUsage Read(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_projectsRoot))
        {
            return ClaudeLocalUsage.Empty;
        }

        var files = new List<SessionFileData>();
        var hadReadFailure = false;
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(_projectsRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            seenPaths.Add(path);
            try
            {
                files.Add(GetOrParse(path, cancellationToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                hadReadFailure = true;
                PrivacySafeDiagnostics.WriteFailure("claude session log could not be read", ex);
            }
        }

        lock (_cacheGate)
        {
            foreach (var stale in _cache.Keys.Where(key => !seenPaths.Contains(key)).ToList())
            {
                _cache.Remove(stale);
            }
        }

        return Aggregate(files, now, hadReadFailure);
    }

    private SessionFileData GetOrParse(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        var length = info.Length;
        var lastWriteUtc = info.LastWriteTimeUtc;

        lock (_cacheGate)
        {
            if (_cache.TryGetValue(path, out var cached)
                && cached.Length == length
                && cached.LastWriteUtc == lastWriteUtc)
            {
                return cached.Data;
            }
        }

        var data = ParseSessionFile(path, cancellationToken);
        lock (_cacheGate)
        {
            _cache[path] = new CachedSessionFile(length, lastWriteUtc, data);
        }

        return data;
    }

    internal static SessionFileData ParseSessionFile(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return ParseSessionLines(
            Path.GetFileNameWithoutExtension(path),
            ReadLines(reader, cancellationToken));
    }

    internal static SessionFileData ParseSessionLines(string fallbackSessionId, IEnumerable<string> lines)
    {
        string? sessionId = null;
        string? cwd = null;
        var entries = new List<UsageEntry>();

        foreach (var line in lines)
        {
            // Cheap pre-filter: only assistant turns that report token usage matter.
            if (string.IsNullOrWhiteSpace(line)
                || (cwd is not null && !line.Contains("\"usage\"", StringComparison.Ordinal)))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                cwd ??= ReadString(root, "cwd");
                sessionId ??= ReadString(root, "sessionId");

                if (!root.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("usage", out var usage)
                    || usage.ValueKind != JsonValueKind.Object
                    || ReadTimestamp(root) is not DateTimeOffset timestamp)
                {
                    continue;
                }

                var input = ReadLong(usage, "input_tokens");
                var output = ReadLong(usage, "output_tokens");
                var cacheCreation = ReadLong(usage, "cache_creation_input_tokens");
                var cacheRead = ReadLong(usage, "cache_read_input_tokens");
                if (input + output + cacheCreation + cacheRead <= 0)
                {
                    continue;
                }

                var messageId = ReadString(message, "id");
                var requestId = ReadString(root, "requestId");
                var dedupeKey = messageId is null && requestId is null
                    ? null
                    : $"{messageId}:{requestId}";

                entries.Add(new UsageEntry(timestamp, input, output, cacheCreation, cacheRead, dedupeKey));
            }
            catch (JsonException)
            {
                // A partially written trailing line is expected while Claude Code is running.
            }
        }

        return new SessionFileData(sessionId ?? fallbackSessionId, cwd ?? string.Empty, entries);
    }

    internal static ClaudeLocalUsage Aggregate(
        IReadOnlyList<SessionFileData> files,
        DateTimeOffset now,
        bool hadReadFailure = false)
    {
        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var analyticsCutoff = today.AddDays(-(AnalyticsWindowDays - 1));
        var dailyCutoff = today.AddDays(-(DailyBucketDays - 1));
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var dailyInput = new Dictionary<DateOnly, long>();
        var dailyOutput = new Dictionary<DateOnly, long>();
        var activityHours = new HashSet<DateTimeOffset>();
        var sessions = new Dictionary<string, SessionAccumulator>(StringComparer.Ordinal);
        long lifetime = 0;

        foreach (var file in files)
        {
            foreach (var entry in file.Entries.OrderBy(entry => entry.TimestampUtc))
            {
                // Claude Code repeats a streamed message once per content block, and resumed
                // sessions copy earlier turns, so the same request can appear several times.
                if (entry.DedupeKey is not null && !seenKeys.Add(entry.DedupeKey))
                {
                    continue;
                }

                lifetime += entry.TotalTokens;
                var localDate = DateOnly.FromDateTime(entry.TimestampUtc.ToLocalTime().DateTime);
                if (localDate >= dailyCutoff)
                {
                    dailyInput[localDate] = dailyInput.GetValueOrDefault(localDate) + entry.InputTotal;
                    dailyOutput[localDate] = dailyOutput.GetValueOrDefault(localDate) + entry.OutputTokens;
                }

                if (localDate < analyticsCutoff)
                {
                    continue;
                }

                var utc = entry.TimestampUtc.ToUniversalTime();
                activityHours.Add(new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero));

                if (!sessions.TryGetValue(file.SessionId, out var session))
                {
                    session = new SessionAccumulator(file.SessionId, file.Cwd);
                    sessions.Add(file.SessionId, session);
                }

                session.Add(entry);
            }
        }

        var dailyTotals = dailyInput.Keys
            .Union(dailyOutput.Keys)
            .OrderBy(date => date)
            .Select(date => (Date: date, Input: dailyInput.GetValueOrDefault(date), Output: dailyOutput.GetValueOrDefault(date)))
            .ToList();
        var dailyBuckets = dailyTotals
            .Select(day => new DailyUsageBucket
            {
                StartDate = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Tokens = day.Input + day.Output,
                InputTokens = day.Input,
                OutputTokens = day.Output
            })
            .ToList();
        var activeDates = dailyTotals.Where(day => day.Input + day.Output > 0).Select(day => day.Date).ToList();
        var recent = sessions.Values
            .OrderByDescending(session => session.LatestUtc)
            .FirstOrDefault();

        return new ClaudeLocalUsage
        {
            DailyBuckets = dailyBuckets,
            SessionSummaries = sessions.Values
                .Select(session => session.ToSummary())
                .OrderByDescending(summary => summary.UpdatedAtUtc)
                .ToList(),
            ActivityEvidence = new ActivityEvidenceSnapshot
            {
                Coverage = hadReadFailure ? ActivityEvidenceCoverage.Partial : ActivityEvidenceCoverage.Available,
                UtcHours = activityHours.OrderBy(hour => hour).ToList()
            },
            LifetimeTokens = files.Count == 0 ? null : lifetime,
            PeakDailyTokens = dailyBuckets.Count == 0 ? null : dailyBuckets.Max(bucket => bucket.Tokens ?? 0),
            CurrentStreakDays = files.Count == 0 ? null : CountCurrentStreak(activeDates, today),
            LongestStreakDays = files.Count == 0 ? null : CountLongestStreak(activeDates),
            RecentSession = recent?.ToThreadUsage()
        };
    }

    private static int CountCurrentStreak(IReadOnlyList<DateOnly> activeDates, DateOnly today)
    {
        var active = activeDates.ToHashSet();
        var day = active.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (active.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }

    private static int CountLongestStreak(IReadOnlyList<DateOnly> orderedActiveDates)
    {
        var longest = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var date in orderedActiveDates)
        {
            current = previous is DateOnly prior && prior.AddDays(1) == date ? current + 1 : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }

        return longest;
    }

    private static IEnumerable<string> ReadLines(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long ReadLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
        && number > 0
            ? number
            : 0;

    private static DateTimeOffset? ReadTimestamp(JsonElement root) =>
        ReadString(root, "timestamp") is { } text
        && DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : null;

    internal sealed record UsageEntry(
        DateTimeOffset TimestampUtc,
        long InputTokens,
        long OutputTokens,
        long CacheCreationTokens,
        long CacheReadTokens,
        string? DedupeKey)
    {
        public long InputTotal => InputTokens + CacheCreationTokens + CacheReadTokens;

        public long TotalTokens => InputTotal + OutputTokens;
    }

    internal sealed record SessionFileData(string SessionId, string Cwd, IReadOnlyList<UsageEntry> Entries);

    private sealed record CachedSessionFile(long Length, DateTime LastWriteUtc, SessionFileData Data);

    private sealed class SessionAccumulator(string sessionId, string cwd)
    {
        private readonly List<SharedRolloutTokenSummary> _tokens = [];
        private long _input;
        private long _output;
        private long _cumulative;

        public DateTimeOffset LatestUtc { get; private set; } = DateTimeOffset.MinValue;

        public void Add(UsageEntry entry)
        {
            _input += entry.InputTotal;
            _output += entry.OutputTokens;
            _cumulative += entry.TotalTokens;
            _tokens.Add(new SharedRolloutTokenSummary(
                entry.TimestampUtc,
                entry.TotalTokens,
                entry.InputTotal,
                entry.OutputTokens,
                entry.CacheReadTokens,
                ReasoningTokens: null,
                _cumulative));
            if (entry.TimestampUtc > LatestUtc)
            {
                LatestUtc = entry.TimestampUtc;
            }
        }

        public SharedRolloutSessionSummary ToSummary() =>
            new(sessionId, cwd, LatestUtc, string.Empty, _tokens.ToList());

        public ThreadUsageSnapshot ToThreadUsage() => new()
        {
            ThreadId = sessionId,
            ThreadName = string.IsNullOrWhiteSpace(cwd) ? null : Path.GetFileName(cwd.TrimEnd('\\', '/')),
            InputTokens = _input,
            OutputTokens = _output,
            TotalTokens = _input + _output,
            LastUpdatedUtc = LatestUtc,
            IsExactCurrentDesktopThread = false
        };
    }
}
