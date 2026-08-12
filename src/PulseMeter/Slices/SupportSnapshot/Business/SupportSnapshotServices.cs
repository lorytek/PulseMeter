using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PulseMeter.Slices.SupportSnapshot.Models;
using PulseMeter.Slices.UsageCollection.Models;

namespace PulseMeter.Slices.SupportSnapshot.Business;

public interface ISupportSnapshotFactsStore
{
    void Observe(UsageSnapshot snapshot, DateTimeOffset observedAtUtc);

    SupportSnapshotFacts Capture();
}

public sealed class SupportSnapshotFactsStore : ISupportSnapshotFactsStore
{
    private SupportSnapshotFacts _facts = SupportSnapshotFacts.NotObserved;

    public void Observe(UsageSnapshot snapshot, DateTimeOffset observedAtUtc)
    {
        var readerState = snapshot.SyncStatus switch
        {
            SyncStatus.Live => SupportReaderState.Live,
            SyncStatus.Stale => SupportReaderState.Stale,
            SyncStatus.Unavailable => SupportReaderState.Unavailable,
            SyncStatus.Mocked => SupportReaderState.Mock,
            _ => SupportReaderState.NotObserved
        };
        var readable = readerState is SupportReaderState.Live or SupportReaderState.Stale or SupportReaderState.Mock;
        _facts = new SupportSnapshotFacts(
            readerState,
            readable ? snapshot.LastUpdatedUtc : null,
            readable && snapshot.Buckets.Count > 0,
            readable && (snapshot.DailyBuckets.Count > 0
                || snapshot.LifetimeTokens.HasValue
                || snapshot.PeakDailyTokens.HasValue
                || snapshot.LongestRunningTurnSec.HasValue
                || snapshot.CurrentStreakDays.HasValue
                || snapshot.LongestStreakDays.HasValue),
            readable && snapshot.ProjectUsageRows.Count > 0);
        _ = observedAtUtc;
    }

    public SupportSnapshotFacts Capture() => _facts;
}

public sealed class SupportSnapshotFormatter
{
    private static readonly Regex PublicSemanticVersion = new("^\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _version;
    private readonly string _buildConfiguration;

    public SupportSnapshotFormatter(string? version = null, string? buildConfiguration = null, Func<DateTimeOffset>? utcNow = null)
    {
        _version = SanitizePublicSemanticVersion(version ?? typeof(SupportSnapshotFormatter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        _buildConfiguration = buildConfiguration is "Debug" or "Release" ? buildConfiguration : BuildConfiguration;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public string Format(SupportSnapshotFacts facts)
    {
        var readable = facts.ReaderState is SupportReaderState.Live or SupportReaderState.Stale or SupportReaderState.Mock;
        var parser = !readable ? facts.ReaderState == SupportReaderState.NotObserved ? "not_observed" : "unknown"
            : facts.HasRateLimitData || facts.HasAccountUsageData || facts.HasLocalProjectHistory ? "recognized" : "no_supported_fields";
        var document = new SupportSnapshotDocument(
            1,
            "PulseMeter",
            _version,
            _buildConfiguration,
            Reader(facts.ReaderState),
            Freshness(facts, readable),
            parser,
            new SupportCoverageDocument(
                Coverage(facts.HasRateLimitData, readable),
                Coverage(facts.HasAccountUsageData, readable),
                Coverage(facts.HasLocalProjectHistory, readable)));
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string SanitizePublicSemanticVersion(string? value)
    {
        var candidate = value?.Split('+')[0] ?? string.Empty;
        return PublicSemanticVersion.IsMatch(candidate) ? candidate : "unknown";
    }

    private static string Reader(SupportReaderState state) => state switch
    {
        SupportReaderState.Live => "live", SupportReaderState.Stale => "stale", SupportReaderState.Unavailable => "unavailable", SupportReaderState.Mock => "mock", _ => "not_observed"
    };

    private string Freshness(SupportSnapshotFacts facts, bool readable)
    {
        if (!readable || facts.LastUpdatedUtc is not DateTimeOffset updated)
        {
            return "unknown";
        }

        var age = _utcNow() - updated;
        if (age < TimeSpan.Zero)
        {
            return "unknown";
        }

        return age < TimeSpan.FromMinutes(1) ? "under_1m" : age <= TimeSpan.FromMinutes(5) ? "1_to_5m" : age <= TimeSpan.FromMinutes(30) ? "5_to_30m" : "over_30m";
    }

    private static string Coverage(bool present, bool readable) => !readable ? "unknown" : present ? "present" : "absent";

    private static string BuildConfiguration
    {
        get
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
    }

    private sealed record SupportSnapshotDocument(int schema_version, string product, string version, string build_configuration, string reader_state, string freshness_age, string parser_state, SupportCoverageDocument coverage);
    private sealed record SupportCoverageDocument(string rate_limit_data, string account_usage_data, string local_project_history);
}
