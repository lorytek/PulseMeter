namespace PulseMeter.Slices.UsageSignals.Models;

public enum UsageAttentionSignalKind
{
    Unknown,
    Sync,
    Idle,
    Runway,
    RateLimit,
    ResetCredit,
    DailyUsage,
    ProjectUsage,
    Budget
}

public sealed record UsageAttentionSignal(
    int Priority,
    string BadgeText,
    string Title,
    string Detail,
    string AccentBrush,
    string? DiagnosticText = null,
    string? DismissSignalId = null,
    UsageAttentionSignalKind Kind = UsageAttentionSignalKind.Unknown,
    string? ScopeId = null);
