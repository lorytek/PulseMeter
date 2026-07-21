namespace PulseMeter.Slices.NeedsAttention.Business;

public interface INeedsAttentionPresenter
{
    IReadOnlyList<NeedsAttentionItem> BuildItems(UsageSignalsSnapshot signals);
}

public sealed class NeedsAttentionPresenter : INeedsAttentionPresenter
{
    public IReadOnlyList<NeedsAttentionItem> BuildItems(UsageSignalsSnapshot signals)
    {
        return signals.AttentionSignals
            .OrderBy(signal => signal.Priority)
            .Select(ToItem)
            .ToList();
    }

    private static NeedsAttentionItem ToItem(UsageAttentionSignal signal)
    {
        return new NeedsAttentionItem(
            signal.BadgeText,
            signal.Title,
            signal.Detail,
            signal.AccentBrush,
            signal.DiagnosticText,
            signal.DismissSignalId,
            GetReviewTarget(signal.Kind));
    }

    private static NeedsAttentionReviewTarget? GetReviewTarget(UsageAttentionSignalKind kind)
    {
        return kind switch
        {
            UsageAttentionSignalKind.Runway => NeedsAttentionReviewTarget.RunwayForecast,
            UsageAttentionSignalKind.RateLimit => NeedsAttentionReviewTarget.RateLimits,
            UsageAttentionSignalKind.ResetCredit => NeedsAttentionReviewTarget.ResetCredits,
            UsageAttentionSignalKind.DailyUsage => NeedsAttentionReviewTarget.DailyUsage,
            UsageAttentionSignalKind.ProjectUsage => NeedsAttentionReviewTarget.ProjectUsage,
            _ => null
        };
    }
}
