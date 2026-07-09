namespace PulseMeter.Tests;

public sealed class NeedsAttentionPresenterTests
{
    [Fact]
    public void BuildItems_MapsSignalsInPriorityOrderAndLimitsToThree()
    {
        var presenter = new NeedsAttentionPresenter();
        var items = presenter.BuildItems(new UsageSignalsSnapshot
        {
            AttentionSignals =
            [
                Signal(6, "TODAY", "Today is above usual", "250 tokens today; 100 tokens daily median.", "#1F73FF"),
                Signal(2, "IDLE", "Usage moved while idle", "Usage moved while idle: 82% -> 86% in 11m", "#F97316", "diagnostic", "idle-drain"),
                Signal(4, "LIMIT", "Weekly window is low", "8% left; resets in 10h 00m.", "#F97316"),
                Signal(3, "RUNWAY", "Projected to run out before reset", "At the current pace, 5h Window may run out in about 10m before the 5h reset.", "#F97316")
            ]
        });

        Assert.Equal(3, items.Count);
        Assert.Equal("IDLE", items[0].BadgeText);
        Assert.Equal("RUNWAY", items[1].BadgeText);
        Assert.Equal("LIMIT", items[2].BadgeText);
        Assert.True(items[0].CanDismiss);
        Assert.True(items[0].CanCopyDiagnostic);
    }

    [Fact]
    public void BuildItems_ReturnsEmptyWhenSignalsAreHealthy()
    {
        var presenter = new NeedsAttentionPresenter();

        var items = presenter.BuildItems(UsageSignalsSnapshot.Empty);

        Assert.Empty(items);
    }

    private static UsageAttentionSignal Signal(
        int priority,
        string badgeText,
        string title,
        string detail,
        string accentBrush,
        string? diagnosticText = null,
        string? dismissSignalId = null)
    {
        return new UsageAttentionSignal(
            priority,
            badgeText,
            title,
            detail,
            accentBrush,
            diagnosticText,
            dismissSignalId);
    }
}
