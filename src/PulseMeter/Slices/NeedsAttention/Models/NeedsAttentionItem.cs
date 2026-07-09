namespace PulseMeter.Slices.NeedsAttention.Models;

public sealed record NeedsAttentionItem(
    string BadgeText,
    string Title,
    string Detail,
    string AccentBrush,
    string? DiagnosticText = null,
    string? DismissSignalId = null)
{
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool CanCopyDiagnostic => !string.IsNullOrWhiteSpace(DiagnosticText);

    public bool CanDismiss => !string.IsNullOrWhiteSpace(DismissSignalId);

    public bool HasActions => CanCopyDiagnostic || CanDismiss;
}
