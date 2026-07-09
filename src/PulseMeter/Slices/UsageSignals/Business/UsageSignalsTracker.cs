using System.Globalization;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Formatting;
using PulseMeter.Shared.RateLimits;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageSignals.Business;

public interface IUsageSignalsTracker
{
    UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc);

    void DismissIdleDrain();
}

public sealed class UsageSignalsTracker : IUsageSignalsTracker
{
    private static readonly TimeSpan MinimumRunwayObservation = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumIdleDrainObservation = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumIdleTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortWindowRunwayWarning = TimeSpan.FromMinutes(90);
    private static readonly TimeSpan WeeklyRunwayWarning = TimeSpan.FromHours(24);
    private const double MinimumIdleDrainPercentDelta = 2;
    private const int WeeklyWindowMinutes = 10_080;
    private const double LowWeeklyRemainingThreshold = 25;
    private const int ResetCreditSoonDays = 3;
    private const double HighProjectShareThreshold = 55;
    private const double TodayHighMedianMultiplier = 1.5;

    private readonly IUserIdleTimeProvider _idleTimeProvider;
    private readonly Dictionary<string, BucketSample> _samples = new(StringComparer.OrdinalIgnoreCase);
    private IdleDrainIncident? _idleDrainIncident;
    private string? _dismissedIdleDrainBucketId;
    private DateTimeOffset? _dismissedIdleDrainResetsAtUtc;

    public UsageSignalsTracker(IUserIdleTimeProvider idleTimeProvider)
    {
        _idleTimeProvider = idleTimeProvider;
    }

    public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
    {
        ClearExpiredIdleIncident(nowUtc, snapshot);

        var runwaySignals = new List<LimitRunwaySignal>();
        var idleDrainIncident = _idleDrainIncident;
        if (snapshot.SyncStatus is SyncStatus.Live)
        {
            var idleTime = _idleTimeProvider.GetIdleTime();

            foreach (var bucket in snapshot.Buckets)
            {
                if (!TryCreateSample(bucket, nowUtc, out var current))
                {
                    continue;
                }

                if (_samples.TryGetValue(current.BucketId, out var previous))
                {
                    var runway = TryBuildRunwaySignal(previous, current);
                    if (runway is not null)
                    {
                        runwaySignals.Add(runway);
                    }

                    TryUpdateIdleDrain(previous, current, idleTime, snapshot);
                }

                _samples[current.BucketId] = current;
            }

            idleDrainIncident = _idleDrainIncident;
        }
        else if (IsMockShowcaseSnapshot(snapshot))
        {
            runwaySignals.AddRange(BuildMockRunwaySignals(snapshot, nowUtc));
            idleDrainIncident = BuildMockIdleDrainIncident(snapshot, nowUtc);
        }

        return BuildSnapshot(snapshot, nowUtc, runwaySignals, idleDrainIncident);
    }

    public void DismissIdleDrain()
    {
        if (_idleDrainIncident is null)
        {
            return;
        }

        _dismissedIdleDrainBucketId = _idleDrainIncident.BucketId;
        _dismissedIdleDrainResetsAtUtc = _idleDrainIncident.ResetsAtUtc;
        _idleDrainIncident = null;
    }

    private UsageSignalsSnapshot BuildSnapshot(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc,
        IReadOnlyList<LimitRunwaySignal> runwaySignals,
        IdleDrainIncident? idleDrainIncident)
    {
        return new UsageSignalsSnapshot
        {
            RunwaySignals = runwaySignals
                .OrderBy(signal => signal.TimeToExhaustion)
                .ToList(),
            IdleDrainIncident = idleDrainIncident,
            ShowAllAttentionSignals = IsMockShowcaseSnapshot(snapshot),
            AttentionSignals = BuildAttentionSignals(snapshot, nowUtc, runwaySignals, idleDrainIncident)
        };
    }

    private IReadOnlyList<UsageAttentionSignal> BuildAttentionSignals(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc,
        IEnumerable<LimitRunwaySignal> runwaySignals,
        IdleDrainIncident? idleDrainIncident)
    {
        var signals = new List<UsageAttentionSignal>();

        AddSyncSignal(signals, snapshot);
        AddIdleDrainSignal(signals, idleDrainIncident);
        AddRunwaySignals(signals, runwaySignals);
        AddWeeklyLimitSignal(signals, snapshot);
        AddResetCreditSignal(signals, snapshot, nowUtc);
        AddDailyUsageSignal(signals, snapshot, nowUtc);
        AddProjectUsageSignal(signals, snapshot);

        return signals
            .OrderBy(signal => signal.Priority)
            .ToList();
    }

