using System.Globalization;
using PulseMeter.Shared.Formatting;

namespace PulseMeter.Slices.RunwayForecast.Business;

public interface IRunwayForecastPresenter
{
    IReadOnlyList<RunwayForecastDisplayRow> BuildRows(
        IEnumerable<LimitRunwayForecast> forecasts,
        string? selectedLimitKey,
        DateTimeOffset now);
}

public sealed class RunwayForecastPresenter : IRunwayForecastPresenter
{
    public IReadOnlyList<RunwayForecastDisplayRow> BuildRows(
        IEnumerable<LimitRunwayForecast> forecasts,
        string? selectedLimitKey,
        DateTimeOffset now)
    {
        return forecasts
            .Where(forecast => string.Equals(
                forecast.LimitKey,
                selectedLimitKey,
                StringComparison.OrdinalIgnoreCase))
            .Where(forecast => forecast.ResetsAtUtc > now)
            .Take(8)
            .Select(forecast => BuildRow(forecast, now))
            .ToList();
    }

    private static RunwayForecastDisplayRow BuildRow(LimitRunwayForecast forecast, DateTimeOffset now)
    {
        var resetCountdown = CountdownFormatter.FormatResetCountdown(forecast.ResetsAtUtc.ToUnixTimeSeconds(), now);
        var status = BuildStatus(forecast.State);
        var forecastText = BuildForecastText(forecast, now);
        var detailText = BuildDetailText(forecast, now);
        var evidenceText = BuildEvidenceText(forecast);
        var resetText = IsWeekly(forecast)
            ? forecast.ResetsAtUtc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.InvariantCulture)
            : forecast.ResetsAtUtc.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture);

        return new RunwayForecastDisplayRow(
            forecast.TrackLabel,
            forecast.WindowLabel,
            $"{Math.Max(0, 100 - forecast.UsedPercent).ToString("0.#", CultureInfo.InvariantCulture)}% left",
            status.Text,
            status.Foreground,
            status.Background,
            status.Border,
            forecastText,
            detailText,
            resetText,
            resetCountdown == "reset unknown" ? "Reset time unavailable" : $"in {resetCountdown}",
            evidenceText,
            string.Join(Environment.NewLine,
                $"{forecast.TrackLabel} - {forecast.WindowLabel}",
                forecastText,
                detailText,
                evidenceText,
                $"Resets {resetText} ({resetCountdown})"));
    }

    private static string BuildForecastText(LimitRunwayForecast forecast, DateTimeOffset now)
    {
        return forecast.State switch
        {
            LimitRunwayForecastState.Learning => "Learning recent pace",
            LimitRunwayForecastState.Stable => "No recent movement",
            LimitRunwayForecastState.OnTrack => "Reset expected first",
            LimitRunwayForecastState.AtRisk
                when forecast.EarliestExhaustsAtUtc is DateTimeOffset earliest
                     && forecast.LatestExhaustsAtUtc is DateTimeOffset latest
                     && earliest > now => BuildExhaustionRange(earliest - now, latest - now),
            LimitRunwayForecastState.AtRisk when forecast.ExhaustsAtUtc is DateTimeOffset exhaustion =>
                exhaustion <= now
                    ? "Projection needs refresh"
                    : $"May run out in {FormatDuration(exhaustion - now)}",
            LimitRunwayForecastState.Exhausted => "Limit is fully used",
            _ => "Forecast unavailable"
        };
    }

    private static string BuildDetailText(LimitRunwayForecast forecast, DateTimeOffset now)
    {
        return forecast.State switch
        {
            LimitRunwayForecastState.Learning when forecast.ObservationDuration is TimeSpan duration =>
                $"Collecting a recent sample ({FormatDuration(duration)} so far).",
            LimitRunwayForecastState.Learning => "One more live sample after 2m will unlock a projection.",
            LimitRunwayForecastState.Stable =>
                "No measurable usage change was observed during this sample; this is not a long-term prediction.",
            LimitRunwayForecastState.OnTrack when forecast.ProjectedRemainingAtResetPercent is double remaining =>
                $"About {remaining.ToString("0.#", CultureInfo.InvariantCulture)}% is projected to remain at reset.",
            LimitRunwayForecastState.OnTrack => "The current window is projected to reset before exhaustion.",
            LimitRunwayForecastState.AtRisk when forecast.ExhaustsAtUtc <= now => "The last projection passed; waiting for fresh usage evidence.",
            LimitRunwayForecastState.AtRisk when forecast.IsActionable => "Recent pace reaches the limit before reset and is inside the alert window.",
            LimitRunwayForecastState.AtRisk => "Recent pace reaches the limit before reset; the alert window is not close yet.",
            LimitRunwayForecastState.Exhausted => "Waiting for this rate-limit window to reset.",
            _ => "Not enough recent movement to calculate a projection."
        };
    }

    private static string BuildEvidenceText(LimitRunwayForecast forecast)
    {
        if (forecast.IsMock)
        {
            return "Estimated | representative mock pace";
        }

        if (forecast.ObservationDuration is not TimeSpan duration)
        {
            return "Estimated | awaiting a second live sample";
        }

        var pace = forecast.PercentPerHour is double percentPerHour
            ? $" | {percentPerHour.ToString("0.#", CultureInfo.InvariantCulture)}%/h pace"
            : string.Empty;
        var readings = forecast.SampleCount == 1 ? "1 reading" : $"{forecast.SampleCount} readings";
        return $"Estimated | {FormatDuration(duration)} sample | {readings} | {FormatConfidence(forecast.Confidence)} confidence{pace}";
    }

    private static (string Text, string Foreground, string Background, string Border) BuildStatus(
        LimitRunwayForecastState state)
    {
        return state switch
        {
            LimitRunwayForecastState.Learning => ("Learning", "#475569", "#F8FAFC", "#CBD5E1"),
            LimitRunwayForecastState.Stable => ("No movement", "#1D4ED8", "#EFF6FF", "#BFDBFE"),
            LimitRunwayForecastState.OnTrack => ("On track", "#15803D", "#F0FDF4", "#86EFAC"),
            LimitRunwayForecastState.AtRisk => ("At risk", "#C2410C", "#FFF7ED", "#FDBA74"),
            LimitRunwayForecastState.Exhausted => ("Exhausted", "#B91C1C", "#FEF2F2", "#FCA5A5"),
            _ => ("Unknown", "#475569", "#F8FAFC", "#CBD5E1")
        };
    }

    private static bool IsWeekly(LimitRunwayForecast forecast)
    {
        return forecast.WindowDurationMins is int minutes && minutes >= 10_080;
    }

    private static string BuildExhaustionRange(TimeSpan earliest, TimeSpan latest)
    {
        var earliestText = FormatDuration(earliest);
        var latestText = FormatDuration(latest);
        return earliestText == latestText
            ? $"May run out in {earliestText}"
            : $"May run out in {earliestText} to {latestText}";
    }

    private static string FormatConfidence(LimitRunwayForecastConfidence confidence)
    {
        return confidence switch
        {
            LimitRunwayForecastConfidence.High => "High",
            LimitRunwayForecastConfidence.Medium => "Medium",
            _ => "Low"
        };
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "now";
        }

        if (value.TotalMinutes < 1)
        {
            return "<1m";
        }

        if (value.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero))}m";
        }

        var hours = (int)value.TotalHours;
        return value.Minutes == 0 ? $"{hours}h" : $"{hours}h {value.Minutes}m";
    }
}
