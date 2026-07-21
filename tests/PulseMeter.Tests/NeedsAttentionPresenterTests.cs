using PulseMeter.Slices.NeedsAttention.Models;

namespace PulseMeter.Tests;

public sealed class NeedsAttentionPresenterTests
{
    [Fact]
    public void BuildItems_MapsEverySignalInPriorityOrder()
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

        Assert.Equal(4, items.Count);
        Assert.Equal("IDLE", items[0].BadgeText);
        Assert.Equal("RUNWAY", items[1].BadgeText);
        Assert.Equal("LIMIT", items[2].BadgeText);
        Assert.Equal("TODAY", items[3].BadgeText);
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

    [Fact]
    public void BuildItems_MapsSemanticKindsToReviewTargetsRegardlessOfBadgeText()
    {
        var presenter = new NeedsAttentionPresenter();
        var items = presenter.BuildItems(new UsageSignalsSnapshot
        {
            ShowAllAttentionSignals = true,
            AttentionSignals =
            [
                Signal(1, "Renamed runway badge", "Runway", "", "#F97316", kind: UsageAttentionSignalKind.Runway),
                Signal(2, "Renamed limit badge", "Limit", "", "#F97316", kind: UsageAttentionSignalKind.RateLimit),
                Signal(3, "Renamed credit badge", "Credit", "", "#F97316", kind: UsageAttentionSignalKind.ResetCredit),
                Signal(4, "Renamed today badge", "Today", "", "#F97316", kind: UsageAttentionSignalKind.DailyUsage),
                Signal(5, "Renamed project badge", "Project", "", "#F97316", kind: UsageAttentionSignalKind.ProjectUsage),
                Signal(6, "RUNWAY", "Sync", "", "#F97316", kind: UsageAttentionSignalKind.Sync),
                Signal(7, "PROJECT", "Idle", "", "#F97316", kind: UsageAttentionSignalKind.Idle),
                Signal(8, "LIMIT", "Budget", "", "#F97316", kind: UsageAttentionSignalKind.Budget),
                Signal(9, "TODAY", "Unknown", "", "#F97316")
            ]
        });

        Assert.Equal(NeedsAttentionReviewTarget.RunwayForecast, items[0].ReviewTarget);
        Assert.Equal(NeedsAttentionReviewTarget.RateLimits, items[1].ReviewTarget);
        Assert.Equal(NeedsAttentionReviewTarget.ResetCredits, items[2].ReviewTarget);
        Assert.Equal(NeedsAttentionReviewTarget.DailyUsage, items[3].ReviewTarget);
        Assert.Equal(NeedsAttentionReviewTarget.ProjectUsage, items[4].ReviewTarget);

        Assert.All(items.Skip(5), item =>
        {
            Assert.Null(item.ReviewTarget);
            Assert.False(item.CanReview);
            Assert.Equal(string.Empty, item.ReviewAccessibleLabel);
        });
        Assert.Equal("Review coding runway", items[0].ReviewAccessibleLabel);
    }

    private static UsageAttentionSignal Signal(
        int priority,
        string badgeText,
        string title,
        string detail,
        string accentBrush,
        string? diagnosticText = null,
        string? dismissSignalId = null,
        UsageAttentionSignalKind kind = UsageAttentionSignalKind.Unknown)
    {
        return new UsageAttentionSignal(
            priority,
            badgeText,
            title,
            detail,
            accentBrush,
            diagnosticText,
            dismissSignalId,
            kind);
    }
}