    private static void AddSyncSignal(List<UsageAttentionSignal> signals, UsageSnapshot snapshot)
    {
        if (snapshot.SyncStatus == SyncStatus.Unavailable)
        {
            signals.Add(new UsageAttentionSignal(
                0,
                "SYNC",
                "Live sync unavailable",
                string.IsNullOrWhiteSpace(snapshot.StatusMessage)
                    ? "Live usage is unavailable. Sync again after the source is ready."
                    : snapshot.StatusMessage,
                "#EF4444"));
            return;
        }

        if (snapshot.SyncStatus == SyncStatus.Stale)
        {
            signals.Add(new UsageAttentionSignal(
                1,
                "SYNC",
                "Live data is stale",
                "Showing last good usage data until the next successful sync.",
                "#F97316"));
        }
    }

    private static void AddIdleDrainSignal(List<UsageAttentionSignal> signals, IdleDrainIncident? idleDrainIncident)
    {
        if (idleDrainIncident is not IdleDrainIncident incident)
        {
            return;
        }

        signals.Add(new UsageAttentionSignal(
            2,
            "IDLE",
            "Usage moved while idle",
            incident.SummaryText,
            incident.AccentBrush,
            incident.DiagnosticText,
            "idle-drain"));
    }

    private static bool IsMockShowcaseSnapshot(UsageSnapshot snapshot)
    {
        return snapshot.SyncStatus == SyncStatus.Mocked
            && snapshot.Source.Equals("Mock", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<LimitRunwaySignal> BuildMockRunwaySignals(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        var signals = new List<LimitRunwaySignal>();

        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var sample)
                || sample.UsedPercent < 90
                || sample.UsedPercent >= 100
                || sample.ResetsAtUtc <= nowUtc.AddMinutes(5))
            {
                continue;
            }

            var resetRemaining = sample.ResetsAtUtc - nowUtc;
            var minutes = Math.Clamp((100 - sample.UsedPercent) * 6, 10, 45);
            var timeToExhaustion = TimeSpan.FromMinutes(minutes);
            if (timeToExhaustion >= resetRemaining)
            {
                timeToExhaustion = TimeSpan.FromTicks(resetRemaining.Ticks / 2);
            }

            if (timeToExhaustion <= TimeSpan.Zero)
            {
                continue;
            }

            var exhaustsAt = nowUtc + timeToExhaustion;
            var runway = FormatDuration(timeToExhaustion);
            signals.Add(new LimitRunwaySignal(
                sample.BucketId,
                sample.LimitKey,
                sample.WindowLabel,
                sample.WindowDurationMins,
                sample.ResetsAtUtc,
                exhaustsAt,
                timeToExhaustion,
                $"Runway: about {runway} in mock mode",
                "Projected to run out before reset",
                $"At the mock demo pace, {sample.WindowLabel} may run out in about {runway} before reset.",
                "#F97316"));
        }

