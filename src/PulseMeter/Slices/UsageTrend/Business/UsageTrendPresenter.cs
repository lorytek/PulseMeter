using System.Globalization;
using PulseMeter.Slices.UsageTrend.UI;

namespace PulseMeter.Slices.UsageTrend.Business;

public interface IUsageTrendPresenter
{
    UsageTrendChartModel? BuildChart(
        LimitUsageTrend trend,
        LimitRunwayForecast? forecast,
        DateTimeOffset now,
        bool showProjection,
        bool showRange,
        UsageTrendForecastReference? referenceForecast = null,
        int? selectedBlockDurationMinutes = null,
        IReadOnlyList<LimitRunwayForecast>? liveForecasts = null);
}

public sealed class UsageTrendPresenter : IUsageTrendPresenter
{
    private const int ProjectionPointCount = 13;
    private const int EarlyBaselineHours = 8;
    private const int EarlyBaselineDays = 2;
    private const int EstablishedBaselineHours = 24;
    private const int EstablishedBaselineDays = 3;
    private const int EarlyMinimumSpanHours = 18;
    private const int EstablishedMinimumSpanHours = 42;
    private const int MinimumProgressiveCurrentMinutes = 15;
    private const double BaselineHalfLifeDays = 7;
    private const double UnfavorableVarianceThresholdPoints = 1;

    public UsageTrendChartModel? BuildChart(
        LimitUsageTrend trend,
        LimitRunwayForecast? forecast,
        DateTimeOffset now,
        bool showProjection,
        bool showRange,
        UsageTrendForecastReference? referenceForecast = null,
        int? selectedBlockDurationMinutes = null,
        IReadOnlyList<LimitRunwayForecast>? liveForecasts = null)
    {
        ArgumentNullException.ThrowIfNull(trend);

        var validPoints = trend.Points
            .Where(point => double.IsFinite(point.UsedPercent))
            .Where(point => point.ObservedAtUtc <= now.AddMinutes(1))
            .OrderBy(point => point.ObservedAtUtc)
            .ToArray();

        if (validPoints.Length == 0)
        {
            return null;
        }

        var windowStart = ResolveWindowStart(trend, validPoints[0].ObservedAtUtc);
        var actual = validPoints
            .Where(point => point.ObservedAtUtc >= windowStart)
            .Select(point => new UsageTrendPoint(point.ObservedAtUtc, Math.Clamp(point.UsedPercent, 0, 100)))
            .ToArray();

        if (actual.Length == 0)
        {
            return null;
        }

        var last = actual[^1];
        var measurementGaps = trend.MeasurementGaps
            .Where(gap => gap.EndedAtUtc > gap.StartedAtUtc)
            .Where(gap => gap.EndedAtUtc >= windowStart && gap.StartedAtUtc <= last.Timestamp)
            .Select(gap => new UsageTrendGap(
                gap.StartedAtUtc < windowStart ? windowStart : gap.StartedAtUtc,
                gap.EndedAtUtc > last.Timestamp ? last.Timestamp : gap.EndedAtUtc))
            .Where(gap => gap.EndedAt > gap.StartedAt)
            .ToArray();
        var projectionEnd = trend.ResetsAtUtc;
        if (projectionEnd <= last.Timestamp)
        {
            projectionEnd = last.Timestamp.AddMinutes(15);
        }

        var pacePerHour = ResolvePacePerHour(actual, forecast);
        var sustainablePacePerHour = ResolveSustainablePace(last, trend.ResetsAtUtc);
        var statisticalProjection = BuildStatisticalProjection(last, projectionEnd, forecast);
        var fullProjection = statisticalProjection.Length > 1
            ? statisticalProjection
            : pacePerHour is double pace
                ? BuildProjection(last, projectionEnd, pace)
                : [];
        var budgetLimitedProjection = TrimAtBudgetLimit(fullProjection);
        var projected = showProjection ? budgetLimitedProjection : [];
        var referenceProjected = showProjection
            ? BuildElapsedReferenceProjection(referenceForecast, trend.ResetsAtUtc, last.Timestamp)
            : [];
        var unfavorableVariance = showProjection
            ? BuildUnfavorableVarianceSegments(actual, referenceProjected, measurementGaps)
            : [];
        var range = showRange && fullProjection.Length > 1
            ? BuildForecastRange(fullProjection, last, forecast, pacePerHour)
            : Array.Empty<UsageTrendBandPoint>();
        var sustainable = BuildSustainableProjection(last, trend.ResetsAtUtc, sustainablePacePerHour);
        var (forecastWindowStart, forecastWindowEnd) = ResolveForecastWindow(forecast, last.Timestamp, trend.ResetsAtUtc);
        var forecastLimitAt = ResolveProjectedLimitAt(budgetLimitedProjection)
            ?? ResolvePointExhaustion(last, forecast, trend.ResetsAtUtc, pacePerHour);
        if (forecastLimitAt <= last.Timestamp || forecastLimitAt > trend.ResetsAtUtc)
        {
            forecastLimitAt = null;
        }
        var summary = BuildRunwaySummary(
            actual,
            measurementGaps,
            trend.BaselineHourlyRates,
            trend.CurrentHourActivityEvidence,
            trend.ActivityCoverage,
            trend.LocalActivityUtcHours,
            last,
            trend.WindowDurationMins,
            forecast,
            trend.ResetsAtUtc,
            now,
            pacePerHour,
            sustainablePacePerHour,
            forecastWindowStart,
            forecastWindowEnd,
            forecastLimitAt);
        var blockAdvisor = BuildBlockAdvisor(
            trend.WindowDurationMins,
            forecast,
            summary.Momentum,
            now,
            trend.ResetsAtUtc,
            selectedBlockDurationMinutes);
        IReadOnlyList<LimitRunwayForecast> forecastsForConstraint = liveForecasts
            ?? (forecast is null ? Array.Empty<LimitRunwayForecast>() : [forecast]);
        var nextConstraint = BuildNextConstraint(forecastsForConstraint, now);

        var projectedAtReset = projected.LastOrDefault()?.UsedPercent;
        var projectionSummary = projectedAtReset is double projectedPercent
            ? $" Projected usage is {projectedPercent.ToString("0", CultureInfo.InvariantCulture)}% by the chart horizon."
            : " A projection is not available yet.";
        var historySummary = windowStart < actual[0].Timestamp
            ? $" The quota window began {FormatLocalDateTime(windowStart)}. The first recorded sample is {FormatLocalDateTime(actual[0].Timestamp)}; earlier history was not measured."
            : $" The first recorded sample is {FormatLocalDateTime(actual[0].Timestamp)}.";
        var gapSummary = measurementGaps.Length == 0
            ? string.Empty
            : $" {measurementGaps.Length.ToString(CultureInfo.InvariantCulture)} measurement {(measurementGaps.Length == 1 ? "gap is" : "gaps are")} shown as not measured.";
        var limitSummary = forecastLimitAt is DateTimeOffset estimatedLimit
            ? $" The estimated limit time at the modeled pace is {FormatLocalDateTime(estimatedLimit)}."
            : string.Empty;
        var comparisonSummary = BuildReferenceComparisonSummary(actual, referenceProjected, referenceForecast?.CapturedAt);
        var accessibleSummary = string.Concat(
            "Coding runway for the ",
            trend.WindowLabel,
            " limit. ",
            summary.Headline,
            ". ",
            summary.UsedPercentText,
            " used. Current pace ",
            summary.CurrentPaceText,
            "; sustainable pace ",
            summary.SustainablePaceText,
            ". ",
            summary.RecommendationText,
            ". ",
            actual.Length.ToString(CultureInfo.InvariantCulture),
            actual.Length == 1 ? " observed point." : " observed points.",
            historySummary,
            gapSummary,
            projectionSummary,
            limitSummary,
            comparisonSummary,
            " Resets ",
            trend.ResetsAtUtc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture),
            ".");

