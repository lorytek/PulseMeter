using Microsoft.Extensions.DependencyInjection;
using System.IO;
using PulseMeter.Shared.Projects;

namespace PulseMeter.Slices.UsageCollection.Business;

public interface IProjectUsageService
{
    Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(IReadOnlyList<DailyUsageBucket> dailyBuckets, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedRolloutSessionSummary>> GetSessionSummariesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SharedRolloutSessionSummary>>(Array.Empty<SharedRolloutSessionSummary>());

    Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken = default) =>
        GetProjectUsageAsync(dailyBuckets, now, cancellationToken);
}

public sealed class ProjectUsageService : IProjectUsageService
{
    private const int UsageWindowDays = 30;
    private readonly SharedRolloutAnalyticsSource _rolloutAnalyticsSource;
    private readonly int _maxRows;

    public ProjectUsageService(string? codexHome = null, int maxRows = 8)
        : this(new SharedRolloutAnalyticsSource(codexHome), maxRows)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ProjectUsageService(SharedRolloutAnalyticsSource rolloutAnalyticsSource, int maxRows = 8)
    {
        _rolloutAnalyticsSource = rolloutAnalyticsSource;
        _maxRows = Math.Max(1, maxRows);
    }

    internal int RolloutParseCount => _rolloutAnalyticsSource.RolloutParseCount;
    internal int RolloutCacheEntryCount => _rolloutAnalyticsSource.RolloutCacheEntryCount;

    public async Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = GetCutoffDate(now);
        if (GetAccountTotalForWindow(dailyBuckets, cutoffDate) <= 0)
        {
            return Array.Empty<ProjectUsageRow>();
        }