        return signals;
    }

    private static IdleDrainIncident? BuildMockIdleDrainIncident(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var sample)
                || sample.UsedPercent < 80
                || sample.ResetsAtUtc <= nowUtc)
            {
                continue;
            }

            var observationDuration = TimeSpan.FromMinutes(11);
            var idleDuration = TimeSpan.FromMinutes(9);
            var before = Math.Max(0, sample.UsedPercent - 4);
            var firstObservedAt = nowUtc - observationDuration;
            var summary = $"Usage moved while idle: {FormatPercent(before)} -> {FormatPercent(sample.UsedPercent)} in {FormatDuration(observationDuration)}";
            var detail = $"{sample.WindowLabel} changed while Windows reported {FormatDuration(idleDuration)} idle.";
            var diagnostic = string.Join(Environment.NewLine, [
                "PulseMeter Mock idle-drain demo",
                $"Bucket: {sample.WindowLabel}",
                $"Usage: {FormatPercent(before)} -> {FormatPercent(sample.UsedPercent)}",
                $"Observed: {firstObservedAt:u} -> {nowUtc:u}",
                $"Duration: {FormatDuration(observationDuration)}",
                $"Windows idle time: {FormatDuration(idleDuration)}",
                "Sync status: Mocked",
                "This is generated mock data for screenshots and demos.",
                "No prompt text or Codex message content was inspected."
            ]);

            return new IdleDrainIncident(
                sample.BucketId,
                sample.LimitKey,
                sample.WindowLabel,
                sample.WindowDurationMins,
                sample.ResetsAtUtc,
                nowUtc,
                firstObservedAt,
                nowUtc,
                before,
                sample.UsedPercent,
                observationDuration,
                idleDuration,
                summary,
                detail,
                diagnostic,
                "#F97316");
        }

        return null;
    }

    private static void AddRunwaySignals(
        List<UsageAttentionSignal> signals,
        IEnumerable<LimitRunwaySignal> runwaySignals)
    {
        foreach (var signal in runwaySignals)
        {
            signals.Add(new UsageAttentionSignal(
                3,
                "RUNWAY",
                signal.Title,
                signal.Detail,
                signal.AccentBrush));
        }
    }

    private static void AddWeeklyLimitSignal(List<UsageAttentionSignal> signals, UsageSnapshot snapshot)
    {
        var bucket = snapshot.Buckets.FirstOrDefault(IsWeeklyWindow);
        if (bucket?.UsedPercent is not double)
        {
            return;
        }

        if (bucket.RemainingPercentValue > LowWeeklyRemainingThreshold)
        {
            return;
        }

        var detail = bucket.ResetCountdown == "reset unknown"
            ? bucket.RemainingPercentText
            : $"{bucket.RemainingPercentText}; {bucket.ResetText}.";

        signals.Add(new UsageAttentionSignal(
            4,
            "LIMIT",
            "Weekly window is low",
            detail,
            "#F97316"));
    }

    private static void AddResetCreditSignal(
        List<UsageAttentionSignal> signals,
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        var expiresAt = snapshot.ResetCredits
            .Select(credit => credit.ExpiresAtUtc)
            .OfType<DateTimeOffset>()
            .Where(expiry => expiry > nowUtc)
            .Order()
            .FirstOrDefault();

        if (expiresAt == default && snapshot.ResetCreditsExpiresAtUtc is DateTimeOffset singleExpiry)
        {
            expiresAt = singleExpiry;
        }

        if (expiresAt == default)
        {
            return;
        }

        var remaining = expiresAt - nowUtc;
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromDays(ResetCreditSoonDays))
        {
            return;
        }

        var countdown = CountdownFormatter.FormatResetCountdown(expiresAt.ToUnixTimeSeconds(), nowUtc);
        signals.Add(new UsageAttentionSignal(
            5,
            "CREDIT",
            "Reset credit expires soon",
            $"One reset credit expires in {countdown}.",
            "#F59E0B"));
    }

    private static void AddDailyUsageSignal(
        List<UsageAttentionSignal> signals,
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        if (snapshot.DailyBuckets.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(nowUtc.LocalDateTime);
        var todayTokens = SumTokensForDate(snapshot.DailyBuckets, today);
        if (todayTokens is null)
        {
            return;
        }

        var median = CalculateRecentMedian(snapshot.DailyBuckets, today);
        if (median <= 0 || todayTokens.Value < median * TodayHighMedianMultiplier)
        {
            return;
        }

        signals.Add(new UsageAttentionSignal(
            6,
            "TODAY",
            "Today is above usual",
            $"{MeterDisplayFormatter.FormatTokens(todayTokens.Value)} tokens today; {MeterDisplayFormatter.FormatTokens((long)Math.Round(median))} daily median.",
            "#1F73FF"));
    }

    private static void AddProjectUsageSignal(List<UsageAttentionSignal> signals, UsageSnapshot snapshot)
    {
        var topProject = snapshot.ProjectUsageRows
            .OrderByDescending(row => row.SharePercent)
            .FirstOrDefault();

        if (topProject is null || topProject.SharePercent < HighProjectShareThreshold)
        {
            return;
        }

        signals.Add(new UsageAttentionSignal(
            7,
            "PROJECT",
            $"{Shorten(topProject.DisplayName, 34)} leads recent usage",
            $"{topProject.SharePercent.ToString("0.#", CultureInfo.InvariantCulture)}% of the last 30-day project estimate.",
            "#1F73FF"));
    }

    private void ClearExpiredIdleIncident(DateTimeOffset nowUtc, UsageSnapshot snapshot)
    {
        if (_idleDrainIncident is not null && nowUtc >= _idleDrainIncident.ResetsAtUtc)
        {
            _idleDrainIncident = null;
        }

        if (_dismissedIdleDrainResetsAtUtc is DateTimeOffset dismissedReset && nowUtc >= dismissedReset)
        {
            _dismissedIdleDrainBucketId = null;
            _dismissedIdleDrainResetsAtUtc = null;
        }

        if (snapshot.SyncStatus is not SyncStatus.Live)
        {
            return;
        }

        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var sample))
            {
                continue;
            }

            if (_idleDrainIncident is not null
                && IsSameLimitWindow(sample, _idleDrainIncident)
                && sample.ResetsAtUtc > _idleDrainIncident.ResetsAtUtc)
            {
                _idleDrainIncident = null;
            }

            if (_dismissedIdleDrainBucketId is not null
                && string.Equals(_dismissedIdleDrainBucketId, sample.BucketId, StringComparison.OrdinalIgnoreCase)
                && _dismissedIdleDrainResetsAtUtc is DateTimeOffset dismissedBucketReset
                && sample.ResetsAtUtc > dismissedBucketReset)
            {
                _dismissedIdleDrainBucketId = null;
                _dismissedIdleDrainResetsAtUtc = null;
            }
        }
    }

    private static LimitRunwaySignal? TryBuildRunwaySignal(BucketSample previous, BucketSample current)
    {
        if (current.ResetsAtUtc != previous.ResetsAtUtc)
        {
            return null;
        }

        var elapsed = current.ObservedAtUtc - previous.ObservedAtUtc;
        if (elapsed < MinimumRunwayObservation)
        {
            return null;
        }

        var usedDelta = current.UsedPercent - previous.UsedPercent;
        if (usedDelta <= 0)
        {
            return null;
        }

        var remaining = Math.Max(0, 100 - current.UsedPercent);
        var percentPerMinute = usedDelta / elapsed.TotalMinutes;
        if (percentPerMinute <= 0 || remaining <= 0)
        {
            return null;
        }

        var minutesToExhaustion = remaining / percentPerMinute;
        var timeToExhaustion = TimeSpan.FromMinutes(minutesToExhaustion);
        var exhaustsAt = current.ObservedAtUtc + timeToExhaustion;
        if (exhaustsAt >= current.ResetsAtUtc)
        {
            return null;
        }

        var warningWindow = IsWeekly(current) ? WeeklyRunwayWarning : ShortWindowRunwayWarning;
        if (timeToExhaustion > warningWindow)
        {
            return null;
        }

        var runway = FormatDuration(timeToExhaustion);
        var resetName = IsWeekly(current) ? "weekly" : "5h";

        return new LimitRunwaySignal(
            current.BucketId,
            current.LimitKey,
            current.WindowLabel,
            current.WindowDurationMins,
            current.ResetsAtUtc,
            exhaustsAt,
            timeToExhaustion,
            $"Runway: about {runway} at current pace",
            "Projected to run out before reset",
            $"At the current pace, {current.WindowLabel} may run out in about {runway} before the {resetName} reset.",
            "#F97316");
    }

    private void TryUpdateIdleDrain(BucketSample previous, BucketSample current, TimeSpan idleTime, UsageSnapshot snapshot)
    {
        if (current.ResetsAtUtc != previous.ResetsAtUtc)
        {
            return;
        }

        if (_dismissedIdleDrainBucketId is not null
            && string.Equals(_dismissedIdleDrainBucketId, current.BucketId, StringComparison.OrdinalIgnoreCase)
            && _dismissedIdleDrainResetsAtUtc == current.ResetsAtUtc)
        {
            return;
        }

        var elapsed = current.ObservedAtUtc - previous.ObservedAtUtc;
        var usedDelta = current.UsedPercent - previous.UsedPercent;
        if (elapsed < MinimumIdleDrainObservation
            || idleTime < MinimumIdleTime
            || usedDelta < MinimumIdleDrainPercentDelta)
        {
            return;
        }

        var summary = $"Usage moved while idle: {FormatPercent(previous.UsedPercent)} -> {FormatPercent(current.UsedPercent)} in {FormatDuration(elapsed)}";
        var detail = $"{current.WindowLabel} changed while Windows reported {FormatDuration(idleTime)} idle.";
        var diagnostic = BuildDiagnosticText(previous, current, elapsed, idleTime, snapshot);

        _idleDrainIncident = new IdleDrainIncident(
            current.BucketId,
            current.LimitKey,
            current.WindowLabel,
            current.WindowDurationMins,
            current.ResetsAtUtc,
            current.ObservedAtUtc,
            previous.ObservedAtUtc,
            current.ObservedAtUtc,
            previous.UsedPercent,
            current.UsedPercent,
            elapsed,
            idleTime,
            summary,
            detail,
            diagnostic,
            "#F97316");
    }

    private static string BuildDiagnosticText(
        BucketSample previous,
        BucketSample current,
        TimeSpan elapsed,
        TimeSpan idleTime,
        UsageSnapshot snapshot)
    {
        var threadEvidence = snapshot.RecentActiveThread is null
            ? "No recent thread activity was reported in the snapshot."
            : "Recent thread activity was present in the snapshot.";

        return string.Join(Environment.NewLine, [
            "PulseMeter idle drain diagnostic",
            $"Bucket: {current.WindowLabel}",
            $"Usage: {FormatPercent(previous.UsedPercent)} -> {FormatPercent(current.UsedPercent)}",
            $"Observed: {previous.ObservedAtUtc:u} -> {current.ObservedAtUtc:u}",
            $"Duration: {FormatDuration(elapsed)}",
            $"Windows idle time: {FormatDuration(idleTime)}",
            $"Sync status: {snapshot.SyncStatus}",
            threadEvidence,
            "No prompt text or Codex message content was inspected."
        ]);
    }

    private static bool TryCreateSample(RateLimitBucket bucket, DateTimeOffset nowUtc, out BucketSample sample)
    {
        sample = default;
        var resetAt = bucket.ResetsAtUtc
            ?? (bucket.ResetsAtUnixSeconds is long unix ? DateTimeOffset.FromUnixTimeSeconds(unix) : null);

        if (bucket.UsedPercent is not double usedPercent || resetAt is not DateTimeOffset resetsAt)
        {
            return false;
        }

        var limitKey = RateLimitBucketKeys.Get(bucket);
        var windowLabel = string.IsNullOrWhiteSpace(bucket.Label) ? bucket.WindowLabel : bucket.Label;
        var bucketId = $"{limitKey}|{bucket.WindowDurationMins?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";

        sample = new BucketSample(
            bucketId,
            limitKey,
            windowLabel,
            bucket.WindowDurationMins,
            Math.Clamp(usedPercent, 0, 100),
            resetsAt,
            nowUtc);
        return true;
    }

    private static bool IsSameLimitWindow(BucketSample sample, IdleDrainIncident incident)
    {
        return string.Equals(sample.LimitKey, incident.LimitKey, StringComparison.OrdinalIgnoreCase)
            && sample.WindowDurationMins == incident.WindowDurationMins;
    }

    private static bool IsWeeklyWindow(RateLimitBucket bucket)
    {
        if (bucket.WindowDurationMins is int mins)
        {
            return mins >= WeeklyWindowMinutes;
        }

        return bucket.WindowLabel.Contains('d', StringComparison.OrdinalIgnoreCase)
            || bucket.Label.Contains("week", StringComparison.OrdinalIgnoreCase);
    }

    private static long? SumTokensForDate(IEnumerable<DailyUsageBucket> buckets, DateOnly date)
    {
        var values = buckets
            .Where(bucket => DateOnly.TryParse(bucket.StartDate, out var bucketDate) && bucketDate == date)
            .Select(bucket => bucket.TotalTokens ?? 0)
            .ToList();

        return values.Count == 0 ? null : values.Sum();
    }

    private static double CalculateRecentMedian(IEnumerable<DailyUsageBucket> buckets, DateOnly today)
    {
        var totals = buckets
            .Select(bucket => new
            {
                HasDate = DateOnly.TryParse(bucket.StartDate, out var date),
                Date = DateOnly.TryParse(bucket.StartDate, out var parsedDate) ? parsedDate : DateOnly.MinValue,
                Tokens = bucket.TotalTokens ?? 0
            })
            .Where(row => row.HasDate
                && row.Date < today
                && row.Date >= today.AddDays(-30)
                && row.Tokens > 0)
            .GroupBy(row => row.Date)
            .Select(group => group.Sum(row => row.Tokens))
            .Order()
            .Select(tokens => (double)tokens)
            .ToList();

        if (totals.Count == 0)
        {
            return 0;
        }

        var middle = totals.Count / 2;
        return totals.Count % 2 == 1
            ? totals[middle]
            : (totals[middle - 1] + totals[middle]) / 2;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static bool IsWeekly(BucketSample sample)
    {
        return sample.WindowDurationMins is int mins && mins >= WeeklyWindowMinutes;
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalMinutes < 1)
        {
            return "<1m";
        }

        if (value.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero))}m";
        }

        var hours = (int)value.TotalHours;
        var minutes = value.Minutes;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }

    private readonly record struct BucketSample(
        string BucketId,
        string LimitKey,
        string WindowLabel,
        int? WindowDurationMins,
        double UsedPercent,
        DateTimeOffset ResetsAtUtc,
        DateTimeOffset ObservedAtUtc);
}
