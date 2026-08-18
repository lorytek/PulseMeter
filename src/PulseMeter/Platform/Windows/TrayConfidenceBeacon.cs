using System.Drawing;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Platform.Windows;

public enum TrayConfidenceState
{
    Starting,
    Syncing,
    Live,
    Stale,
    Unavailable,
    Mock,
    Unknown
}

public static class TrayConfidenceBeacon
{
    public static TrayConfidenceState Map(bool isStarting, bool isRefreshing, SyncStatus syncStatus) =>
        isRefreshing ? TrayConfidenceState.Syncing :
        isStarting ? TrayConfidenceState.Starting :
        syncStatus switch
        {
            SyncStatus.Live => TrayConfidenceState.Live,
            SyncStatus.Stale => TrayConfidenceState.Stale,
            SyncStatus.Unavailable => TrayConfidenceState.Unavailable,
            SyncStatus.Mocked => TrayConfidenceState.Mock,
            _ => TrayConfidenceState.Unknown
        };

    public static string Tooltip(TrayConfidenceState state) => state switch
    {
        TrayConfidenceState.Starting => "PulseMeter — Starting",
        TrayConfidenceState.Syncing => "PulseMeter — Syncing",
        TrayConfidenceState.Live => "PulseMeter — Live",
        TrayConfidenceState.Stale => "PulseMeter — Stale",
        TrayConfidenceState.Unavailable => "PulseMeter — Unavailable",
        TrayConfidenceState.Mock => "PulseMeter — Mock",
        _ => "PulseMeter — Unknown"
    };

    public static string Tooltip(TrayIconPresentation presentation) =>
        presentation.RemainingPercent is int remainingPercent
            ? $"PulseMeter — Weekly {remainingPercent}% left"
            : Tooltip(presentation.ConfidenceState);

    public static Color UsageBadgeColor(int remainingPercent) => remainingPercent switch
    {
        >= 50 => Color.FromArgb(22, 163, 74),
        >= 25 => Color.FromArgb(31, 115, 255),
        >= 10 => Color.FromArgb(217, 119, 6),
        _ => Color.FromArgb(220, 38, 38)
    };
}

public readonly record struct TrayIconPresentation(
    TrayConfidenceState ConfidenceState,
    int? RemainingPercent)
{
    public static TrayIconPresentation Create(
        TrayConfidenceState confidenceState,
        double? remainingPercent)
    {
        if (confidenceState != TrayConfidenceState.Live
            || remainingPercent is not double value
            || !double.IsFinite(value))
        {
            return new TrayIconPresentation(confidenceState, null);
        }

        var rounded = (int)Math.Round(
            Math.Clamp(value, 0, 100),
            MidpointRounding.AwayFromZero);
        return new TrayIconPresentation(confidenceState, rounded);
    }
}

public sealed class TrayConfidenceTransitionTracker
{
    private TrayIconPresentation? _applied;

    public bool ShouldApply(TrayConfidenceState state) =>
        ShouldApply(TrayIconPresentation.Create(state, null));

    public bool ShouldApply(TrayIconPresentation presentation) => _applied != presentation;

    public void MarkApplied(TrayConfidenceState state) =>
        MarkApplied(TrayIconPresentation.Create(state, null));

    public void MarkApplied(TrayIconPresentation presentation) => _applied = presentation;
}
