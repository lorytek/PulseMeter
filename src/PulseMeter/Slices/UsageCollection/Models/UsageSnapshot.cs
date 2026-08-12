namespace PulseMeter.Slices.UsageCollection.Models;

/// <summary>
/// Indicates whether the local activity-hour evidence was read completely.
/// </summary>
public enum ActivityEvidenceCoverage
{
    Unavailable,
    Partial,
    Available
}

/// <summary>
/// Privacy-safe local activity evidence. It retains only distinct UTC hour starts;
/// no rollout content, paths, identifiers, or token amounts are included.
/// </summary>
public sealed class ActivityEvidenceSnapshot
{
    public static readonly ActivityEvidenceSnapshot Unavailable = new();

    public ActivityEvidenceCoverage Coverage { get; init; } = ActivityEvidenceCoverage.Unavailable;

    public IReadOnlyList<DateTimeOffset> UtcHours { get; init; } = Array.Empty<DateTimeOffset>();
}

public sealed class UsageSnapshot
{
    public IReadOnlyList<RateLimitBucket> Buckets { get; init; } = Array.Empty<RateLimitBucket>();

    public IReadOnlyList<RateLimitHistoryPoint> RateLimitHistory { get; init; } = Array.Empty<RateLimitHistoryPoint>();

    public long? LifetimeTokens { get; init; }

    public long? PeakDailyTokens { get; init; }

    public int? LongestRunningTurnSec { get; init; }

    public int? CurrentStreakDays { get; init; }

    public int? LongestStreakDays { get; init; }

    public IReadOnlyList<DailyUsageBucket> DailyBuckets { get; init; } = Array.Empty<DailyUsageBucket>();

    public IReadOnlyList<ProjectUsageRow> ProjectUsageRows { get; init; } = Array.Empty<ProjectUsageRow>();

    public UsageAttributionSnapshot UsageAttribution { get; init; } = UsageAttributionSnapshot.Empty;

    public ActivityEvidenceSnapshot ActivityEvidence { get; init; } = ActivityEvidenceSnapshot.Unavailable;

    public int? ResetCreditsAvailable { get; init; }

    public DateTimeOffset? ResetCreditsExpiresAtUtc { get; init; }

    public IReadOnlyList<ResetCreditSnapshot> ResetCredits { get; init; } = Array.Empty<ResetCreditSnapshot>();

    public ThreadUsageSnapshot? RecentActiveThread { get; init; }

    public SyncStatus SyncStatus { get; init; } = SyncStatus.Mocked;

    public DateTimeOffset? LastUpdatedUtc { get; init; }

    public string Source { get; init; } = "Mock";

    public string? StatusMessage { get; init; }

    public string? RawRateLimitsJson { get; init; }
}
