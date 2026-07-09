namespace PulseMeter.Slices.UsageSignals.Models;

public sealed record UsageAttentionSignal(
    int Priority,
    string BadgeText,
    string Title,
    string Detail,
    string AccentBrush,
    string? DiagnosticText = null,
    string? DismissSignalId = null);
