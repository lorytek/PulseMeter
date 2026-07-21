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

    void Flush()
    {
    }
}

public sealed class UsageSignalsTracker : IUsageSignalsTracker
{
    private static readonly TimeSpan MinimumRunwayObservation = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ShortRunwayHistory = TimeSpan.FromHours(3);
    private static readonly TimeSpan WeeklyRunwayHistory = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaximumTrendHistory = TimeSpan.FromDays(7);
    private static readonly TimeSpan MeasurementGapThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WeeklyFlatSampleCheckpointInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ShortFlatSampleCheckpointInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShortRunwayHalfLife = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan WeeklyRunwayHalfLife = TimeSpan.FromHours(6);
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
    private const double MinimumRunwayMovement = 0.1;
    private const double RunwayPriorShape = 0.5;
    private const double ForecastLowerQuantile = 0.10;
    private const double ForecastUpperQuantile = 0.90;
    private const int ForecastProjectionPointCount = 13;
    private const int MinimumRunwaySamples = 3;
    private const int MaximumRunwaySamples = 1_024;
    private const int MaximumPersistedRunwayBuckets = 16;

    private readonly IUserIdleTimeProvider _idleTimeProvider;
    private readonly IRunwayObservationStateStore? _runwayObservationStateStore;
    // Runway needs a recent, bounded pace sample; idle drain needs a baseline that spans
    // only one continuous Windows-idle period. Keep those observations independently.
    private readonly Dictionary<string, BucketObservation> _observations = new(StringComparer.OrdinalIgnoreCase);
    private IdleDrainIncident? _idleDrainIncident;
    private string? _dismissedIdleDrainBucketId;
    private DateTimeOffset? _dismissedIdleDrainResetsAtUtc;
    private bool _restoreComplete;
    private bool _restoreFailedClosed;
    private int _restoreAttempts;
    private bool _runwayStateDirty;

    public UsageSignalsTracker(IUserIdleTimeProvider idleTimeProvider, IRunwayObservationStateStore? runwayObservationStateStore = null)
    {
        _idleTimeProvider = idleTimeProvider;
        _runwayObservationStateStore = runwayObservationStateStore;
    }

    public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
    {
        ClearExpiredIdleIncident(nowUtc, snapshot);

        var runwaySignals = new List<LimitRunwaySignal>();
        var runwayForecasts = new List<LimitRunwayForecast>();
        var usageTrends = new List<LimitUsageTrend>();
        var idleDrainIncident = _idleDrainIncident;
        if (snapshot.SyncStatus is SyncStatus.Live)
        {
            HydrateRestoredObservations(nowUtc);
            MergeHistoricalRateLimitPoints(snapshot, nowUtc);
            var idleTime = _idleTimeProvider.GetIdleTime();

            foreach (var bucket in snapshot.Buckets)
            {
                if (!TryCreateSample(bucket, nowUtc, out var current))
                {
                    continue;
                }

                if (current.ResetsAtUtc <= nowUtc)
                {
                    continue;
                }

                if (!_observations.TryGetValue(current.BucketId, out var observation)
                    || observation.ResetsAtUtc != current.ResetsAtUtc)
                {
                    _observations[current.BucketId] = new BucketObservation(current);
                    _runwayStateDirty = true;
                    runwayForecasts.Add(BuildInitialForecast(current, isMock: false));
                    continue;
                }

                if (AddRunwaySample(observation, current))
                {
                    _runwayStateDirty = true;
                }
                var forecast = BuildRunwayForecast(SelectForecastSamples(observation.RunwaySamples));
                runwayForecasts.Add(forecast);
                var runway = TryBuildRunwaySignal(forecast, current, nowUtc);
                if (runway is not null)
                {
                    runwaySignals.Add(runway);
                }

                var idleDrainBaseline = observation.IdleDrainBaseline;
                if (observation.Restored)
                {
                    observation.Restored = false;
                    idleDrainBaseline = current;
                }
                else if (ShouldRebaseIdleDrain(idleDrainBaseline, current, idleTime))
                {
                    idleDrainBaseline = current;
                }
                else
                {
                    TryUpdateIdleDrain(idleDrainBaseline, current, idleTime, snapshot);
                }

                observation.IdleDrainBaseline = idleDrainBaseline;
            }

            if (RemoveExpiredObservationAnchors(nowUtc))
            {
                _runwayStateDirty = true;
            }

            PersistRunwayObservations();

            usageTrends.AddRange(BuildUsageTrends());

            idleDrainIncident = _idleDrainIncident;
        }
        else if (IsMockShowcaseSnapshot(snapshot))
        {
            runwaySignals.AddRange(BuildMockRunwaySignals(snapshot, nowUtc));
            runwayForecasts.AddRange(BuildMockRunwayForecasts(snapshot, nowUtc));
            usageTrends.AddRange(BuildMockUsageTrends(snapshot, nowUtc));
            idleDrainIncident = BuildMockIdleDrainIncident(snapshot, nowUtc);
        }

        return BuildSnapshot(snapshot, nowUtc, runwaySignals, runwayForecasts, usageTrends, idleDrainIncident);
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

    public void Flush()
    {
        PersistRunwayObservations();
    }

    private void HydrateRestoredObservations(DateTimeOffset nowUtc)
    {
        if (_restoreComplete || _restoreFailedClosed)
        {
            return;
        }

        RunwayObservationLoadResult loadResult;
        try
        {
            loadResult = _runwayObservationStateStore?.Load()
                ?? new RunwayObservationLoadResult(RunwayObservationLoadStatus.Missing);
        }
        catch (Exception)
        {
            RecordUnavailableRestoreAttempt();
            return;
        }

        if (loadResult.Status == RunwayObservationLoadStatus.Unavailable)
        {
            RecordUnavailableRestoreAttempt();
            return;
        }

        _restoreComplete = true;
        var state = loadResult.State;
        if (loadResult.Status != RunwayObservationLoadStatus.Loaded
            || state is null
            || state.SchemaVersion != RunwayObservationStateStore.CurrentSchemaVersion
            || state.Samples is null
            || state.Samples.Any(sample => sample is null))
        {
            return;
        }

        foreach (var group in state.Samples.Cast<RunwayObservationSample>()
                     .GroupBy(sample => sample.BucketId, StringComparer.OrdinalIgnoreCase)
                     .Take(MaximumPersistedRunwayBuckets))
        {
            var samples = group.ToList();
            if (!TryRestoreBucketObservation(samples, nowUtc, out var observation))
            {
                continue;
            }

            _observations[group.Key] = observation;
        }
    }

    private void RecordUnavailableRestoreAttempt()
    {
        _restoreAttempts++;
        if (_restoreAttempts >= 3)
        {
            _restoreFailedClosed = true;
        }
    }

    private void MergeHistoricalRateLimitPoints(UsageSnapshot snapshot, DateTimeOffset nowUtc)
    {
        if (snapshot.RateLimitHistory.Count == 0)
        {
            return;
        }

        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var current)
                || current.ResetsAtUtc <= nowUtc)
            {
                continue;
            }