        return new UsageTrendChartModel(
            actual,
            projected,
            sustainable,
            range,
            windowStart,
            projectionEnd,
            now,
            trend.ResetsAtUtc,
            forecastWindowStart,
            forecastWindowEnd,
            forecastLimitAt,
            UsageTrendChartMode.UsageTrend,
            showProjection,
            showRange && range.Length > 0,
            summary,
            accessibleSummary)
        {
            ReferenceProjectedPoints = referenceProjected,
            UnfavorableVarianceSegments = unfavorableVariance,
            MeasurementGaps = measurementGaps,
            ReferenceForecastCapturedAt = referenceProjected.Length > 1 ? referenceForecast?.CapturedAt : null,
            BlockAdvisor = blockAdvisor,
            NextConstraint = nextConstraint
        };
    }

    private static UsageTrendNextConstraint BuildNextConstraint(
        IReadOnlyList<LimitRunwayForecast> forecasts,
        DateTimeOffset now)
    {
        var activeCandidates = forecasts
            .Where(candidate => candidate.WindowDurationMins is 300 or 10_080
                && candidate.ResetsAtUtc > now)
            .ToArray();
        if (activeCandidates.Length == 0)
        {
            return BuildStillLearningConstraint();
        }

        if (activeCandidates.Any(candidate => candidate.IsMock)
            && activeCandidates.Any(candidate => !candidate.IsMock))
        {
            return BuildStillLearningConstraint();
        }
        var exhausted = activeCandidates
            .Where(candidate => candidate.State == LimitRunwayForecastState.Exhausted || candidate.UsedPercent >= 100)
            .OrderBy(candidate => candidate.WindowDurationMins)
            .ThenBy(candidate => candidate.BucketId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (exhausted is not null)
        {
            return BuildConstraint(
                exhausted,
                $"Blocked now · resets {FormatConstraintTime(exhausted.ResetsAtUtc)}");
        }

        var activeByDuration = new Dictionary<int, LimitRunwayForecast>();
        foreach (var duration in new[] { 300, 10_080 })
        {
            var matching = activeCandidates
                .Where(candidate => candidate.WindowDurationMins == duration)
                .OrderByDescending(candidate => candidate.ResetsAtUtc)
                .ThenBy(candidate => candidate.BucketId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matching.Length > 1)
            {
                return BuildStillLearningConstraint();
            }

            if (matching.Length == 1)
            {
                activeByDuration[duration] = matching[0];
            }
        }

        var active = activeByDuration.Values.ToArray();
        if (active.Any(candidate => (!candidate.IsMock && candidate.Confidence == LimitRunwayForecastConfidence.Low)
            || candidate.State is LimitRunwayForecastState.Learning or LimitRunwayForecastState.Stable))
        {
            return BuildStillLearningConstraint();
        }

        var atRisk = active
            .Select(candidate => (Forecast: candidate, Range: ResolveConstraintRange(candidate, now)))
            .Where(candidate => candidate.Forecast.State == LimitRunwayForecastState.AtRisk)
            .ToArray();
        if (atRisk.Any(candidate => candidate.Range is null))
        {
            return BuildStillLearningConstraint();
        }

        if (atRisk.Length == 0)
        {
            var detail = active.Length == 1
                ? $"The active {FormatConstraintLimit(active[0].WindowDurationMins)} is on track until reset."
                : "Both active limits are on track until reset.";
            return new UsageTrendNextConstraint(
                "Next constraint · No blocker forecast",
                detail,
                $"Next constraint: no blocker forecast. {detail} Forecasts use current pace, not task cost or project attribution.");
        }

        if (atRisk.Length == 1)
        {
            var candidate = atRisk[0];
            return BuildConstraint(candidate.Forecast, BuildLikelyFirstDetail(candidate.Forecast, candidate.Range!.Value));
        }

        var first = atRisk[0];
        var second = atRisk[1];
        if (RangesOverlap(first.Range!.Value, second.Range!.Value))
        {
            return new UsageTrendNextConstraint(
                "Next constraint · Too close to call",
                "The 5h and 7d forecast ranges overlap.",
                "Next constraint: too close to call. The active 5h and 7d forecast ranges overlap. Forecasts use current pace, not task cost or project attribution.");
        }

        var winner = first.Range.Value.End < second.Range.Value.Start ? first : second;
        return BuildConstraint(winner.Forecast, BuildLikelyFirstDetail(winner.Forecast, winner.Range!.Value));
    }

    private static UsageTrendNextConstraint BuildStillLearningConstraint() => new(
        "Next constraint · Still learning",
        "Need at least one reliable active 5h or 7d forecast.",
        "Next constraint: still learning. At least one reliable active 5h or 7d forecast is needed. Forecasts use current pace, not task cost or project attribution.");

    private static UsageTrendNextConstraint BuildConstraint(LimitRunwayForecast forecast, string detail) => new(
        $"Next constraint · {FormatConstraintLimit(forecast.WindowDurationMins)}",
        detail,
        $"Next constraint: {FormatConstraintLimit(forecast.WindowDurationMins)}. {detail} Forecasts use current pace, not task cost or project attribution.");

    private static (DateTimeOffset Start, DateTimeOffset End)? ResolveConstraintRange(
        LimitRunwayForecast forecast,
        DateTimeOffset now)
    {
        var first = forecast.EarliestExhaustsAtUtc ?? forecast.ExhaustsAtUtc;
        var second = forecast.LatestExhaustsAtUtc ?? forecast.ExhaustsAtUtc;
        if (first is not DateTimeOffset earliest
            || second is not DateTimeOffset latest
            || earliest <= now
            || latest <= now
            || earliest > forecast.ResetsAtUtc
            || latest > forecast.ResetsAtUtc)
        {
            return null;
        }

        return earliest <= latest ? (earliest, latest) : (latest, earliest);
    }

    private static bool RangesOverlap(
        (DateTimeOffset Start, DateTimeOffset End) first,
        (DateTimeOffset Start, DateTimeOffset End) second) =>
        first.Start <= second.End && second.Start <= first.End;

    private static string BuildLikelyFirstDetail(
        LimitRunwayForecast forecast,
        (DateTimeOffset Start, DateTimeOffset End) range)
    {
        var around = forecast.ExhaustsAtUtc is DateTimeOffset central
            && central >= range.Start
            && central <= range.End
            ? central
            : range.Start.AddTicks((range.End - range.Start).Ticks / 2);
        return $"Likely first around {FormatConstraintTime(around)} · {forecast.Confidence} confidence";
    }

    private static string FormatConstraintLimit(int? durationMinutes) => durationMinutes switch
    {
        300 => "5h limit",
        10_080 => "7d limit",
        _ => "limit"
    };

    private static string FormatConstraintTime(DateTimeOffset value) => FormatLocalDateTime(value);

    private static UsageTrendBlockAdvisor BuildBlockAdvisor(
        int? windowDurationMins,
        LimitRunwayForecast? forecast,
        UsageMomentumSummary momentum,
        DateTimeOffset now,
        DateTimeOffset resetAt,
        int? selectedDurationMinutes)
    {
        var durations = windowDurationMins >= 10_080
            ? new[] { 60, 120, 240, 480 }
            : new[] { 15, 30, 60, 120 };
        var selectedMinutes = durations.Contains(selectedDurationMinutes ?? -1)
            ? selectedDurationMinutes!.Value
            : durations[0];
        var selectedEndsAt = now.AddMinutes(selectedMinutes);
        var durationText = FormatBlockDurationLabel(selectedMinutes);
        var usesEstablishedBaseline = momentum.Confidence == UsageMomentumConfidence.Established
            && momentum.BaselineHourCount >= EstablishedBaselineHours
            && momentum.BaselineDayCount >= EstablishedBaselineDays
            && forecast is
            {
                IsMock: false,
                Confidence: LimitRunwayForecastConfidence.Low,
                State: LimitRunwayForecastState.OnTrack or LimitRunwayForecastState.Stable
            };
        var confidenceText = usesEstablishedBaseline
            ? "Based on an established activity-qualified baseline."
            : forecast is null
            ? "No forecast confidence is available."
            : BuildAdvisorConfidenceText(forecast);

        string state;
        string detail;
        UsageTrendBlockAdvisorStatus status;
        if (forecast?.State == LimitRunwayForecastState.Exhausted || forecast?.UsedPercent >= 100)
        {
            state = "Wait for reset";
            status = UsageTrendBlockAdvisorStatus.WaitForReset;
            detail = $"Capacity is exhausted; it resets {FormatLocalDateTime(resetAt)}. Tests the current pace, not task cost.";
        }
        else if (selectedEndsAt >= resetAt)
        {
            state = "May be interrupted";
            status = UsageTrendBlockAdvisorStatus.MayBeInterrupted;
            detail = $"This block reaches the reset at {FormatLocalDateTime(resetAt)}, so capacity changes during it. Tests the current pace, not task cost.";
        }
        else if (usesEstablishedBaseline && forecast!.State == LimitRunwayForecastState.Stable)
        {
            state = "Likely fits";
            status = UsageTrendBlockAdvisorStatus.LikelyFits;
            detail = $"Recent quota movement is quiet; the established activity-qualified baseline indicates this {durationText} block should fit before reset. Tests the current pace, not task cost.";
        }
        else if (forecast is null
            || forecast.State is LimitRunwayForecastState.Learning or LimitRunwayForecastState.Stable
            || (forecast.Confidence == LimitRunwayForecastConfidence.Low && !forecast.IsMock && !usesEstablishedBaseline))
        {
            state = "Still learning";
            status = UsageTrendBlockAdvisorStatus.StillLearning;
            detail = forecast is { Confidence: LimitRunwayForecastConfidence.Low, IsMock: false }
                ? $"Not enough evidence to plan this {durationText} block yet. Tests the current pace, not task cost."
                : $"Need more samples before estimating whether {durationText} fits. Tests the current pace, not task cost.";
        }
        else if (forecast.State == LimitRunwayForecastState.OnTrack)
        {
            state = "Likely fits";
            status = UsageTrendBlockAdvisorStatus.LikelyFits;
            detail = $"Current pace is on track to stay below the limit until reset {FormatLocalDateTime(resetAt)}. {confidenceText} Tests the current pace, not task cost.";
        }
        else
        {
            var earliest = forecast.EarliestExhaustsAtUtc ?? forecast.ExhaustsAtUtc;
            var latest = forecast.LatestExhaustsAtUtc ?? forecast.ExhaustsAtUtc;
            if (earliest is DateTimeOffset first && latest is DateTimeOffset second)
            {
                var earliestAt = first <= second ? first : second;
                var latestAt = first <= second ? second : first;
                if (selectedEndsAt > latestAt)
                {
                    state = "Unlikely to fit";
                    status = UsageTrendBlockAdvisorStatus.UnlikelyToFit;
                    detail = $"At the current pace, capacity is expected to run short by {FormatLocalDateTime(latestAt)}. {confidenceText} Tests the current pace, not task cost.";
                }
                else if (forecast.Confidence == LimitRunwayForecastConfidence.Low || selectedEndsAt >= earliestAt)
                {
                    state = "May be interrupted";
                    status = UsageTrendBlockAdvisorStatus.MayBeInterrupted;
                    detail = $"Capacity may run short between {FormatDateTimeRange(earliestAt, latestAt)}. {confidenceText} Tests the current pace, not task cost.";
                }
                else
                {
                    state = "Likely fits";
                    status = UsageTrendBlockAdvisorStatus.LikelyFits;
                    detail = $"This block ends before the earliest expected limit time ({FormatLocalDateTime(earliestAt)}). {confidenceText} Tests the current pace, not task cost.";
                }
            }
            else
            {
                state = "Still learning";
                status = UsageTrendBlockAdvisorStatus.StillLearning;
                detail = $"No reliable limit time is available for {durationText}. Tests the current pace, not task cost.";
            }
        }

        var options = durations
            .Select(minutes =>
            {
                var label = FormatBlockDurationLabel(minutes);
                return new UsageTrendBlockOption(
                    minutes,
                    label,
                    minutes == selectedMinutes,
                    $"Plan a {label} coding block",
                    $"Checks whether a {label} block fits the selected limit at the current pace, not task cost.");
            })
            .ToArray();
        var subjectText = FormatBlockDurationSubject(selectedMinutes);
        var verdictText = status switch
        {
            UsageTrendBlockAdvisorStatus.LikelyFits => "likely fits",
            UsageTrendBlockAdvisorStatus.MayBeInterrupted => "may be interrupted",
            UsageTrendBlockAdvisorStatus.UnlikelyToFit => "is unlikely to fit",
            UsageTrendBlockAdvisorStatus.WaitForReset => "should wait for reset",
            _ => "needs more evidence"
        };
        var (constraintLabel, constraintAt) = ResolveBlockTimelineConstraint(forecast, resetAt);
        var conciseConfidence = usesEstablishedBaseline
            ? "Established baseline"
            : forecast?.Confidence switch
        {
            LimitRunwayForecastConfidence.Low => "Low confidence",
            LimitRunwayForecastConfidence.Medium => "Medium confidence",
            LimitRunwayForecastConfidence.High => "High confidence",
            _ => "Confidence unavailable"
        };
        var selectedTimelinePosition = Array.IndexOf(durations, selectedMinutes) + 1;
        var timelineProgressPercent = 100d * selectedTimelinePosition / durations.Length;
        return new UsageTrendBlockAdvisor(
            state,
            detail,
            $"Plan your next block: {durationText}. {state}. {detail}",
            options,
            status)
        {
            SubjectText = subjectText,
            VerdictText = verdictText,
            NowText = FormatLocalDateTime(now),
            EndsAtText = $"{selectedEndsAt.ToLocalTime():h:mm tt} · +{durationText}",
            ConstraintLabel = constraintLabel,
            ConstraintTimeText = FormatLocalDateTime(constraintAt),
            ConfidenceText = conciseConfidence,
            TimelineAccessibleSummary = $"Now {FormatLocalDateTime(now)}. The selected {durationText} block ends {FormatLocalDateTime(selectedEndsAt)}. {constraintLabel} {FormatLocalDateTime(constraintAt)}.",
            // The line represents the selected choice within the four planning
            // options, not elapsed wall-clock time to the quota constraint.
            TimelineProgressPercent = timelineProgressPercent
        };
    }

    private static (string Label, DateTimeOffset At) ResolveBlockTimelineConstraint(
        LimitRunwayForecast? forecast,
        DateTimeOffset resetAt)
    {
        if (forecast?.State != LimitRunwayForecastState.OnTrack)
        {
            var earliest = forecast?.EarliestExhaustsAtUtc ?? forecast?.ExhaustsAtUtc;
            if (earliest is DateTimeOffset value && value < resetAt)
            {
                return ("Earliest expected limit", value);
            }
        }

        return ("Reset", resetAt);
    }

    private static string BuildAdvisorConfidenceText(LimitRunwayForecast forecast) =>
        forecast.Confidence switch
        {
            LimitRunwayForecastConfidence.Low => "Low-confidence forecast, so it is not a safe prediction.",
            LimitRunwayForecastConfidence.Medium => "Medium-confidence forecast.",
            _ => "High-confidence forecast."
        };

    private static string FormatBlockDurationSubject(int minutes) => minutes switch
    {
        15 => "A 15-minute block",
        30 => "A 30-minute block",
        60 => "A 1-hour block",
        120 => "A 2-hour block",
        240 => "A 4-hour block",
        480 => "An 8-hour block",
        _ => $"A {FormatBlockDurationLabel(minutes)} block"
    };

    private static string FormatBlockDurationLabel(int minutes) => minutes switch
    {
        60 => "1h",
        480 => "1 day (8h)",
        _ => FormatDurationCompact(TimeSpan.FromMinutes(minutes))
    };

    private static UsageTrendPoint[] BuildElapsedReferenceProjection(
        UsageTrendForecastReference? reference,
        DateTimeOffset resetAt,
        DateTimeOffset latestActualAt)
    {
        if (reference is null
            || reference.ResetAt != resetAt
            || latestActualAt <= reference.CapturedAt)
        {
            return [];
        }

        var points = reference.ProjectedPoints
            .Where(point => double.IsFinite(point.UsedPercent))
            .Where(point => point.Timestamp >= reference.CapturedAt && point.Timestamp <= resetAt)
            .OrderBy(point => point.Timestamp)
            .ToArray();
        if (points.Length < 2 || latestActualAt <= points[0].Timestamp)
        {
            return [];
        }

        var comparisonEnd = latestActualAt < points[^1].Timestamp ? latestActualAt : points[^1].Timestamp;
        var elapsed = points.Where(point => point.Timestamp <= comparisonEnd).ToList();
        if (elapsed.Count == 0)
        {
            return [];
        }

        if (elapsed[^1].Timestamp < comparisonEnd
            && InterpolatePointAt(points, comparisonEnd) is UsageTrendPoint endpoint)
        {
            elapsed.Add(endpoint);
        }

        return elapsed.Count > 1 ? elapsed.ToArray() : [];
    }

    private static UsageTrendVarianceSegment[] BuildUnfavorableVarianceSegments(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendPoint> reference,
        IReadOnlyList<UsageTrendGap> measurementGaps)
    {
        if (actual.Count < 2 || reference.Count < 2)
        {
            return [];
        }

        var segments = new List<UsageTrendVarianceSegment>();
        var overlapStart = actual[0].Timestamp > reference[0].Timestamp
            ? actual[0].Timestamp
            : reference[0].Timestamp;
        var overlapEnd = actual[^1].Timestamp < reference[^1].Timestamp
            ? actual[^1].Timestamp
            : reference[^1].Timestamp;
        if (overlapEnd <= overlapStart)
        {
            return [];
        }

        var boundaries = actual.Select(point => point.Timestamp)
            .Concat(reference.Select(point => point.Timestamp))
            .Append(overlapStart)
            .Append(overlapEnd)
            .Where(timestamp => timestamp >= overlapStart && timestamp <= overlapEnd)
            .Distinct()
            .OrderBy(timestamp => timestamp)
            .ToArray();
        for (var index = 1; index < boundaries.Length; index++)
        {
            var startAt = boundaries[index - 1];
            var endAt = boundaries[index];
            if (measurementGaps.Any(gap => startAt < gap.EndedAt && endAt > gap.StartedAt))
            {
                continue;
            }

            if (InterpolatePointAt(actual, startAt) is not UsageTrendPoint actualStart
                || InterpolatePointAt(actual, endAt) is not UsageTrendPoint actualEnd
                || InterpolatePointAt(reference, startAt) is not UsageTrendPoint referenceStart
                || InterpolatePointAt(reference, endAt) is not UsageTrendPoint referenceEnd)
            {
                continue;
            }

            var startDelta = actualStart.UsedPercent - referenceStart.UsedPercent - UnfavorableVarianceThresholdPoints;
            var endDelta = actualEnd.UsedPercent - referenceEnd.UsedPercent - UnfavorableVarianceThresholdPoints;
            if (startDelta <= 0 && endDelta <= 0)
            {
                continue;
            }

            if (startDelta > 0 && endDelta > 0)
            {
                segments.Add(new UsageTrendVarianceSegment(actualStart, actualEnd));
                continue;
            }

            var denominator = endDelta - startDelta;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                continue;
            }

            var crossingProgress = Math.Clamp(-startDelta / denominator, 0, 1);
            var crossing = InterpolateBetween(actualStart, actualEnd, crossingProgress);
            segments.Add(startDelta > 0
                ? new UsageTrendVarianceSegment(actualStart, crossing)
                : new UsageTrendVarianceSegment(crossing, actualEnd));
        }

        return segments.ToArray();
    }

    private static string BuildReferenceComparisonSummary(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendPoint> reference,
        DateTimeOffset? capturedAt)
    {
        if (capturedAt is not DateTimeOffset captured
            || reference.Count < 2
            || InterpolatePointAt(actual, reference[^1].Timestamp) is not UsageTrendPoint comparableActual)
        {
            return string.Empty;
        }

        var difference = comparableActual.UsedPercent - reference[^1].UsedPercent;
        var comparison = Math.Abs(difference) <= UnfavorableVarianceThresholdPoints
            ? "in line with"
            : difference > 0
                ? $"{difference:0.#} percentage points above"
                : $"{Math.Abs(difference):0.#} percentage points below";
        return $" Compared with the forecast captured {FormatLocalDateTime(captured)}, actual usage is {comparison} that forecast at the latest comparable sample.";
    }

    private static UsageTrendPoint? InterpolatePointAt(
        IReadOnlyList<UsageTrendPoint> points,
        DateTimeOffset timestamp)
    {
        if (points.Count == 0 || timestamp < points[0].Timestamp || timestamp > points[^1].Timestamp)
        {
            return null;
        }

        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index];
            if (timestamp == current.Timestamp || index == points.Count - 1)
            {
                return current with { Timestamp = timestamp };
            }

            var next = points[index + 1];
            if (timestamp > next.Timestamp)
            {
                continue;
            }

            var totalMilliseconds = (next.Timestamp - current.Timestamp).TotalMilliseconds;
            if (totalMilliseconds <= 0)
            {
                return current with { Timestamp = timestamp };
            }

            var progress = (timestamp - current.Timestamp).TotalMilliseconds / totalMilliseconds;
            return InterpolateBetween(current, next, progress) with { Timestamp = timestamp };
        }

        return null;
    }

    private static UsageTrendPoint InterpolateBetween(
        UsageTrendPoint start,
        UsageTrendPoint end,
        double progress)
    {
        var clamped = Math.Clamp(progress, 0, 1);
        var elapsedTicks = end.Timestamp.Ticks - start.Timestamp.Ticks;
        return new UsageTrendPoint(
            start.Timestamp.AddTicks((long)Math.Round(elapsedTicks * clamped)),
            start.UsedPercent + ((end.UsedPercent - start.UsedPercent) * clamped));
    }

    private static UsageTrendPoint[] BuildStatisticalProjection(
        UsageTrendPoint last,
        DateTimeOffset end,
        LimitRunwayForecast? forecast)
    {
        var points = forecast?.ProjectionPoints?
            .Where(point => point.Timestamp >= last.Timestamp && point.Timestamp <= end)
            .OrderBy(point => point.Timestamp)
            .Select(point => new UsageTrendPoint(point.Timestamp, Math.Clamp(point.ExpectedUsedPercent, 0, 100)))
            .ToList()
            ?? [];

        if (forecast?.ExhaustsAtUtc is DateTimeOffset exhaustsAt
            && exhaustsAt > last.Timestamp
            && exhaustsAt <= end)
        {
            points.RemoveAll(point => point.Timestamp == exhaustsAt);
            points.Add(new UsageTrendPoint(exhaustsAt, 100));
            points.Sort(static (left, right) => left.Timestamp.CompareTo(right.Timestamp));
        }

        if (points.Count == 0 || points[0].Timestamp <= last.Timestamp)
        {
            return points.ToArray();
        }

        return [last, .. points];
    }

    private static UsageTrendPoint[] BuildProjection(UsageTrendPoint last, DateTimeOffset end, double pacePerHour)
    {
        var duration = end - last.Timestamp;
        return Enumerable.Range(0, ProjectionPointCount)
            .Select(index =>
            {
                var timestamp = last.Timestamp + TimeSpan.FromTicks(duration.Ticks * index / (ProjectionPointCount - 1));
                var elapsedHours = Math.Max(0, (timestamp - last.Timestamp).TotalHours);
                return new UsageTrendPoint(timestamp, Math.Clamp(last.UsedPercent + (pacePerHour * elapsedHours), 0, 100));
            })
            .ToArray();
    }

    private static UsageTrendPoint[] TrimAtBudgetLimit(IReadOnlyList<UsageTrendPoint> points)
    {
        if (points.Count < 2)
        {
            return points.ToArray();
        }

        var result = new List<UsageTrendPoint>(points.Count) { points[0] };
        for (var index = 1; index < points.Count; index++)
        {
            var previous = result[^1];
            var current = points[index];
            if (previous.UsedPercent >= 100)
            {
                break;
            }

            if (current.UsedPercent >= 100 && current.UsedPercent > previous.UsedPercent)
            {
                var fraction = Math.Clamp((100 - previous.UsedPercent) / (current.UsedPercent - previous.UsedPercent), 0, 1);
                var elapsedTicks = current.Timestamp.Ticks - previous.Timestamp.Ticks;
                result.Add(new UsageTrendPoint(
                    previous.Timestamp.AddTicks((long)Math.Round(elapsedTicks * fraction)),
                    100));
                break;
            }

            result.Add(current);
        }

        return result.ToArray();
    }

    private static DateTimeOffset? ResolveProjectedLimitAt(IReadOnlyList<UsageTrendPoint> points)
    {
        var limitPoint = points.FirstOrDefault(point => point.UsedPercent >= 100);
        return limitPoint?.Timestamp;
    }

    private static UsageTrendPoint[] BuildSustainableProjection(
        UsageTrendPoint last,
        DateTimeOffset resetAt,
        double? sustainablePacePerHour)
    {
        if (sustainablePacePerHour is not double sustainablePace
            || sustainablePace <= 0
            || resetAt <= last.Timestamp
            || last.UsedPercent >= 100)
        {
            return [];
        }

        return [last, new UsageTrendPoint(resetAt, 100)];
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End) ResolveForecastWindow(
        LimitRunwayForecast? forecast,
        DateTimeOffset observedAt,
        DateTimeOffset resetAt)
    {
        if (forecast is null
            || forecast.State != LimitRunwayForecastState.AtRisk
            || (forecast.Confidence == LimitRunwayForecastConfidence.Low && !forecast.IsMock)
            || forecast.EarliestExhaustsAtUtc is not DateTimeOffset first
            || forecast.LatestExhaustsAtUtc is not DateTimeOffset second)
        {
            return (null, null);
        }

        var start = first <= second ? first : second;
        var end = first <= second ? second : first;
        return start > observedAt && end > start && end <= resetAt
            ? (start, end)
            : (null, null);
    }

    private static UsageTrendRunwaySummary BuildRunwaySummary(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        IReadOnlyList<LimitHourlyUsageRate> baselineHourlyRates,
        HourlyActivityEvidence currentHourActivityEvidence,
        LocalActivityCoverage activityCoverage,
        IReadOnlyList<DateTimeOffset> localActivityUtcHours,
        UsageTrendPoint last,
        int? windowDurationMins,
        LimitRunwayForecast? forecast,
        DateTimeOffset resetAt,
        DateTimeOffset now,
        double? pacePerHour,
        double? sustainablePacePerHour,
        DateTimeOffset? forecastWindowStart,
        DateTimeOffset? forecastWindowEnd,
        DateTimeOffset? pointExhaustion)
    {
        string headline;
        string forecastLead;
        string forecastWhen;

        var isExhausted = last.UsedPercent >= 100 || forecast?.State == LimitRunwayForecastState.Exhausted;
        if (isExhausted)
        {
            headline = "Limit reached";
            forecastLead = "Reset in";
            forecastWhen = FormatDurationCompact(resetAt - now);
        }
        else if (pointExhaustion is DateTimeOffset exhaustsAt && exhaustsAt > now && exhaustsAt < resetAt)
        {
            headline = $"About {FormatDurationCompact(exhaustsAt - now)} left at this pace";
            forecastLead = $"Limit in about {FormatDurationCompact(exhaustsAt - now)}";
            forecastWhen = $"• reset in {FormatDurationCompact(resetAt - now)}";
        }
        else if (forecastWindowStart is DateTimeOffset earliest && forecastWindowEnd is DateTimeOffset latest)
        {
            headline = $"About {FormatDurationRange(earliest - now, latest - now)} left at this pace";
            forecastLead = $"Limit in about {FormatDurationRange(earliest - now, latest - now)}";
            forecastWhen = $"• reset in {FormatDurationCompact(resetAt - now)}";
        }
        else if (pacePerHour is null or <= 0)
        {
            headline = forecast?.State == LimitRunwayForecastState.Stable
                ? "Usage is stable right now"
                : "Runway is still learning";
            forecastLead = "Keep coding to build a reliable forecast";
            forecastWhen = string.Empty;
        }
        else
        {
            headline = "On pace to last until reset";
            forecastLead = "Expected to last until reset";
            forecastWhen = $"• reset in {FormatDurationCompact(resetAt - now)}";
        }

        var currentPaceText = FormatPace(pacePerHour);
        var sustainablePaceText = FormatPace(sustainablePacePerHour);
        var paceRatio = pacePerHour is double currentPace
            && currentPace > 0
            && sustainablePacePerHour is double sustainablePace
            && sustainablePace > 0
                ? currentPace / sustainablePace
                : double.NaN;
        var hasPaceRatio = double.IsFinite(paceRatio);
        var currentPaceBand = ResolvePaceBand(paceRatio);
        var momentum = ClarifyMomentumForSustainablePace(
            BuildUsageMomentum(
                actual,
                windowDurationMins,
                forecast?.ObservationDuration,
                forecast?.SampleCount,
                measurementGaps,
                baselineHourlyRates,
                currentHourActivityEvidence,
                activityCoverage,
                localActivityUtcHours),
            currentPaceBand);
        var comparisonText = hasPaceRatio ? $"{paceRatio:0.#}×" : "—";
        var comparisonLabel = hasPaceRatio
            ? paceRatio > 1.05 ? "above sustainable pace" : "of sustainable pace"
            : "comparison unavailable";

        string recommendation;
        if (isExhausted)
        {
            recommendation = "Wait until the limit resets before starting another coding run";
        }
        else if (hasPaceRatio
            && paceRatio > 1.05
            && forecast is { Confidence: LimitRunwayForecastConfidence.Low, IsMock: false })
        {
            recommendation = "Early signal: at this pace, the limit may arrive before reset";
        }
        else if (hasPaceRatio && paceRatio > 1.05)
        {
            var reduction = Math.Clamp(Math.Round((1 - (1 / paceRatio)) * 20) * 5, 5, 95);
            recommendation = $"At this pace, the limit may arrive before reset. Reduce pace by about {reduction:0}% to last until reset";
        }
        else if (hasPaceRatio)
        {
            recommendation = "Current pace should last until reset";
        }
        else
        {
            recommendation = "Keep coding to build a reliable pace estimate";
        }

        return new UsageTrendRunwaySummary(
            headline,
            forecastLead,
            forecastWhen,
            BuildConfidenceText(forecast, momentum),
            $"{Math.Clamp(last.UsedPercent, 0, 100):0}%",
            momentum,
            currentPaceText,
            currentPaceBand,
            sustainablePaceText,
            comparisonText,
            comparisonLabel,
            recommendation,
            CanOpenPacingPlan: !isExhausted);
    }

    internal static UsagePaceBand ResolvePaceBand(double paceRatio)
    {
        if (!double.IsFinite(paceRatio) || paceRatio <= 0)
        {
            return UsagePaceBand.Unknown;
        }

        if (paceRatio < 0.95)
        {
            return UsagePaceBand.BelowSustainable;
        }

        if (paceRatio <= 1.05)
        {
            return UsagePaceBand.AtSustainable;
        }

        return paceRatio <= 1.5
            ? UsagePaceBand.AboveSustainable
            : UsagePaceBand.FarAboveSustainable;
    }

    internal static UsageMomentumSummary ClarifyMomentumForSustainablePace(
        UsageMomentumSummary momentum,
        UsagePaceBand paceBand)
    {
        if (momentum.IsLearning
            || !string.Equals(momentum.ValueText, "About the same", StringComparison.Ordinal))
        {
            return momentum;
        }

        var stateText = paceBand switch
        {
            UsagePaceBand.BelowSustainable => "and below sustainable pace",
            UsagePaceBand.AtSustainable => "and near sustainable pace",
            UsagePaceBand.AboveSustainable or UsagePaceBand.FarAboveSustainable => "but above sustainable pace",
            _ => "vs your recent active-hour pace"
        };

        return momentum with
        {
            ValueText = "Typical for you",
            StateText = stateText,
            AccessibleSummary = string.IsNullOrWhiteSpace(momentum.AccessibleSummary)
                ? $"Usage momentum is typical for you, {stateText}."
                : $"{momentum.AccessibleSummary} Current usage is typical for you, {stateText}."
        };
    }

    internal static UsageMomentumSummary BuildUsageMomentum(
        IReadOnlyList<UsageTrendPoint> actual,
        int? windowDurationMins,
        TimeSpan? observationDuration = null,
        int? sampleCount = null,
        IReadOnlyList<UsageTrendGap>? measurementGaps = null,
        IReadOnlyList<LimitHourlyUsageRate>? baselineHourlyRates = null,
        HourlyActivityEvidence currentHourActivityEvidence = HourlyActivityEvidence.QuotaMovement,
        LocalActivityCoverage activityCoverage = LocalActivityCoverage.Unavailable,
        IReadOnlyList<DateTimeOffset>? localActivityUtcHours = null)
    {
        var gaps = measurementGaps ?? Array.Empty<UsageTrendGap>();
        var isDailyBaseline = windowDurationMins >= 24 * 60;
        var evidenceDuration = isDailyBaseline
            ? ResolveMomentumEvidenceDuration(actual, observationDuration: null)
            : ResolveMomentumEvidenceDuration(actual, observationDuration);
        var evidenceSamples = isDailyBaseline && measurementGaps is not null
            ? actual.Count
            : sampleCount is > 0 ? sampleCount.Value : actual.Count;
        var baselineTarget = ResolveMomentumBaselineTarget(windowDurationMins);
        if (actual.Count < 2 || (!isDailyBaseline && evidenceDuration < baselineTarget))
        {
            return WithActivityDetails(
                LearningMomentum(windowDurationMins, evidenceDuration, evidenceSamples),
                activityCoverage);
        }

        return isDailyBaseline
            ? BuildDailyMedianMomentum(
                actual,
                gaps,
                evidenceSamples,
                baselineHourlyRates,
                currentHourActivityEvidence,
                activityCoverage)
            : BuildWindowMedianMomentum(
                actual,
                gaps,
                windowDurationMins,
                evidenceDuration,
                evidenceSamples,
                currentHourActivityEvidence,
                activityCoverage,
                localActivityUtcHours ?? []);
    }

    private static UsageMomentumSummary BuildWindowMedianMomentum(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        int? windowDurationMins,
        TimeSpan evidenceDuration,
        int evidenceSamples,
        HourlyActivityEvidence currentHourActivityEvidence,
        LocalActivityCoverage activityCoverage,
        IReadOnlyList<DateTimeOffset> localActivityUtcHours)
    {
        var latest = actual[^1].Timestamp;
        var currentRate = UsageRateBetween(actual, measurementGaps, latest.AddHours(-1), latest);
        var hours = Math.Max(2, (int)Math.Round((windowDurationMins ?? 300) / 60d));
        var historicalRates = Enumerable.Range(1, Math.Max(1, hours - 1))
            .Select(offset =>
            {
                var startedAt = latest.AddHours(-(offset + 1));
                var endedAt = latest.AddHours(-offset);
                return new
                {
                    Rate = UsageRateBetween(actual, measurementGaps, startedAt, endedAt),
                    HasLocalEvent = localActivityUtcHours.Any(hour => hour < endedAt && hour.AddHours(1) > startedAt)
                };
            })
            .Where(item => item.Rate is double value
                && double.IsFinite(value)
                && (value >= 0.1 || item.HasLocalEvent))
            .Select(item => item.Rate!.Value)
            .ToArray();

        if (historicalRates.Length < hours - 1)
        {
            return WithActivityDetails(LearningMomentum(windowDurationMins, evidenceDuration, evidenceSamples), activityCoverage);
        }

        if (currentRate is not double current)
        {
            return WithActivityDetails(LearningMomentum(windowDurationMins, evidenceDuration, evidenceSamples), activityCoverage);
        }

        if (currentHourActivityEvidence == HourlyActivityEvidence.None)
        {
            return WithActivityDetails(
                new UsageMomentumSummary("Baseline ready", "Waiting for observed Codex activity", "Activity-qualified recent hours", 0)
                {
                    IsLearning = true,
                    BaselineProgress = 1,
                    BaselineHourCount = historicalRates.Length,
                    AccessibleSummary = "Activity-qualified baseline ready. Waiting for observed Codex activity before showing a direction."
                },
                activityCoverage);
        }

        return WithActivityDetails(CreateMomentum(current, Median(historicalRates), "vs 5h window median"), activityCoverage);
    }

    private static UsageMomentumSummary BuildDailyMedianMomentum(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        int evidenceSamples,
        IReadOnlyList<LimitHourlyUsageRate>? retainedBaselineRates,
        HourlyActivityEvidence currentHourActivityEvidence,
        LocalActivityCoverage activityCoverage)
    {
        var latest = actual[^1].Timestamp;
        var currentMeasurement = ResolveCurrentUsageRate(actual, measurementGaps, latest);
        var baselineCutoff = latest.AddHours(-1);
        var retained = retainedBaselineRates?
            .Where(rate => double.IsFinite(rate.PercentPerHour) && rate.PercentPerHour is >= 0 and <= 100)
            .Where(rate => rate.ActivityEvidence != HourlyActivityEvidence.None)
            .Where(rate => rate.HourStartedAtUtc.AddHours(1) <= baselineCutoff)
            .Select(rate => new WeightedHourlyRate(rate.HourStartedAtUtc, rate.PercentPerHour))
            .ToArray() ?? [];
        // A supplied collection is authoritative, even when every measured hour is inactive.
        // The fallback exists only for legacy callers that do not yet carry classified facts.
        var rates = retainedBaselineRates is not null
            ? retained
            : DerivePriorHourlyRates(actual, measurementGaps, latest);
        var dayCount = rates
            .Select(rate => DateOnly.FromDateTime(rate.ObservedAtUtc.ToLocalTime().DateTime))
            .Distinct()
            .Count();
        var evidenceSpan = rates.Length < 2
            ? (rates.Length == 1 ? TimeSpan.FromHours(1) : TimeSpan.Zero)
            : rates.Max(rate => rate.ObservedAtUtc) - rates.Min(rate => rate.ObservedAtUtc) + TimeSpan.FromHours(1);
        var confidence = ResolveMomentumConfidence(rates.Length, dayCount, evidenceSpan);

        if (rates.Length == 0)
        {
            return WithActivityDetails(
                LearningDurableBaseline(rates.Length, dayCount, evidenceSpan, evidenceSamples),
                activityCoverage);
        }

        if (currentHourActivityEvidence == HourlyActivityEvidence.None)
        {
            return WithActivityDetails(
                WaitingForObservedActivity(rates.Length, dayCount, confidence),
                activityCoverage);
        }

        if (currentMeasurement is not CurrentUsageRate currentMeasurementValue)
        {
            return WithActivityDetails(
                WaitingForCurrentMomentum(evidenceSamples, rates.Length, dayCount, confidence),
                activityCoverage);
        }

        var weighted = BuildRecencyWeights(rates, latest);
        var median = WeightedQuantile(weighted, 0.5);
        var lower = WeightedQuantile(weighted, 0.25);
        var upper = WeightedQuantile(weighted, 0.75);
        var confidenceLabel = FormatMomentumConfidence(confidence);
        var measurementPrefix = currentMeasurementValue.IsFullHour
            ? string.Empty
            : $"{FormatBaselineDuration(currentMeasurementValue.MeasuredDuration)} live · ";
        var baselineText = $"{measurementPrefix}recent median {median:0.#}%/h · usual {lower:0.#}–{upper:0.#}%/h";
        return WithActivityDetails(CreateDurableMomentum(currentMeasurementValue.PercentPerHour, median, lower, upper, baselineText) with
        {
            Confidence = confidence,
            BaselineHourCount = rates.Length,
            BaselineDayCount = dayCount,
            AccessibleSummary = $"Usage momentum compared with a {confidenceLabel.ToLowerInvariant()} baseline of {rates.Length} active hours across {FormatDayCount(dayCount)}. Current pace uses {FormatBaselineDuration(currentMeasurementValue.MeasuredDuration)} of measured activity. Recent median {median:0.#} percent per hour; usual range {lower:0.#} to {upper:0.#} percent per hour."
        }, activityCoverage);
    }

    private static UsageMomentumSummary WaitingForObservedActivity(
        int comparablePriorHours,
        int dayCount,
        UsageMomentumConfidence confidence) => new(
            "Baseline ready",
            "Waiting for observed Codex activity",
            $"{FormatMomentumConfidence(confidence)} · {FormatComparablePriorHours(comparablePriorHours)} across {FormatDayCount(dayCount)}",
            0)
        {
            IsLearning = true,
            BaselineProgress = 1,
            Confidence = confidence,
            BaselineHourCount = comparablePriorHours,
            BaselineDayCount = dayCount,
            AccessibleSummary = $"{FormatMomentumConfidence(confidence)} activity-qualified baseline ready. Waiting for observed Codex activity before showing a direction."
        };

    private static UsageMomentumSummary WithActivityDetails(
        UsageMomentumSummary summary,
        LocalActivityCoverage activityCoverage)
    {
        var coverageText = activityCoverage switch
        {
            LocalActivityCoverage.Available => "Available",
            LocalActivityCoverage.Partial => "Partial",
            _ => "Unavailable"
        };
        return summary with
        {
            ActivityCoverage = activityCoverage,
            ActivityCoverageText = coverageText,
            ScopeText = "Account and plan scope are not verified."
        };
    }

    private static UsageMomentumSummary WaitingForCurrentMomentum(
        int evidenceSamples,
        int comparablePriorHours,
        int dayCount,
        UsageMomentumConfidence confidence)
    {
        var samplesText = evidenceSamples == 1
            ? "1 sample"
            : $"{Math.Max(0, evidenceSamples)} samples";
        var comparableText = FormatComparablePriorHours(comparablePriorHours);
        return new UsageMomentumSummary(
            "Measuring activity",
            $"First look after {MinimumProgressiveCurrentMinutes} measured minutes",
            $"{FormatMomentumConfidence(confidence)} · {comparableText} across {FormatDayCount(dayCount)}",
            0)
        {
            IsLearning = true,
            BaselineProgress = 1,
            Confidence = confidence,
            BaselineHourCount = comparablePriorHours,
            BaselineDayCount = dayCount,
            AccessibleSummary = $"{FormatMomentumConfidence(confidence)} baseline ready. At least {MinimumProgressiveCurrentMinutes} measured activity minutes are needed before momentum can be calculated. {comparableText} are available across {FormatDayCount(dayCount)} from {samplesText}."
        };
    }

    private static UsageMomentumSummary LearningDurableBaseline(
        int comparablePriorHours,
        int dayCount,
        TimeSpan evidenceSpan,
        int evidenceSamples)
    {
        var samplesText = evidenceSamples == 1
            ? "1 sample"
            : $"{Math.Max(0, evidenceSamples)} samples";
        var comparableText = FormatComparablePriorHours(comparablePriorHours);
        var progress = Math.Min(
            Math.Clamp(comparablePriorHours / (double)EarlyBaselineHours, 0, 1),
            Math.Min(
                Math.Clamp(dayCount / (double)EarlyBaselineDays, 0, 1),
                Math.Clamp(evidenceSpan.TotalHours / EarlyMinimumSpanHours, 0, 1)));
        return new UsageMomentumSummary(
            "Waiting",
            "Need the first completed active hour",
            $"{comparableText} across {FormatDayCount(dayCount)} · {samplesText}",
            0)
        {
            IsLearning = true,
            BaselineProgress = progress,
            BaselineHourCount = comparablePriorHours,
            BaselineDayCount = dayCount,
            AccessibleSummary = $"Learning baseline. One completed activity-qualified hour is needed for the first look. {comparableText} are currently available across {FormatDayCount(dayCount)} from {samplesText}."
        };
    }

    private static string FormatComparablePriorHours(int count) =>
        count == 1 ? "1 comparable prior hour" : $"{Math.Max(0, count)} comparable prior hours";

    private static WeightedHourlyRate[] DerivePriorHourlyRates(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        DateTimeOffset latest)
    {
        var availableHours = Math.Max(0, Math.Min(28 * 24, (int)Math.Floor((latest - actual[0].Timestamp).TotalHours) - 1));
        return Enumerable.Range(1, availableHours)
            .Select(offset => new
            {
                StartedAt = latest.AddHours(-(offset + 1)),
                Rate = UsageRateBetween(actual, measurementGaps, latest.AddHours(-(offset + 1)), latest.AddHours(-offset))
            })
            .Where(item => item.Rate is double value && double.IsFinite(value))
            .Select(item => new WeightedHourlyRate(item.StartedAt, item.Rate!.Value))
            .ToArray();
    }

    private static UsageMomentumConfidence ResolveMomentumConfidence(
        int hourCount,
        int dayCount,
        TimeSpan evidenceSpan) =>
        hourCount >= EstablishedBaselineHours && dayCount >= EstablishedBaselineDays && evidenceSpan >= TimeSpan.FromHours(EstablishedMinimumSpanHours)
            ? UsageMomentumConfidence.Established
            : hourCount >= EarlyBaselineHours && dayCount >= EarlyBaselineDays && evidenceSpan >= TimeSpan.FromHours(EarlyMinimumSpanHours)
                ? UsageMomentumConfidence.EarlyEstimate
                : hourCount >= 1
                    ? UsageMomentumConfidence.FirstLook
                    : UsageMomentumConfidence.Learning;

    private static string FormatDayCount(int count) => count == 1 ? "1 day" : $"{Math.Max(0, count)} days";

    private static string FormatMomentumConfidence(UsageMomentumConfidence confidence) => confidence switch
    {
        UsageMomentumConfidence.FirstLook => "First look",
        UsageMomentumConfidence.EarlyEstimate => "Early estimate",
        UsageMomentumConfidence.Established => "Established",
        _ => "Learning"
    };

    private static WeightedValue[] BuildRecencyWeights(IReadOnlyList<WeightedHourlyRate> rates, DateTimeOffset now) =>
        rates.Select(rate => new WeightedValue(
            rate.PercentPerHour,
            Math.Pow(0.5, Math.Max(0, (now - rate.ObservedAtUtc).TotalDays) / BaselineHalfLifeDays)))
            .ToArray();

    private static double WeightedQuantile(IReadOnlyList<WeightedValue> values, double quantile)
    {
        var ordered = values.OrderBy(item => item.Value).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var target = ordered.Sum(item => item.Weight) * Math.Clamp(quantile, 0, 1);
        var cumulative = 0d;
        foreach (var item in ordered)
        {
            cumulative += item.Weight;
            if (cumulative >= target)
            {
                return item.Value;
            }
        }

        return ordered[^1].Value;
    }

    private readonly record struct WeightedHourlyRate(DateTimeOffset ObservedAtUtc, double PercentPerHour);

    private readonly record struct WeightedValue(double Value, double Weight);

    private readonly record struct CurrentUsageRate(
        double PercentPerHour,
        TimeSpan MeasuredDuration,
        bool IsFullHour);

    private static UsageMomentumSummary CreateMomentum(double currentRate, double medianRate, string baselineText)
    {
        var difference = currentRate - medianRate;
        var steadyThreshold = Math.Max(0.05, Math.Abs(medianRate) * 0.1);
        var scale = Math.Max(0.25, Math.Abs(medianRate));
        var gaugeValue = Math.Clamp(difference / scale, -1, 1);

        string valueText;
        string stateText;
        if (Math.Abs(difference) <= steadyThreshold)
        {
            valueText = "About the same";
            stateText = "as recent baseline";
            gaugeValue = 0;
        }
        else if (difference > 0)
        {
            valueText = $"{difference:0.#}%/h faster";
            stateText = "than recent baseline";
        }
        else
        {
            valueText = $"{Math.Abs(difference):0.#}%/h slower";
            stateText = "than recent baseline";
        }

        return new UsageMomentumSummary(valueText, stateText, baselineText, gaugeValue)
        {
            IsLearning = false,
            BaselineProgress = 1,
            AccessibleSummary = $"Usage momentum: {valueText}; {stateText}; {baselineText}."
        };
    }

    private static UsageMomentumSummary LearningMomentum(
        int? windowDurationMins,
        TimeSpan evidenceDuration,
        int evidenceSamples)
    {
        var target = ResolveMomentumBaselineTarget(windowDurationMins);
        var collected = evidenceDuration < TimeSpan.Zero
            ? TimeSpan.Zero
            : evidenceDuration > target
                ? target
                : evidenceDuration;
        var collectedText = FormatBaselineDuration(collected);
        var remaining = target - collected;
        var requirementText = remaining > TimeSpan.Zero
            ? $"{FormatBaselineDuration(remaining)} more data needed"
            : "More comparable data needed";
        var samplesText = evidenceSamples == 1
            ? "1 sample"
            : $"{Math.Max(0, evidenceSamples)} samples";
        var evidenceText = $"{collectedText} usable now · {samplesText}";
        var baselineProgress = target > TimeSpan.Zero
            ? Math.Clamp(collected.TotalMinutes / target.TotalMinutes, 0, 1)
            : 0;
        var readinessText = $"{baselineProgress * 100:0}% ready";

        return new UsageMomentumSummary(
            readinessText,
            requirementText,
            evidenceText,
            0)
        {
            IsLearning = true,
            BaselineProgress = baselineProgress,
            AccessibleSummary = $"Baseline progress: {readinessText}. {requirementText}. {collectedText} currently usable from {samplesText}."
        };
    }

    private static UsageMomentumSummary CreateDurableMomentum(
        double currentRate,
        double medianRate,
        double lowerRate,
        double upperRate,
        string baselineText)
    {
        var difference = currentRate - medianRate;
        var spread = Math.Max(medianRate - lowerRate, upperRate - medianRate);
        var steadyThreshold = Math.Max(0.05, Math.Max(Math.Abs(medianRate) * 0.1, spread * 0.5));
        var scale = Math.Max(0.25, Math.Max(Math.Abs(medianRate), spread));
        var gaugeValue = Math.Clamp(difference / scale, -1, 1);

        string valueText;
        string stateText;
        if (Math.Abs(difference) <= steadyThreshold)
        {
            valueText = "About the same";
            stateText = "as recent active-hour baseline";
            gaugeValue = 0;
        }
        else if (difference > 0)
        {
            valueText = $"{difference:0.#}%/h faster";
            stateText = "than recent active-hour baseline";
        }
        else
        {
            valueText = $"{Math.Abs(difference):0.#}%/h slower";
            stateText = "than recent active-hour baseline";
        }

        return new UsageMomentumSummary(valueText, stateText, baselineText, gaugeValue)
        {
            IsLearning = false,
            BaselineProgress = 1
        };
    }

    private static TimeSpan ResolveMomentumBaselineTarget(int? windowDurationMins) =>
        windowDurationMins >= 24 * 60
            ? TimeSpan.FromHours(24)
            : TimeSpan.FromMinutes(Math.Max(60, windowDurationMins ?? 300));

    private static TimeSpan ResolveMomentumEvidenceDuration(
        IReadOnlyList<UsageTrendPoint> actual,
        TimeSpan? observationDuration)
    {
        if (observationDuration is TimeSpan explicitDuration && explicitDuration > TimeSpan.Zero)
        {
            return explicitDuration;
        }

        return actual.Count < 2
            ? TimeSpan.Zero
            : actual[^1].Timestamp - actual[0].Timestamp;
    }

    private static string FormatBaselineDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0h";
        }

        return duration.TotalMinutes < 60
            ? $"{Math.Max(1, Math.Round(duration.TotalMinutes)):0}m"
            : $"{duration.TotalHours:0.#}h";
    }

    private static double? UsageRateBetween(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var hours = (end - start).TotalHours;
        if (hours <= 0
            || OverlapsMeasurementGap(measurementGaps, start, end)
            || InterpolatePointAt(actual, start) is not UsageTrendPoint startPoint
            || InterpolatePointAt(actual, end) is not UsageTrendPoint endPoint)
        {
            return null;
        }

        return Math.Max(0, endPoint.UsedPercent - startPoint.UsedPercent) / hours;
    }

    private static CurrentUsageRate? ResolveCurrentUsageRate(
        IReadOnlyList<UsageTrendPoint> actual,
        IReadOnlyList<UsageTrendGap> measurementGaps,
        DateTimeOffset latest)
    {
        if (UsageRateBetween(actual, measurementGaps, latest.AddHours(-1), latest) is double fullHourRate)
        {
            return new CurrentUsageRate(fullHourRate, TimeSpan.FromHours(1), IsFullHour: true);
        }

        var earliestAllowed = latest.AddHours(-1);
        var minimumDuration = TimeSpan.FromMinutes(MinimumProgressiveCurrentMinutes);
        var startPoint = actual
            .Where(point => point.Timestamp >= earliestAllowed && point.Timestamp < latest)
            .Where(point => latest - point.Timestamp >= minimumDuration)
            .FirstOrDefault(point => !OverlapsMeasurementGap(measurementGaps, point.Timestamp, latest));
        if (startPoint is null)
        {
            return null;
        }

        var duration = latest - startPoint.Timestamp;
        var delta = actual[^1].UsedPercent - startPoint.UsedPercent;
        if (duration <= TimeSpan.Zero || delta < 0 || !double.IsFinite(delta))
        {
            return null;
        }

        return new CurrentUsageRate(delta / duration.TotalHours, duration, IsFullHour: false);
    }

    private static bool OverlapsMeasurementGap(
        IReadOnlyList<UsageTrendGap> measurementGaps,
        DateTimeOffset start,
        DateTimeOffset end) =>
        measurementGaps.Any(gap => gap.StartedAt < end && gap.EndedAt > start);

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static DateTimeOffset? ResolvePointExhaustion(
        UsageTrendPoint last,
        LimitRunwayForecast? forecast,
        DateTimeOffset resetAt,
        double? pacePerHour)
    {
        if (forecast?.ExhaustsAtUtc is DateTimeOffset forecastPoint)
        {
            return forecastPoint;
        }

        if (pacePerHour is not double pace || pace <= 0 || last.UsedPercent >= 100)
        {
            return null;
        }

        var hours = (100 - last.UsedPercent) / pace;
        if (!double.IsFinite(hours) || hours <= 0)
        {
            return null;
        }

        var result = last.Timestamp.AddHours(hours);
        return result <= resetAt ? result : null;
    }

    private static string BuildConfidenceText(
        LimitRunwayForecast? forecast,
        UsageMomentumSummary momentum)
    {
        if (forecast is null)
        {
            return "Collecting live samples";
        }

        var evidence = forecast.SampleCount == 1
            ? "1 sample"
            : $"{Math.Max(0, forecast.SampleCount)} samples";
        if (forecast.ObservationDuration is TimeSpan duration && duration > TimeSpan.Zero)
        {
            if (forecast.WindowDurationMins is >= 10_080)
            {
                var days = momentum.BaselineDayCount == 1 ? "1 day" : $"{momentum.BaselineDayCount} days";
                return momentum.Confidence switch
                {
                    UsageMomentumConfidence.Established => $"Established • {momentum.BaselineHourCount} active hours • {days}",
                    UsageMomentumConfidence.EarlyEstimate => $"Early estimate • {momentum.BaselineHourCount} active hours • {days}",
                    UsageMomentumConfidence.FirstLook => $"First look • {momentum.BaselineHourCount} active hours • {days}",
                    _ => "Waiting for the first activity-qualified hour"
                };
            }

            evidence = $"{evidence} over {FormatEvidenceDuration(duration)}";
        }

        return $"{forecast.Confidence} evidence • {evidence}";
    }

    private static string FormatPace(double? pacePerHour)
    {
        if (pacePerHour is not double pace || !double.IsFinite(pace) || pace < 0)
        {
            return "—";
        }

        return pace is > 0 and < 0.05 ? "<0.1%/h" : $"{pace:0.#}%/h";
    }

    private static string FormatDurationRange(TimeSpan first, TimeSpan second)
    {
        var earliest = first <= second ? first : second;
        var latest = first <= second ? second : first;
        var firstText = FormatDurationCompact(earliest);
        var secondText = FormatDurationCompact(latest);
        return string.Equals(firstText, secondText, StringComparison.Ordinal)
            ? firstText
            : $"{firstText}–{secondText}";
    }

    private static string FormatDurationCompact(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "now";
        }

        if (duration.TotalMinutes < 90)
        {
            return $"{Math.Max(1, Math.Round(duration.TotalMinutes)):0}m";
        }

        if (duration.TotalHours < 48)
        {
            return $"{Math.Max(1, Math.Round(duration.TotalHours)):0}h";
        }

        var days = (int)Math.Floor(duration.TotalDays);
        var hours = duration.Hours;
        return hours == 0 ? $"{days}d" : $"{days}d {hours}h";
    }

    private static string FormatEvidenceDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 60)
        {
            return $"{Math.Max(1, Math.Round(duration.TotalMinutes)):0}m";
        }

        return duration.TotalHours < 24
            ? $"{duration.TotalHours:0.#}h"
            : $"{duration.TotalDays:0.#}d";
    }

    private static string FormatDateTimeRange(DateTimeOffset first, DateTimeOffset second) =>
        $"{FormatLocalDateTime(first)}–{FormatLocalDateTime(second)}";

    private static string FormatLocalDateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("ddd, MMM d, h:mm tt", CultureInfo.CurrentCulture);

    private static UsageTrendBandPoint[] BuildForecastRange(
        IReadOnlyList<UsageTrendPoint> projected,
        UsageTrendPoint last,
        LimitRunwayForecast? forecast,
        double? pacePerHour)
    {
        var statisticalRange = forecast?.ProjectionPoints?
            .Where(point => point.Timestamp >= projected[0].Timestamp && point.Timestamp <= projected[^1].Timestamp)
            .OrderBy(point => point.Timestamp)
            .Select(point =>
            {
                var lower = Math.Clamp(point.LowerUsedPercent, 0, 100);
                var upper = Math.Clamp(point.UpperUsedPercent, lower, 100);
                return new UsageTrendBandPoint(point.Timestamp, lower, upper);
            })
            .ToArray()
            ?? [];
        if (statisticalRange.Length > 1)
        {
            return forecast is not { IsMock: true }
                   && forecast?.Confidence == LimitRunwayForecastConfidence.Low
                ? []
                : statisticalRange;
        }

        // Live statistical intervals come from the model's posterior projection
        // points. The proportional fallback is retained only for deterministic
        // visual-harness/mock scenarios.
        if (forecast is not { IsMock: true })
        {
            return [];
        }

        if (pacePerHour is not double pace || pace <= 0)
        {
            return [];
        }

        var earliest = forecast?.EarliestExhaustsAtUtc;
        var latest = forecast?.LatestExhaustsAtUtc;
        var hasBoundedExhaustion = earliest is DateTimeOffset earliestValue
            && latest is DateTimeOffset latestValue
            && latestValue > last.Timestamp
            && earliestValue > last.Timestamp;
        if (!hasBoundedExhaustion && forecast is not { IsMock: true } && forecast?.Confidence == LimitRunwayForecastConfidence.Low)
        {
            return [];
        }

        var remaining = Math.Max(0, 100 - last.UsedPercent);
        var slowPace = pace;
        var fastPace = pace;
        if (hasBoundedExhaustion)
        {
            slowPace = remaining / Math.Max(0.01, (latest!.Value - last.Timestamp).TotalHours);
            fastPace = remaining / Math.Max(0.01, (earliest!.Value - last.Timestamp).TotalHours);
        }
        else
        {
            var margin = forecast?.Confidence switch
            {
                LimitRunwayForecastConfidence.High => 0.2,
                LimitRunwayForecastConfidence.Medium => 0.35,
                _ => 0.45
            };
            slowPace = pace * (1 - margin);
            fastPace = pace * (1 + margin);
        }

        return projected
            .Select(point =>
            {
                var elapsedHours = Math.Max(0, (point.Timestamp - last.Timestamp).TotalHours);
                return new UsageTrendBandPoint(
                    point.Timestamp,
                    Math.Clamp(last.UsedPercent + (slowPace * elapsedHours), 0, 100),
                    Math.Clamp(last.UsedPercent + (fastPace * elapsedHours), 0, 100));
            })
            .ToArray();
    }

    private static double? ResolvePacePerHour(IReadOnlyList<UsageTrendPoint> actual, LimitRunwayForecast? forecast)
    {
        if (forecast?.PercentPerHour is double forecastPace && double.IsFinite(forecastPace) && forecastPace > 0)
        {
            return forecastPace;
        }

        if (actual.Count < 2)
        {
            return null;
        }

        var elapsedHours = (actual[^1].Timestamp - actual[0].Timestamp).TotalHours;
        if (elapsedHours <= 0)
        {
            return null;
        }

        var pace = (actual[^1].UsedPercent - actual[0].UsedPercent) / elapsedHours;
        return double.IsFinite(pace) && pace > 0 ? pace : null;
    }

    private static double? ResolveSustainablePace(UsageTrendPoint last, DateTimeOffset resetAt)
    {
        var hoursUntilReset = (resetAt - last.Timestamp).TotalHours;
        if (hoursUntilReset <= 0 || !double.IsFinite(hoursUntilReset) || last.UsedPercent >= 100)
        {
            return null;
        }

        var pace = (100 - Math.Clamp(last.UsedPercent, 0, 100)) / hoursUntilReset;
        return double.IsFinite(pace) && pace >= 0 ? pace : null;
    }

    private static DateTimeOffset ResolveWindowStart(LimitUsageTrend trend, DateTimeOffset firstObservedAt)
    {
        if (trend.WindowDurationMins is not int minutes || minutes <= 0)
        {
            return firstObservedAt;
        }

        var candidate = trend.ResetsAtUtc.AddMinutes(-minutes);
        return candidate < trend.ResetsAtUtc && candidate <= firstObservedAt
            ? candidate
            : firstObservedAt;
    }
}
