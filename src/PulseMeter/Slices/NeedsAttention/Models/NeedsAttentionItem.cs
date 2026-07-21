namespace PulseMeter.Slices.NeedsAttention.Models;

public sealed record NeedsAttentionItem(
    string BadgeText,
    string Title,
    string Detail,
    string AccentBrush,
    string? DiagnosticText = null,
    string? DismissSignalId = null,
    NeedsAttentionReviewTarget? ReviewTarget = null)
{
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool CanCopyDiagnostic => !string.IsNullOrWhiteSpace(DiagnosticText);

    public string CopyAccessibleLabel => CanCopyDiagnostic
        ? $"Copy diagnostic for {Title}"
        : string.Empty;

    public bool CanDismiss => !string.IsNullOrWhiteSpace(DismissSignalId);

    public bool CanReview => ReviewTarget is not null;

    public string ReviewAccessibleLabel => ReviewTarget switch
    {
        NeedsAttentionReviewTarget.RunwayForecast => "Review coding runway",
        NeedsAttentionReviewTarget.RateLimits => "Review rate limits",
        NeedsAttentionReviewTarget.ResetCredits => "Review reset credits",
        NeedsAttentionReviewTarget.DailyUsage => "Review daily usage",
        NeedsAttentionReviewTarget.ProjectUsage => "Review project usage",
        _ => string.Empty
    };

    public bool HasActions => CanCopyDiagnostic || CanDismiss || CanReview;
}