            var cutoff = nowUtc - ResolveTrendHistory(current.WindowDurationMins);
            var candidates = snapshot.RateLimitHistory
                .Where(point => string.Equals(point.LimitKey, current.LimitKey, StringComparison.OrdinalIgnoreCase)
                    && point.WindowDurationMins == current.WindowDurationMins
                    && point.ResetsAtUtc == current.ResetsAtUtc
                    && point.ObservedAtUtc >= cutoff
                    && point.ObservedAtUtc <= nowUtc
                    && point.UsedPercent <= current.UsedPercent)
                .Select(point => new BucketSample(
                    current.BucketId,
                    current.LimitKey,
                    current.TrackLabel,
                    current.WindowLabel,
                    current.ForecastWindowLabel,
                    current.WindowDurationMins,
                    point.UsedPercent,
                    current.ResetsAtUtc,
                    point.ObservedAtUtc,
                    StartsAfterMeasurementGap: false))
                .ToList();

            if (_observations.TryGetValue(current.BucketId, out var existing)
                && existing.ResetsAtUtc == current.ResetsAtUtc)
            {
                candidates.AddRange(existing.RunwaySamples.Select(sample => sample with
                {
                    LimitKey = current.LimitKey,
                    TrackLabel = current.TrackLabel,
                    WindowLabel = current.WindowLabel,
                    ForecastWindowLabel = current.ForecastWindowLabel,
                    WindowDurationMins = current.WindowDurationMins,
                    ResetsAtUtc = current.ResetsAtUtc
                }));
            }

            var ordered = candidates
                .OrderBy(sample => sample.ObservedAtUtc)
                .ThenBy(sample => sample.UsedPercent)
                .ToArray();
            if (ordered.Length == 0)
            {
                continue;
            }

            var monotonic = new List<BucketSample>(ordered.Length);
            foreach (var candidate in ordered)
            {
                if (monotonic.Count > 0
                    && candidate.ObservedAtUtc == monotonic[^1].ObservedAtUtc)
                {
                    if (candidate.UsedPercent > monotonic[^1].UsedPercent)
                    {
                        monotonic[^1] = candidate;
                    }

                    continue;
                }

                if (monotonic.Count > 0 && candidate.UsedPercent < monotonic[^1].UsedPercent)
                {
                    continue;
                }

                monotonic.Add(candidate);
            }

            if (monotonic.Count == 0)
            {
                continue;
            }

            var merged = new BucketObservation(monotonic[0], isRestored: true);
            for (var index = 1; index < monotonic.Count; index++)
            {
                AddRunwaySample(merged, monotonic[index]);
            }

            if (existing is not null && existing.RunwaySamples.SequenceEqual(merged.RunwaySamples))
            {
                continue;
            }