        var sessions = await GetSessionSummariesAsync(now, cancellationToken);
        return GetProjectUsage(dailyBuckets, now, cutoffDate, sessions, cancellationToken);
    }

    public Task<IReadOnlyList<SharedRolloutSessionSummary>> GetSessionSummariesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        _rolloutAnalyticsSource.GetSessionSummariesAsync(GetCutoffDate(now), cancellationToken);

    public Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = GetCutoffDate(now);
        return Task.FromResult(GetProjectUsage(dailyBuckets, now, cutoffDate, sessionSummaries, cancellationToken));
    }

    private IReadOnlyList<ProjectUsageRow> GetProjectUsage(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        DateOnly cutoffDate,
        IReadOnlyList<SharedRolloutSessionSummary> sessions,
        CancellationToken cancellationToken)
    {
        var accountTotal = GetAccountTotalForWindow(dailyBuckets, cutoffDate);
        if (accountTotal <= 0)
        {
            return Array.Empty<ProjectUsageRow>();
        }

        var today = DateOnly.FromDateTime(now.ToLocalTime().DateTime);
        var recentWeekStart = today.AddDays(-6);
        var previousWeekStart = today.AddDays(-13);
        var aggregates = new Dictionary<string, ProjectUsageAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = NormalizeProjectPath(session.Cwd);
            if (!aggregates.TryGetValue(fullPath, out var aggregate))
            {
                aggregate = new ProjectUsageAggregate(fullPath);
                aggregates.Add(fullPath, aggregate);
            }

            aggregate.AddUsage(session.ThreadId, session.TokenSummaries, recentWeekStart, previousWeekStart);
            aggregate.ThreadCount++;
        }

        var totalRawTokens = aggregates.Values.Sum(aggregate => aggregate.RawLocalTokens);
        if (totalRawTokens <= 0) return Array.Empty<ProjectUsageRow>();

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

    private static ProjectUsageRow ToProjectUsageRow(ProjectUsageAggregate aggregate, IReadOnlyDictionary<string, int> baseNameCounts, long totalRawTokens, long accountTotal)
    {
        var sharePercent = aggregate.RawLocalTokens / (double)totalRawTokens * 100;
        var scale = accountTotal / (double)totalRawTokens;
        var baseDisplayName = GetBaseDisplayName(aggregate.FullPath);
        var displayName = baseNameCounts.TryGetValue(baseDisplayName, out var count) && count > 1 ? $"{baseDisplayName} ({GetParentDisplayName(aggregate.FullPath)})" : baseDisplayName;
        var leadingChats = aggregate.RecentThreadUsage.Values.OrderByDescending(item => item.RawTokens).ThenBy(item => item.ThreadId, StringComparer.Ordinal).Take(2).ToList();
        var leadingChat = leadingChats.FirstOrDefault();
        var secondLeadingChat = leadingChats.Skip(1).FirstOrDefault();
        var largestMoment = aggregate.LargestRecentMoment;
        return new ProjectUsageRow(
            displayName, aggregate.FullPath, (long)Math.Round(accountTotal * (aggregate.RawLocalTokens / (double)totalRawTokens)), aggregate.RawLocalTokens,
            aggregate.ThreadCount, Math.Round(sharePercent, 1), ScaleTokens(aggregate.RawLast7Days, scale), ScaleTokens(aggregate.RawPrevious7Days, scale),
            aggregate.ActiveDaysLast7.Count, CountSpikeDays(aggregate.DailyTokens),
            leadingChat is null ? string.Empty : FormatChatDisplayName(displayName, leadingChat.LatestTimestampUtc ?? DateTimeOffset.UnixEpoch), leadingChat is null ? 0 : ScaleTokens(leadingChat.RawTokens, scale),
            secondLeadingChat is null ? string.Empty : FormatChatDisplayName(displayName, secondLeadingChat.LatestTimestampUtc ?? DateTimeOffset.UnixEpoch), secondLeadingChat is null ? 0 : ScaleTokens(secondLeadingChat.RawTokens, scale),
            largestMoment is null ? string.Empty : FormatChatDisplayName(displayName, largestMoment.TimestampUtc), largestMoment is null ? 0 : ScaleTokens(largestMoment.TotalTokens, scale), largestMoment?.TimestampUtc);
    }

    private static long ScaleTokens(long rawTokens, double scale) => (long)Math.Round(rawTokens * scale);
    private static string FormatChatDisplayName(string projectDisplayName, DateTimeOffset timestampUtc) => $"{projectDisplayName} chat - {timestampUtc.ToLocalTime():dd MMM HH:mm}";
    private static int CountSpikeDays(IReadOnlyDictionary<DateOnly, long> dailyTokens)
    {
        var activeDays = dailyTokens.Values.Where(tokens => tokens > 0).OrderBy(tokens => tokens).ToList();
        if (activeDays.Count == 0) return 0;
        var middle = activeDays.Count / 2;
        var median = activeDays.Count % 2 == 1 ? activeDays[middle] : (long)Math.Round((activeDays[middle - 1] + activeDays[middle]) / 2d);
        return median <= 0 ? 0 : activeDays.Count(tokens => tokens >= median * 1.5);
    }

    private static long GetAccountTotalForWindow(IReadOnlyList<DailyUsageBucket> dailyBuckets, DateOnly cutoffDate) => dailyBuckets.Where(bucket => DateOnly.TryParse(bucket.StartDate, out var date) && date >= cutoffDate).Sum(bucket => bucket.TotalTokens ?? 0);
    private static DateOnly GetCutoffDate(DateTimeOffset now) => DateOnly.FromDateTime(now.ToLocalTime().DateTime).AddDays(-(UsageWindowDays - 1));
    private static string NormalizeProjectPath(string path) => LocalProjectPathNormalizer.Normalize(path);
    private static string GetBaseDisplayName(string fullPath) => fullPath == "(unknown project)" ? "Unknown project" : Path.GetFileName(fullPath) is { Length: > 0 } name ? name : fullPath;
    private static string GetParentDisplayName(string fullPath) => Path.GetDirectoryName(fullPath) is { } parent && Path.GetFileName(parent) is { Length: > 0 } name ? name : "unknown";

    private sealed class ProjectUsageAggregate
    {
        public ProjectUsageAggregate(string fullPath) => FullPath = fullPath;
        public string FullPath { get; }
        public long RawLocalTokens { get; set; }
        public int ThreadCount { get; set; }
        public long RawLast7Days { get; private set; }
        public long RawPrevious7Days { get; private set; }
        public HashSet<DateOnly> ActiveDaysLast7 { get; } = [];
        public Dictionary<DateOnly, long> DailyTokens { get; } = [];
        public Dictionary<string, ProjectThreadUsage> RecentThreadUsage { get; } = new(StringComparer.Ordinal);
        public ProjectBurnMoment? LargestRecentMoment { get; private set; }

        public void AddUsage(string threadId, IReadOnlyList<SharedRolloutTokenSummary> tokenSummaries, DateOnly recentWeekStart, DateOnly previousWeekStart)
        {
            foreach (var summary in tokenSummaries)
            {
                RawLocalTokens += summary.TotalTokens;
                var localDate = DateOnly.FromDateTime(summary.TimestampUtc.ToLocalTime().DateTime);
                DailyTokens[localDate] = DailyTokens.GetValueOrDefault(localDate) + summary.TotalTokens;
                if (localDate >= recentWeekStart)
                {
                    RawLast7Days += summary.TotalTokens;
                    ActiveDaysLast7.Add(localDate);
                    if (!RecentThreadUsage.TryGetValue(threadId, out var threadUsage)) RecentThreadUsage[threadId] = threadUsage = new ProjectThreadUsage(threadId);
                    threadUsage.RawTokens += summary.TotalTokens;
                    if (threadUsage.LatestTimestampUtc is null || summary.TimestampUtc > threadUsage.LatestTimestampUtc.Value) threadUsage.LatestTimestampUtc = summary.TimestampUtc;
                    if (LargestRecentMoment is null || summary.TotalTokens > LargestRecentMoment.TotalTokens) LargestRecentMoment = new ProjectBurnMoment(threadId, summary.TimestampUtc, summary.TotalTokens);
                }
                else if (localDate >= previousWeekStart) RawPrevious7Days += summary.TotalTokens;
            }
        }
    }

    private sealed class ProjectThreadUsage(string threadId) { public string ThreadId { get; } = threadId; public long RawTokens { get; set; } public DateTimeOffset? LatestTimestampUtc { get; set; } }
    private sealed record ProjectBurnMoment(string ThreadId, DateTimeOffset TimestampUtc, long TotalTokens);
}
