namespace PulseMeter.Slices.NeedsAttention.Business;

public interface INeedsAttentionPresenter
{
    IReadOnlyList<NeedsAttentionItem> BuildItems(UsageSignalsSnapshot signals);
}

public sealed class NeedsAttentionPresenter : INeedsAttentionPresenter
{
    private const int MaximumItems = 3;

    public IReadOnlyList<NeedsAttentionItem> BuildItems(UsageSignalsSnapshot signals)
    {
        var ordered = signals.AttentionSignals
            .OrderBy(signal => signal.Priority)
            .ToList();

        var selected = signals.ShowAllAttentionSignals
            ? ordered
            : ordered.Take(MaximumItems);

        return selected.Select(ToItem).ToList();
    }

    private static NeedsAttentionItem ToItem(UsageAttentionSignal signal)
    {
        return new NeedsAttentionItem(
            signal.BadgeText,
            signal.Title,
            signal.Detail,
            signal.AccentBrush,
            signal.DiagnosticText,
            signal.DismissSignalId);
    }
}