            merged.IdleDrainBaseline = current;
            _observations[current.BucketId] = merged;
            _runwayStateDirty = true;
        }
    }

    private static bool TryRestoreBucketObservation(
        IReadOnlyList<RunwayObservationSample> storedSamples,
        DateTimeOffset nowUtc,
        out BucketObservation observation)
    {
        observation = null!;
        if (storedSamples.Count is 0 or > MaximumRunwaySamples)
        {
            return false;
        }

        var first = storedSamples[0];
        if (string.IsNullOrWhiteSpace(first.BucketId)
            || string.IsNullOrWhiteSpace(first.LimitKey)
            || first.ResetsAtUtc <= nowUtc
            || first.ObservedAtUtc > nowUtc
            || !double.IsFinite(first.UsedPercent)
            || first.UsedPercent is < 0 or > 100)
        {
            return false;
        }

        var history = ResolveTrendHistory(first.WindowDurationMins);
        var cutoff = nowUtc - history;

        var restored = new List<BucketSample>(storedSamples.Count);
        BucketSample? previous = null;
        foreach (var stored in storedSamples)
        {
            if (!string.Equals(stored.BucketId, first.BucketId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.LimitKey, first.LimitKey, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.TrackLabel, first.TrackLabel, StringComparison.Ordinal)
                || !string.Equals(stored.WindowLabel, first.WindowLabel, StringComparison.Ordinal)
                || !string.Equals(stored.ForecastWindowLabel, first.ForecastWindowLabel, StringComparison.Ordinal)
                || stored.WindowDurationMins != first.WindowDurationMins
                || stored.ResetsAtUtc != first.ResetsAtUtc
                || stored.ResetsAtUtc <= nowUtc
                || stored.ObservedAtUtc > nowUtc
                || !double.IsFinite(stored.UsedPercent)
                || stored.UsedPercent is < 0 or > 100)
            {
                return false;
            }

            var sample = new BucketSample(
                stored.BucketId,
                stored.LimitKey,
                stored.TrackLabel,
                stored.WindowLabel,
                stored.ForecastWindowLabel,
                stored.WindowDurationMins,
                stored.UsedPercent,
                stored.ResetsAtUtc,
                stored.ObservedAtUtc,
                stored.StartsAfterMeasurementGap);
            if (previous is BucketSample previousSample
                && (sample.ObservedAtUtc <= previousSample.ObservedAtUtc
                    || sample.UsedPercent < previousSample.UsedPercent))
            {
                return false;
            }

            previous = sample;
            if (sample.ObservedAtUtc >= cutoff)
            {
                restored.Add(sample);
            }
        }

        if (restored.Count == 0)
        {
            return false;
        }

        observation = new BucketObservation(restored[0], isRestored: true);
        observation.RunwaySamples.Clear();
        observation.RunwaySamples.AddRange(restored);
        observation.IdleDrainBaseline = restored[^1];
        return true;
    }

    private void PersistRunwayObservations()
    {
        if (_runwayObservationStateStore is null
            || !_restoreComplete
            || _restoreFailedClosed
            || !_runwayStateDirty)
        {
            return;
        }

        var samples = _observations.Values
            .Take(MaximumPersistedRunwayBuckets)
            .SelectMany(observation => observation.RunwaySamples)
            .Select(sample => new RunwayObservationSample(
                sample.BucketId,
                sample.LimitKey,
                sample.TrackLabel,
                sample.WindowLabel,
                sample.ForecastWindowLabel,
                sample.WindowDurationMins,
                sample.UsedPercent,
                sample.ResetsAtUtc,
                sample.ObservedAtUtc,
                sample.StartsAfterMeasurementGap))
            .ToArray();
        try
        {
            if (_runwayObservationStateStore.Save(new RunwayObservationState(RunwayObservationStateStore.CurrentSchemaVersion, samples)))
            {
                _runwayStateDirty = false;
            }
        }
        catch (Exception)
        {
        }
    }

    private bool RemoveExpiredObservationAnchors(DateTimeOffset nowUtc)
    {
        var removed = false;
        foreach (var bucketId in _observations.Keys
                     .Where(bucketId => _observations[bucketId].ResetsAtUtc <= nowUtc)
                     .ToArray())
        {
            _observations.Remove(bucketId);
            removed = true;
        }

        return removed;
    }

    private UsageSignalsSnapshot BuildSnapshot(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc,
        IReadOnlyList<LimitRunwaySignal> runwaySignals,
        IReadOnlyList<LimitRunwayForecast> runwayForecasts,
        IReadOnlyList<LimitUsageTrend> usageTrends,
        IdleDrainIncident? idleDrainIncident)
    {
        return new UsageSignalsSnapshot
        {
            RunwaySignals = runwaySignals
                .OrderBy(signal => signal.TimeToExhaustion)
                .ToList(),
            RunwayForecasts = runwayForecasts
                .OrderBy(forecast => ForecastSortOrder(forecast.State))
                .ThenBy(forecast => forecast.ExhaustsAtUtc ?? forecast.ResetsAtUtc)
                .ToList(),
            UsageTrends = usageTrends
                .OrderBy(trend => trend.WindowDurationMins ?? int.MaxValue)
                .ThenBy(trend => trend.WindowLabel, StringComparer.OrdinalIgnoreCase)
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
                "#EF4444",
                Kind: UsageAttentionSignalKind.Sync));
            return;
        }

        if (snapshot.SyncStatus == SyncStatus.Stale)
        {
            signals.Add(new UsageAttentionSignal(
                1,
                "SYNC",
                "Live data is stale",
                "Showing last good usage data until the next successful sync.",
                "#F97316",
                Kind: UsageAttentionSignalKind.Sync));
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
            "Usage increased while idle",
            incident.SummaryText,
            incident.AccentBrush,
            incident.DiagnosticText,
            "idle-drain",
            UsageAttentionSignalKind.Idle));
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

    private static IReadOnlyList<LimitRunwayForecast> BuildMockRunwayForecasts(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        var forecasts = new List<LimitRunwayForecast>();

        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var sample)
                || sample.ResetsAtUtc <= nowUtc)
            {
                continue;
            }

            if (sample.UsedPercent >= 100)
            {
                forecasts.Add(BuildInitialForecast(sample, isMock: true));
                continue;
            }

            if (sample.UsedPercent >= 90 && sample.ResetsAtUtc > nowUtc.AddMinutes(5))
            {
                var resetRemaining = sample.ResetsAtUtc - nowUtc;
                var timeToExhaustion = TimeSpan.FromMinutes(Math.Clamp((100 - sample.UsedPercent) * 6, 10, 45));
                if (timeToExhaustion >= resetRemaining)
                {
                    timeToExhaustion = TimeSpan.FromTicks(resetRemaining.Ticks / 2);
                }

                var exhaustsAt = nowUtc + timeToExhaustion;
                var earliestExhaustsAt = nowUtc + TimeSpan.FromTicks(timeToExhaustion.Ticks / 2);
                var latestExhaustsAt = exhaustsAt.AddHours(6);
                var latestAllowed = sample.ResetsAtUtc.AddMinutes(-2);
                if (latestExhaustsAt > latestAllowed)
                {
                    latestExhaustsAt = latestAllowed;
                }

                forecasts.Add(new LimitRunwayForecast(
                    sample.BucketId,
                    sample.LimitKey,
                    sample.TrackLabel,
                    sample.ForecastWindowLabel,
                    sample.WindowDurationMins,
                    sample.ResetsAtUtc,
                    sample.UsedPercent,
                    LimitRunwayForecastState.AtRisk,
                    exhaustsAt,
                    0,
                    (100 - sample.UsedPercent) / Math.Max(timeToExhaustion.TotalHours, 0.01),
                    TimeSpan.FromMinutes(11),
                    IsWithinWarningWindow(sample, timeToExhaustion),
                    IsMock: true,
                    Confidence: LimitRunwayForecastConfidence.Medium,
                    EarliestExhaustsAtUtc: earliestExhaustsAt,
                    LatestExhaustsAtUtc: latestExhaustsAt,
                    SampleCount: 5));
                continue;
            }

            forecasts.Add(new LimitRunwayForecast(
                sample.BucketId,
                sample.LimitKey,
                sample.TrackLabel,
                sample.ForecastWindowLabel,
                sample.WindowDurationMins,
                sample.ResetsAtUtc,
                sample.UsedPercent,
                LimitRunwayForecastState.OnTrack,
                null,
                Math.Max(1, 100 - sample.UsedPercent - 4),
                Math.Max(0.5, sample.UsedPercent / 24),
                TimeSpan.FromMinutes(11),
                IsActionable: false,
                IsMock: true));
        }

        return forecasts;
    }

    private IReadOnlyList<LimitUsageTrend> BuildUsageTrends()
    {
        return _observations.Values
            .Where(observation => observation.RunwaySamples.Count > 0)
            .Select(observation =>
            {
                var latest = observation.RunwaySamples[^1];
                var gaps = observation.RunwaySamples
                    .Select((sample, index) => (Sample: sample, Index: index))
                    .Where(item => item.Index > 0 && item.Sample.StartsAfterMeasurementGap)
                    .Select(item => new LimitUsageGap(
                        observation.RunwaySamples[item.Index - 1].ObservedAtUtc,
                        item.Sample.ObservedAtUtc))
                    .ToArray();
                return new LimitUsageTrend(
                    latest.BucketId,
                    latest.LimitKey,
                    latest.TrackLabel,
                    latest.ForecastWindowLabel,
                    latest.WindowDurationMins,
                    latest.ResetsAtUtc,
                    observation.RunwaySamples
                        .Select(sample => new LimitUsagePoint(sample.ObservedAtUtc, sample.UsedPercent))
                        .ToArray(),
                    IsMock: false)
                {
                    MeasurementGaps = gaps
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<LimitUsageTrend> BuildMockUsageTrends(
        UsageSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        double[] progress = [0, 0.12, 0.24, 0.31, 0.43, 0.51, 0.63, 0.73, 0.82, 0.89, 0.93, 0.97, 1];
        var trends = new List<LimitUsageTrend>();

        foreach (var bucket in snapshot.Buckets)
        {
            if (!TryCreateSample(bucket, nowUtc, out var sample)
                || sample.ResetsAtUtc <= nowUtc)
            {
                continue;
            }

            var isWeekly = IsWeekly(sample);
            var history = isWeekly ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(42);
            var startPercent = Math.Max(4, sample.UsedPercent - (isWeekly ? 38 : 78));
            var points = progress
                .Select((ratio, index) => new LimitUsagePoint(
                    nowUtc - history + TimeSpan.FromTicks(history.Ticks * index / (progress.Length - 1)),
                    Math.Clamp(startPercent + ((sample.UsedPercent - startPercent) * ratio), 0, 100)))
                .ToArray();

            trends.Add(new LimitUsageTrend(
                sample.BucketId,
                sample.LimitKey,
                sample.TrackLabel,
                sample.ForecastWindowLabel,
                sample.WindowDurationMins,
                sample.ResetsAtUtc,
                points,
                IsMock: true));
        }

        return trends;
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
            var summary = FormatIdleUsageSummary(sample.WindowLabel, before, sample.UsedPercent, observationDuration);
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
                signal.AccentBrush,
                Kind: UsageAttentionSignalKind.Runway));
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
            "#F97316",
            Kind: UsageAttentionSignalKind.RateLimit,
            ScopeId: RateLimitBucketKeys.GetWindowScope(bucket)));
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
            "#F59E0B",
            Kind: UsageAttentionSignalKind.ResetCredit));
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
            "#1F73FF",
            Kind: UsageAttentionSignalKind.DailyUsage));
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
            "#1F73FF",
            Kind: UsageAttentionSignalKind.ProjectUsage));
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

    private static LimitRunwaySignal? TryBuildRunwaySignal(
        LimitRunwayForecast forecast,
        BucketSample current,
        DateTimeOffset nowUtc)
    {
        if (forecast.State is not LimitRunwayForecastState.AtRisk
            || !forecast.IsActionable
            || forecast.ExhaustsAtUtc is not DateTimeOffset exhaustsAt)
        {
            return null;
        }

        var timeToExhaustion = exhaustsAt - nowUtc;

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

    private static LimitRunwayForecast BuildInitialForecast(BucketSample current, bool isMock)
    {
        return new LimitRunwayForecast(
            current.BucketId,
            current.LimitKey,
            current.TrackLabel,
            current.ForecastWindowLabel,
            current.WindowDurationMins,
            current.ResetsAtUtc,
            current.UsedPercent,
            current.UsedPercent >= 100
                ? LimitRunwayForecastState.Exhausted
                : LimitRunwayForecastState.Learning,
            current.UsedPercent >= 100 ? current.ObservedAtUtc : null,
            current.UsedPercent >= 100 ? 0 : null,
            null,
            null,
            IsActionable: current.UsedPercent >= 100,
            IsMock: isMock,
            Confidence: LimitRunwayForecastConfidence.Low,
            SampleCount: 1);
    }

    private static LimitRunwayForecast BuildRunwayForecast(IReadOnlyList<BucketSample> samples)
    {
        var current = samples[^1];
        var elapsed = CalculateMeasuredObservationDuration(samples);
        if (current.UsedPercent >= 100)
        {
            var exhaustedRate = CalculateDiscountedUsageRate(samples);
            return BuildInitialForecast(current, isMock: false) with
            {
                ObservationDuration = elapsed,
                Confidence = CalculateEvidenceConfidence(samples, elapsed, exhaustedRate),
                SampleCount = samples.Count,
                ExhaustionProbabilityBeforeReset = 1
            };
        }

        if (samples.Count < MinimumRunwaySamples || elapsed < MinimumRunwayObservation)
        {
            return BuildInitialForecast(current, isMock: false) with
            {
                ObservationDuration = elapsed,
                SampleCount = samples.Count
            };
        }

        var usedDelta = current.UsedPercent - samples[0].UsedPercent;
        if (usedDelta <= MinimumRunwayMovement)
        {
            return new LimitRunwayForecast(
                current.BucketId,
                current.LimitKey,
                current.TrackLabel,
                current.ForecastWindowLabel,
                current.WindowDurationMins,
                current.ResetsAtUtc,
                current.UsedPercent,
                LimitRunwayForecastState.Stable,
                null,
                100 - current.UsedPercent,
                0,
                elapsed,
                IsActionable: false,
                IsMock: false,
                Confidence: LimitRunwayForecastConfidence.Low,
                SampleCount: samples.Count);
        }

        var rate = CalculateDiscountedUsageRate(samples);
        if (rate.PercentPerMinute <= 0 || rate.WeightedExposureMinutes <= 0)
        {
            return new LimitRunwayForecast(
                current.BucketId,
                current.LimitKey,
                current.TrackLabel,
                current.ForecastWindowLabel,
                current.WindowDurationMins,
                current.ResetsAtUtc,
                current.UsedPercent,
                LimitRunwayForecastState.Stable,
                null,
                100 - current.UsedPercent,
                0,
                elapsed,
                IsActionable: false,
                IsMock: false,
                Confidence: LimitRunwayForecastConfidence.Low,
                SampleCount: samples.Count,
                ExhaustionProbabilityBeforeReset: 0);
        }

        var confidence = CalculateEvidenceConfidence(samples, elapsed, rate);
        var remaining = Math.Max(0, 100 - current.UsedPercent);
        var remainingPoints = Math.Max(1, (int)Math.Ceiling(remaining));
        var posteriorShape = rate.WeightedIncrementCount + RunwayPriorShape;
        var posteriorPercentPerMinute = posteriorShape / rate.WeightedExposureMinutes;
        var timeToResetMinutes = Math.Max(0, (current.ResetsAtUtc - current.ObservedAtUtc).TotalMinutes);
        var exhaustionProbability = GammaPoissonForecastMath.ExhaustionProbability(
            remainingPoints,
            posteriorShape,
            rate.WeightedExposureMinutes,
            timeToResetMinutes);
        var timeToExhaustion = TimeSpan.FromMinutes(remainingPoints / posteriorPercentPerMinute);
        var exhaustsAt = current.ObservedAtUtc + timeToExhaustion;
        var earliestMinutes = GammaPoissonForecastMath.ExhaustionQuantileMinutes(
            ForecastLowerQuantile,
            remainingPoints,
            posteriorShape,
            rate.WeightedExposureMinutes);
        var latestMinutes = GammaPoissonForecastMath.ExhaustionQuantileMinutes(
            ForecastUpperQuantile,
            remainingPoints,
            posteriorShape,
            rate.WeightedExposureMinutes);
        var earliestExhaustsAt = earliestMinutes is double earliest
            ? current.ObservedAtUtc + TimeSpan.FromMinutes(earliest)
            : (DateTimeOffset?)null;
        var latestExhaustsAt = latestMinutes is double latest
            ? current.ObservedAtUtc + TimeSpan.FromMinutes(latest)
            : (DateTimeOffset?)null;
        var projectedRemaining = Math.Max(
            0,
            remaining - (posteriorPercentPerMinute * timeToResetMinutes));
        var isAtRisk = GammaPoissonForecastMath.IsAtRisk(exhaustionProbability);
        var isActionable = GammaPoissonForecastMath.HasActionableProbability(exhaustionProbability)
            && confidence is not LimitRunwayForecastConfidence.Low
            && exhaustsAt < current.ResetsAtUtc
            && IsWithinWarningWindow(current, timeToExhaustion);
        var projectionPoints = BuildRunwayProjection(
            current,
            posteriorPercentPerMinute,
            posteriorShape,
            rate.WeightedExposureMinutes,
            remainingPoints,
            latestExhaustsAt ?? exhaustsAt);

        return new LimitRunwayForecast(
            current.BucketId,
            current.LimitKey,
            current.TrackLabel,
            current.ForecastWindowLabel,
            current.WindowDurationMins,
            current.ResetsAtUtc,
            current.UsedPercent,
            isAtRisk ? LimitRunwayForecastState.AtRisk : LimitRunwayForecastState.OnTrack,
            exhaustsAt,
            projectedRemaining,
            posteriorPercentPerMinute * 60,
            elapsed,
            isActionable,
            IsMock: false,
            Confidence: confidence,
            EarliestExhaustsAtUtc: earliestExhaustsAt,
            LatestExhaustsAtUtc: latestExhaustsAt,
            SampleCount: samples.Count,
            ExhaustionProbabilityBeforeReset: exhaustionProbability,
            ProjectionPoints: projectionPoints);
    }

    private static TimeSpan CalculateMeasuredObservationDuration(IReadOnlyList<BucketSample> samples)
    {
        var measuredTicks = 0L;
        for (var index = 1; index < samples.Count; index++)
        {
            if (samples[index].StartsAfterMeasurementGap)
            {
                continue;
            }

            var duration = samples[index].ObservedAtUtc - samples[index - 1].ObservedAtUtc;
            if (duration > TimeSpan.Zero)
            {
                measuredTicks += duration.Ticks;
            }
        }

        return TimeSpan.FromTicks(measuredTicks);
    }

    private static DiscountedUsageRate CalculateDiscountedUsageRate(IReadOnlyList<BucketSample> samples)
    {
        var current = samples[^1];
        var halfLife = IsWeekly(current) ? WeeklyRunwayHalfLife : ShortRunwayHalfLife;
        var weightedIncrementCount = 0d;
        var weightedExposureMinutes = 0d;
        var positiveIntervals = 0;

        for (var index = 1; index < samples.Count; index++)
        {
            var previous = samples[index - 1];
            var sample = samples[index];
            if (sample.StartsAfterMeasurementGap)
            {
                continue;
            }

            var exposureMinutes = (sample.ObservedAtUtc - previous.ObservedAtUtc).TotalMinutes;
            if (exposureMinutes <= 0)
            {
                continue;
            }

            var midpoint = previous.ObservedAtUtc + TimeSpan.FromTicks((sample.ObservedAtUtc - previous.ObservedAtUtc).Ticks / 2);
            var ageMinutes = Math.Max(0, (current.ObservedAtUtc - midpoint).TotalMinutes);
            var weight = Math.Exp(-Math.Log(2) * ageMinutes / halfLife.TotalMinutes);
            var previousCount = Math.Round(previous.UsedPercent, MidpointRounding.AwayFromZero);
            var currentCount = Math.Round(sample.UsedPercent, MidpointRounding.AwayFromZero);
            var increment = Math.Max(0, currentCount - previousCount);
            if (increment > 0)
            {
                positiveIntervals++;
            }

            weightedIncrementCount += weight * increment;
            weightedExposureMinutes += weight * exposureMinutes;
        }

        var percentPerMinute = weightedExposureMinutes <= 0
            ? 0
            : weightedIncrementCount / weightedExposureMinutes;
        return new DiscountedUsageRate(
            percentPerMinute,
            weightedIncrementCount,
            weightedExposureMinutes,
            positiveIntervals);
    }

    private static LimitRunwayForecastConfidence CalculateEvidenceConfidence(
        IReadOnlyList<BucketSample> samples,
        TimeSpan elapsed,
        DiscountedUsageRate rate)
    {
        var highDuration = IsWeekly(samples[^1]) ? TimeSpan.FromHours(2) : TimeSpan.FromMinutes(20);
        var posteriorShape = rate.WeightedIncrementCount + RunwayPriorShape;
        var posteriorCoefficientOfVariation = posteriorShape <= 0
            ? double.PositiveInfinity
            : 1 / Math.Sqrt(posteriorShape);
        if (samples.Count >= 8
            && rate.PositiveIntervals >= 3
            && elapsed >= highDuration
            && rate.WeightedIncrementCount >= 8
            && posteriorCoefficientOfVariation <= 0.35)
        {
            return LimitRunwayForecastConfidence.High;
        }

        if (samples.Count >= 3
            && rate.PositiveIntervals >= 2
            && elapsed >= MinimumRunwayObservation
            && rate.WeightedIncrementCount >= 2)
        {
            return LimitRunwayForecastConfidence.Medium;
        }

        return LimitRunwayForecastConfidence.Low;
    }

    private static IReadOnlyList<LimitRunwayProjectionPoint> BuildRunwayProjection(
        BucketSample current,
        double percentPerMinute,
        double posteriorShape,
        double posteriorExposureMinutes,
        int remainingPoints,
        DateTimeOffset forecastHorizon)
    {
        var history = IsWeekly(current) ? WeeklyRunwayHistory : ShortRunwayHistory;
        var minimumEnd = current.ObservedAtUtc + history;
        var desiredEnd = forecastHorizon > minimumEnd ? forecastHorizon : minimumEnd;
        var projectionEnd = current.ResetsAtUtc <= desiredEnd ? current.ResetsAtUtc : desiredEnd;
        if (projectionEnd <= current.ObservedAtUtc)
        {
            return [];
        }

        var duration = projectionEnd - current.ObservedAtUtc;
        return Enumerable.Range(0, ForecastProjectionPointCount)
            .Select(index =>
            {
                var timestamp = current.ObservedAtUtc
                    + TimeSpan.FromTicks(duration.Ticks * index / (ForecastProjectionPointCount - 1));
                var futureMinutes = Math.Max(0, (timestamp - current.ObservedAtUtc).TotalMinutes);
                var lowerIncrement = GammaPoissonForecastMath.CountQuantile(
                    ForecastLowerQuantile,
                    remainingPoints,
                    posteriorShape,
                    posteriorExposureMinutes,
                    futureMinutes);
                var upperIncrement = GammaPoissonForecastMath.CountQuantile(
                    ForecastUpperQuantile,
                    remainingPoints,
                    posteriorShape,
                    posteriorExposureMinutes,
                    futureMinutes);
                var lower = Math.Clamp(current.UsedPercent + lowerIncrement, current.UsedPercent, 100);
                var upper = Math.Clamp(current.UsedPercent + upperIncrement, lower, 100);
                var expected = Math.Clamp(
                    current.UsedPercent + (percentPerMinute * futureMinutes),
                    lower,
                    upper);
                return new LimitRunwayProjectionPoint(timestamp, expected, lower, upper);
            })
            .ToArray();
    }

    private static bool IsWithinWarningWindow(BucketSample sample, TimeSpan timeToExhaustion)
    {
        var warningWindow = IsWeekly(sample) ? WeeklyRunwayWarning : ShortWindowRunwayWarning;
        return timeToExhaustion <= warningWindow;
    }

    private static int ForecastSortOrder(LimitRunwayForecastState state)
    {
        return state switch
        {
            LimitRunwayForecastState.Exhausted => 0,
            LimitRunwayForecastState.AtRisk => 1,
            LimitRunwayForecastState.Learning => 2,
            LimitRunwayForecastState.Stable => 3,
            LimitRunwayForecastState.OnTrack => 4,
            _ => 5
        };
    }

    private static bool AddRunwaySample(BucketObservation observation, BucketSample current)
    {
        var samples = observation.RunwaySamples;
        var latest = samples[^1];
        if (current == latest)
        {
            return false;
        }
        if (current.ObservedAtUtc <= latest.ObservedAtUtc)
        {
            return false;
        }

        if (current.ResetsAtUtc != latest.ResetsAtUtc)
        {
            samples.Clear();
            samples.Add(current);
            return true;
        }

        // Usage inside one quota window is cumulative. A lower value with the same reset
        // identifies a stale/transient reading and must not erase the recorded history.
        if (current.UsedPercent < latest.UsedPercent)
        {
            return false;
        }

        if (observation.Restored
            && current.ObservedAtUtc - latest.ObservedAtUtc > MeasurementGapThreshold)
        {
            current = current with { StartsAfterMeasurementGap = true };
        }

        if (samples.Count >= 3
            && current.UsedPercent == latest.UsedPercent
            && samples[^2].UsedPercent == latest.UsedPercent
            && !current.StartsAfterMeasurementGap
            && !latest.StartsAfterMeasurementGap
            && current.ObservedAtUtc - samples[^2].ObservedAtUtc <= ResolveFlatSampleCheckpointInterval(current))
        {
            samples[^1] = current;
        }
        else
        {
            samples.Add(current);
        }

        var history = ResolveTrendHistory(current.WindowDurationMins);
        var cutoff = current.ObservedAtUtc - history;
        while (samples.Count > 1 && samples[0].ObservedAtUtc < cutoff)
        {
            samples.RemoveAt(0);
        }

        if (samples.Count > MaximumRunwaySamples)
        {
            CompactRunwaySamples(samples);
        }

        return true;
    }

    private static IReadOnlyList<BucketSample> SelectForecastSamples(IReadOnlyList<BucketSample> samples)
    {
        var current = samples[^1];
        var history = IsWeekly(current) ? WeeklyRunwayHistory : ShortRunwayHistory;
        var cutoff = current.ObservedAtUtc - history;
        var firstRecentIndex = 0;
        while (firstRecentIndex < samples.Count - 1 && samples[firstRecentIndex].ObservedAtUtc < cutoff)
        {
            firstRecentIndex++;
        }

        return firstRecentIndex == 0 ? samples : samples.Skip(firstRecentIndex).ToArray();
    }

    private static TimeSpan ResolveTrendHistory(int? windowDurationMins)
    {
        if (windowDurationMins is not int minutes || minutes <= 0)
        {
            return WeeklyRunwayHistory;
        }

        return TimeSpan.FromMinutes(Math.Min(minutes, (int)MaximumTrendHistory.TotalMinutes));
    }

    private static TimeSpan ResolveFlatSampleCheckpointInterval(BucketSample sample) =>
        IsWeekly(sample) ? WeeklyFlatSampleCheckpointInterval : ShortFlatSampleCheckpointInterval;

    private static void CompactRunwaySamples(List<BucketSample> samples)
    {
        var source = samples.ToArray();
        var requiredIndexes = new HashSet<int> { 0, source.Length - 1 };
        for (var index = 1; index < source.Length; index++)
        {
            if (!source[index].StartsAfterMeasurementGap)
            {
                continue;
            }

            requiredIndexes.Add(index - 1);
            requiredIndexes.Add(index);
        }

        var selectedIndexes = requiredIndexes.Count >= MaximumRunwaySamples
            ? requiredIndexes.OrderDescending().Take(MaximumRunwaySamples)
            : requiredIndexes.Concat(
                Enumerable.Range(0, MaximumRunwaySamples - requiredIndexes.Count)
                    .Select(index => (int)Math.Round(
                        index * (source.Length - 1d)
                        / Math.Max(1, MaximumRunwaySamples - requiredIndexes.Count - 1d))));
        var compacted = selectedIndexes
            .Distinct()
            .Order()
            .Select(index => source[index])
            .ToArray();
        samples.Clear();
        samples.AddRange(compacted);
    }

    private static bool ShouldRebaseIdleDrain(BucketSample baseline, BucketSample current, TimeSpan idleTime)
    {
        var elapsed = current.ObservedAtUtc - baseline.ObservedAtUtc;
        return elapsed <= TimeSpan.Zero
            || idleTime < elapsed
            || current.UsedPercent < baseline.UsedPercent;
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

        var summary = FormatIdleUsageSummary(current.WindowLabel, previous.UsedPercent, current.UsedPercent, elapsed);
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
        var forecastWindowLabel = FirstNonEmpty(
            bucket.WindowLabel,
            bucket.Label,
            bucket.WindowDurationMins is int minutes ? FormatWindowDuration(minutes) : null);
        var trackLabel = FirstNonEmpty(bucket.GroupLabel, bucket.LimitName, bucket.LimitId, limitKey);
        var bucketId = $"{limitKey}|{bucket.WindowDurationMins?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";

        sample = new BucketSample(
            bucketId,
            limitKey,
            trackLabel,
            windowLabel,
            forecastWindowLabel,
            bucket.WindowDurationMins,
            Math.Clamp(usedPercent, 0, 100),
            resetsAt,
            nowUtc);
        return true;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Usage";
    }

    private static string FormatWindowDuration(int minutes)
    {
        return minutes >= WeeklyWindowMinutes
            ? $"{Math.Max(1, minutes / 1_440)}d"
            : minutes >= 60
                ? $"{Math.Max(1, minutes / 60)}h"
                : $"{minutes}m";
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

    private static string FormatIdleUsageSummary(string windowLabel, double beforePercent, double afterPercent, TimeSpan duration)
    {
        const string windowSuffix = " Window";
        var displayLabel = windowLabel.EndsWith(windowSuffix, StringComparison.OrdinalIgnoreCase)
            ? windowLabel[..^windowSuffix.Length]
            : windowLabel;

        return $"{displayLabel} usage increased from {FormatPercent(beforePercent)} to {FormatPercent(afterPercent)} in {FormatDuration(duration)}.";
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
        string TrackLabel,
        string WindowLabel,
        string ForecastWindowLabel,
        int? WindowDurationMins,
        double UsedPercent,
        DateTimeOffset ResetsAtUtc,
        DateTimeOffset ObservedAtUtc,
        bool StartsAfterMeasurementGap = false);

    private sealed class BucketObservation
    {
        public BucketObservation(BucketSample initial, bool isRestored = false)
        {
            RunwaySamples.Add(initial);
            IdleDrainBaseline = initial;
            Restored = isRestored;
        }

        public List<BucketSample> RunwaySamples { get; } = [];

        public BucketSample IdleDrainBaseline { get; set; }

        public bool Restored { get; set; }

        public DateTimeOffset ResetsAtUtc => RunwaySamples[^1].ResetsAtUtc;
    }

    private readonly record struct DiscountedUsageRate(
        double PercentPerMinute,
        double WeightedIncrementCount,
        double WeightedExposureMinutes,
        int PositiveIntervals);
}
