using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Shared.Projects;
using PulseMeter.Slices.UsageCollection.Models;
using PulseMeter.Slices.UsageAttribution.Models;

namespace PulseMeter.Slices.UsageAttribution.Business;

public interface IUsageAttributionService
{
    Task<UsageAttributionSnapshot> GetUsageAttributionAsync(IReadOnlyList<DailyUsageBucket> dailyBuckets, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken = default) =>
        GetUsageAttributionAsync(dailyBuckets, now, cancellationToken);
}

public sealed class UsageAttributionService : IUsageAttributionService
{
    private const int UsageWindowDays = 30;
    private readonly SharedRolloutAnalyticsSource _rolloutAnalyticsSource;
    private readonly int _maxSessions;

    public UsageAttributionService(string? codexHome = null, int maxSessions = 5)
        : this(new SharedRolloutAnalyticsSource(codexHome), maxSessions)
    {
    }

    [ActivatorUtilitiesConstructor]
    public UsageAttributionService(SharedRolloutAnalyticsSource rolloutAnalyticsSource, int maxSessions = 5)
    {
        _rolloutAnalyticsSource = rolloutAnalyticsSource;
        _maxSessions = Math.Max(1, maxSessions);
    }

    public async Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = GetCutoffDate(now);
        if (GetAccountTotalForWindow(dailyBuckets, cutoffDate) <= 0)
        {
            return UsageAttributionSnapshot.Empty;
        }

        var sessionSummaries = await _rolloutAnalyticsSource.GetSessionSummariesAsync(cutoffDate, cancellationToken);
        return GetUsageAttribution(dailyBuckets, now, cutoffDate, sessionSummaries, cancellationToken);
    }

    public Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = GetCutoffDate(now);
        return Task.FromResult(GetUsageAttribution(dailyBuckets, now, cutoffDate, sessionSummaries, cancellationToken));
    }

    private UsageAttributionSnapshot GetUsageAttribution(
        IReadOnlyList<DailyUsageBucket> dailyBuckets,
        DateTimeOffset now,
        DateOnly cutoffDate,
        IReadOnlyList<SharedRolloutSessionSummary> sessionSummaries,
        CancellationToken cancellationToken)
    {
        var accountTotal = GetAccountTotalForWindow(dailyBuckets, cutoffDate);
        if (accountTotal <= 0) return UsageAttributionSnapshot.Empty;

        cancellationToken.ThrowIfCancellationRequested();
        var sessions = sessionSummaries
            .Select(summary => new SessionAggregate(summary))
            .ToList();
        var rawTotal = sessions.Sum(session => session.RawLocalTokens);
        if (rawTotal <= 0) return UsageAttributionSnapshot.Empty;

        var scale = accountTotal / (double)rawTotal;
        var rows = sessions
            .Select(session => ToSessionRow(session, scale, rawTotal))
            .OrderByDescending(row => row.EstimatedTokens)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(_maxSessions)
            .ToList();

        return new UsageAttributionSnapshot
        {
            Sessions = rows,
            AccountWindowTokens = accountTotal,
            RawLocalTokens = rawTotal,
            EstimatedAttributedTokens = rows.Sum(row => row.EstimatedTokens),
            LastUpdatedUtc = now
        };
    }

    private static UsageAttributionSessionRow ToSessionRow(SessionAggregate session, double scale, long rawTotal) => new(
        DisplayNameFor(session.Summary),
        session.Summary.ThreadId,
        GetProjectDisplayName(session.Summary.Cwd),
        NormalizeProjectPath(session.Summary.Cwd),
        session.RawLocalTokens,
        ScaleTokens(session.RawLocalTokens, scale),
        Math.Round(session.RawLocalTokens / (double)rawTotal * 100, 1),
        SumNullable(session.TokenSummaries.Select(item => item.InputTokens)),
        SumNullable(session.TokenSummaries.Select(item => item.OutputTokens)),
        SumNullable(session.TokenSummaries.Select(item => item.CachedInputTokens)),
        SumNullable(session.TokenSummaries.Select(item => item.ReasoningTokens)),
        session.Summary.UpdatedAtUtc,
        session.TokenSummaries.Max(item => item.TimestampUtc));

    private static long ScaleTokens(long rawTokens, double scale) => (long)Math.Round(rawTokens * scale);
    private static long? SumNullable(IEnumerable<long?> values)
    {
        var hasAny = false;
        long total = 0;
        foreach (var value in values) if (value is long tokens) { hasAny = true; total += tokens; }
        return hasAny ? total : null;
    }

    private static string DisplayNameFor(SharedRolloutSessionSummary summary) => $"{FallbackChatPrefix(summary)} · {FormatChatSuffix(summary)}";
    private static string FallbackChatPrefix(SharedRolloutSessionSummary summary) => string.Equals(GetProjectDisplayName(summary.Cwd), "Unknown project", StringComparison.Ordinal) ? "Local chat" : $"{GetProjectDisplayName(summary.Cwd)} chat";
    private static string FormatChatSuffix(SharedRolloutSessionSummary summary) => summary.UpdatedAtUtc is DateTimeOffset updatedAt ? updatedAt.ToUniversalTime().ToString("dd MMM HH:mm", CultureInfo.InvariantCulture) : "time unknown";
    private static long GetAccountTotalForWindow(IReadOnlyList<DailyUsageBucket> dailyBuckets, DateOnly cutoffDate) => dailyBuckets.Where(bucket => DateOnly.TryParse(bucket.StartDate, out var date) && date >= cutoffDate).Sum(bucket => bucket.TotalTokens ?? 0);
    private static DateOnly GetCutoffDate(DateTimeOffset now) => DateOnly.FromDateTime(now.ToLocalTime().DateTime).AddDays(-(UsageWindowDays - 1));
    private static string NormalizeProjectPath(string path) => LocalProjectPathNormalizer.Normalize(path);
    private static string GetProjectDisplayName(string path) => LocalProjectPathNormalizer.GetDisplayName(path);

    private sealed class SessionAggregate(SharedRolloutSessionSummary summary)
    {
        public SharedRolloutSessionSummary Summary { get; } = summary;
        public IReadOnlyList<SharedRolloutTokenSummary> TokenSummaries { get; } = summary.TokenSummaries;
        public long RawLocalTokens { get; } = summary.TokenSummaries.Sum(item => item.TotalTokens);
    }
}
