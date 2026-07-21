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
        UsageTrendForecastReference? referenceForecast = null);
}

public sealed class UsageTrendPresenter : IUsageTrendPresenter
{
    private const int ProjectionPointCount = 13;
    private const double UnfavorableVarianceThresholdPoints = 1;

    public UsageTrendChartModel? BuildChart(
        LimitUsageTrend trend,
        LimitRunwayForecast? forecast,
        DateTimeOffset now,
        bool showProjection,
        bool showRange,
        UsageTrendForecastReference? referenceForecast = null)
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
            ReferenceForecastCapturedAt = referenceProjected.Length > 1 ? referenceForecast?.CapturedAt : null
        };
    }

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
            .ToArray()
            ?? [];

        if (points.Length == 0 || points[0].Timestamp <= last.Timestamp)
        {
            return points;
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
            forecastLead = "Waiting for the limit to reset";
            forecastWhen = FormatLocalDateTime(resetAt);
        }
        else if (pointExhaustion is DateTimeOffset exhaustsAt && exhaustsAt > now && exhaustsAt < resetAt)
        {
            headline = $"About {FormatDurationCompact(exhaustsAt - now)} left at this pace";
            forecastLead = forecast?.Confidence is LimitRunwayForecastConfidence.Medium or LimitRunwayForecastConfidence.High
                ? "Estimated to reach the limit"
                : "May reach the limit";
            forecastWhen = FormatLocalDateTime(exhaustsAt);
        }
        else if (forecastWindowStart is DateTimeOffset earliest && forecastWindowEnd is DateTimeOffset latest)
        {
            headline = $"About {FormatDurationRange(earliest - now, latest - now)} left at this pace";
            forecastLead = "Could reach the limit between";
            forecastWhen = FormatDateTimeRange(earliest, latest);
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
            forecastLead = "Projected to remain below the limit until";
            forecastWhen = FormatLocalDateTime(resetAt);
        }

        var currentPaceText = FormatPace(pacePerHour);
        var sustainablePaceText = FormatPace(sustainablePacePerHour);
        var momentum = BuildUsageMomentum(actual, windowDurationMins);
        var paceRatio = pacePerHour is double currentPace
            && currentPace > 0
            && sustainablePacePerHour is double sustainablePace
            && sustainablePace > 0
                ? currentPace / sustainablePace
                : double.NaN;
        var hasPaceRatio = double.IsFinite(paceRatio);
        var projectedRemaining = forecast?.ProjectedRemainingAtResetPercent;
        double? paceProjectedRemaining = pacePerHour is double currentPaceAtReset && currentPaceAtReset >= 0
            ? 100 - (last.UsedPercent + (currentPaceAtReset * Math.Max(0, (resetAt - last.Timestamp).TotalHours)))
            : null;
        if (projectedRemaining is not double remaining || !double.IsFinite(remaining))
        {
            projectedRemaining = paceProjectedRemaining;
        }
        else if (remaining >= 0
            && pointExhaustion is DateTimeOffset exhaustsBeforeReset
            && exhaustsBeforeReset < resetAt
            && paceProjectedRemaining is double paceRemaining
            && paceRemaining < 0)
        {
            projectedRemaining = paceRemaining;
        }

        var comparisonText = projectedRemaining is double resetRemaining && double.IsFinite(resetRemaining)
            ? resetRemaining >= 0
                ? $"{resetRemaining:0}%"
                : $"-{Math.Abs(resetRemaining):0}%"
            : "—";
        var comparisonLabel = projectedRemaining is double outcome && double.IsFinite(outcome)
            ? outcome >= 0 ? "remaining at reset" : "will reach limit before reset"
            : "outcome at reset";

        string recommendation;
        if (isExhausted)
        {
            recommendation = "Wait until the limit resets before starting another coding run";
        }
        else if (hasPaceRatio
            && paceRatio > 1.05
            && forecast is { Confidence: LimitRunwayForecastConfidence.Low, IsMock: false })
        {
            recommendation = "Current pace is above sustainable; treat this as an early signal";
        }
        else if (hasPaceRatio && paceRatio > 1.05)
        {
            var reduction = Math.Clamp(Math.Round((1 - (1 / paceRatio)) * 20) * 5, 5, 95);
            recommendation = $"Reduce pace by about {reduction:0}% to last until reset";
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
            BuildConfidenceText(forecast),
            $"{Math.Clamp(last.UsedPercent, 0, 100):0}%",
            momentum,
            currentPaceText,
            sustainablePaceText,
            comparisonText,
            comparisonLabel,
            recommendation,
            CanOpenPacingPlan: !isExhausted);
    }

    internal static UsageMomentumSummary BuildUsageMomentum(
        IReadOnlyList<UsageTrendPoint> actual,
        int? windowDurationMins)
    {
        if (actual.Count < 2)
        {
            return LearningMomentum(windowDurationMins);
        }

        return windowDurationMins >= 24 * 60
            ? BuildDailyMedianMomentum(actual)
            : BuildWindowMedianMomentum(actual, windowDurationMins);
    }

    private static UsageMomentumSummary BuildWindowMedianMomentum(
        IReadOnlyList<UsageTrendPoint> actual,
        int? windowDurationMins)
    {
        var latest = actual[^1].Timestamp;
        var currentRate = UsageRateBetween(actual, latest.AddHours(-1), latest);
        var hours = Math.Max(2, (int)Math.Round((windowDurationMins ?? 300) / 60d));
        var historicalRates = Enumerable.Range(1, Math.Max(1, hours - 1))
            .Select(offset => UsageRateBetween(actual, latest.AddHours(-(offset + 1)), latest.AddHours(-offset)))
            .Where(rate => rate is double value && double.IsFinite(value))
            .Select(rate => rate!.Value)
            .ToArray();

        return currentRate is double current && historicalRates.Length >= 2
            ? CreateMomentum(current, Median(historicalRates), "vs 5h window median")
            : LearningMomentum(windowDurationMins);
    }

    private static UsageMomentumSummary BuildDailyMedianMomentum(IReadOnlyList<UsageTrendPoint> actual)
    {
        var latest = actual[^1].Timestamp;
        var currentDayStart = StartOfLocalDay(latest);
        var currentRate = UsageRateBetween(actual, currentDayStart, latest);
        var completedDayRates = Enumerable.Range(1, 6)
            .Select(offset =>
            {
                var end = currentDayStart.AddDays(-(offset - 1));
                return UsageRateBetween(actual, end.AddDays(-1), end);
            })
            .Where(rate => rate is double value && double.IsFinite(value))
            .Select(rate => rate!.Value)
            .ToArray();

        return currentRate is double current && completedDayRates.Length >= 2
            ? CreateMomentum(current, Median(completedDayRates), "vs median day")
            : LearningMomentum(10_080);
    }

    private static UsageMomentumSummary CreateMomentum(double currentRate, double medianRate, string baselineText)
    {
        var difference = currentRate - medianRate;
        var steadyThreshold = Math.Max(0.05, Math.Abs(medianRate) * 0.1);
        var scale = Math.Max(0.25, Math.Abs(medianRate));
        var gaugeValue = Math.Clamp(difference / scale, -1, 1);

        if (Math.Abs(difference) <= steadyThreshold)
        {
            return new UsageMomentumSummary("→ 0%/h", "pace steady", baselineText, 0);
        }

        return difference > 0
            ? new UsageMomentumSummary($"↗ +{difference:0.#}%/h", "usage accelerating", baselineText, gaugeValue)
            : new UsageMomentumSummary($"↘ -{Math.Abs(difference):0.#}%/h", "usage slowing", baselineText, gaugeValue);
    }

    private static UsageMomentumSummary LearningMomentum(int? windowDurationMins) =>
        new(
            "—",
            "learning baseline",
            windowDurationMins >= 24 * 60 ? "vs median day" : "vs 5h window median",
            0);

    private static double? UsageRateBetween(
        IReadOnlyList<UsageTrendPoint> actual,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var hours = (end - start).TotalHours;
        if (hours <= 0
            || InterpolatePointAt(actual, start) is not UsageTrendPoint startPoint
            || InterpolatePointAt(actual, end) is not UsageTrendPoint endPoint)
        {
            return null;
        }

        return Math.Max(0, endPoint.UsedPercent - startPoint.UsedPercent) / hours;
    }

    private static DateTimeOffset StartOfLocalDay(DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        return new DateTimeOffset(local.Date, local.Offset);
    }

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

    private static string BuildConfidenceText(LimitRunwayForecast? forecast)
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
                return duration < TimeSpan.FromHours(24)
                    ? $"Building 24h baseline • {FormatEvidenceDuration(duration)} of 24h collected · {evidence}"
                    : $"{forecast.Confidence} evidence • {evidence} over latest 24h";
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
