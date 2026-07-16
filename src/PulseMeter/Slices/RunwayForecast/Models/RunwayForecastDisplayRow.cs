namespace PulseMeter.Slices.RunwayForecast.Models;

public sealed record RunwayForecastDisplayRow(
    string TrackText,
    string WindowText,
    string RemainingText,
    string StatusText,
    string StatusForeground,
    string StatusBackground,
    string StatusBorder,
    string ForecastText,
    string DetailText,
    string ResetText,
    string ResetCountdownText,
    string EvidenceText,
    string TooltipText);
