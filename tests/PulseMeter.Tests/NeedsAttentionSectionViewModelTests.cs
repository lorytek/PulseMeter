using PulseMeter.Platform.Timing;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.NeedsAttention.Models;
using PulseMeter.Slices.NeedsAttention.UI;

namespace PulseMeter.Tests;

public sealed class NeedsAttentionSectionViewModelTests
{
    [Fact]
    public void CopyDiagnostic_CopiesTextShowsFeedbackAndClearsAfterTimerTick()
    {
        var clipboard = new RecordingClipboardService();
        var timerFactory = new StubTimerFactory();
        var viewModel = CreateViewModel(clipboard, timerFactory);
        var item = new NeedsAttentionItem("IDLE", "Usage moved while idle", "Detail", "#F97316", "diagnostic text");
        var propertyChanges = new List<string?>();
        viewModel.PropertyChanged += (_, e) => propertyChanges.Add(e.PropertyName);

        viewModel.CopyDiagnosticCommand.Execute(item);

        Assert.Equal("diagnostic text", clipboard.Text);
        Assert.Equal("Diagnostic copied", viewModel.CopyFeedbackText);
        Assert.Equal("#15803D", viewModel.CopyFeedbackBrush);
        Assert.True(viewModel.HasCopyFeedback);
        Assert.True(timerFactory.Timer.Started);
        Assert.Equal(TimeSpan.FromSeconds(3), timerFactory.Timer.Interval);
        Assert.True(
            propertyChanges.IndexOf(nameof(viewModel.HasCopyFeedback)) <
            propertyChanges.IndexOf(nameof(viewModel.CopyFeedbackText)));

        timerFactory.Timer.RaiseTick();

        Assert.False(viewModel.HasCopyFeedback);
        Assert.Equal(string.Empty, viewModel.CopyFeedbackText);
        Assert.False(timerFactory.Timer.Started);
    }

    [Fact]
    public void CopyDiagnostic_WhenClipboardFails_ShowsSafeRetryFeedback()
    {
        var timerFactory = new StubTimerFactory();
        var viewModel = CreateViewModel(new ThrowingClipboardService(), timerFactory);
        var item = new NeedsAttentionItem("IDLE", "Usage moved while idle", "Detail", "#F97316", "private diagnostic");

        viewModel.CopyDiagnosticCommand.Execute(item);

        Assert.Equal("Couldn't copy. Try again.", viewModel.CopyFeedbackText);
        Assert.Equal("#DC2626", viewModel.CopyFeedbackBrush);
        Assert.DoesNotContain("private diagnostic", viewModel.CopyFeedbackText);
        Assert.True(timerFactory.Timer.Started);
    }

    [Fact]
    public void CopyAccessibleLabel_IdentifiesTheSignal()
    {
        var item = new NeedsAttentionItem("IDLE", "Usage moved while idle", "Detail", "#F97316", "diagnostic");

        Assert.Equal("Copy diagnostic for Usage moved while idle", item.CopyAccessibleLabel);
    }

    [Fact]
    public void AttentionPreview_ShowsTopThreeAndCanRevealEverySignal()
    {
        var viewModel = new NeedsAttentionSectionViewModel(new NeedsAttentionPresenter());
        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            AttentionSignals =
            [
                Signal(5, "FIVE"),
                Signal(1, "ONE"),
                Signal(4, "FOUR"),
                Signal(2, "TWO"),
                Signal(3, "THREE")
            ]
        });

        Assert.Equal(5, viewModel.NeedsAttentionItems.Count);
        Assert.Equal(["ONE", "TWO", "THREE"], viewModel.VisibleNeedsAttentionItems.Select(item => item.BadgeText));
        Assert.True(viewModel.HasHiddenAttentionItems);
        Assert.False(viewModel.IsShowingAll);
        Assert.Equal("Show all 5", viewModel.ToggleAttentionItemsText);
        Assert.Equal("Show all 5 attention signals", viewModel.ToggleAttentionItemsAccessibleLabel);

        viewModel.ToggleAttentionItemsCommand.Execute(null);

        Assert.Equal(5, viewModel.VisibleNeedsAttentionItems.Count);
        Assert.True(viewModel.IsShowingAll);
        Assert.Equal("Show top 3", viewModel.ToggleAttentionItemsText);
        Assert.Equal("Show top 3 attention signals", viewModel.ToggleAttentionItemsAccessibleLabel);

        viewModel.ToggleAttentionItemsCommand.Execute(null);

        Assert.Equal(3, viewModel.VisibleNeedsAttentionItems.Count);
        Assert.False(viewModel.IsShowingAll);
    }

    [Fact]
    public void AttentionPreview_CollapsesWhenSignalCountDropsBelowPreviewLimit()
    {
        var viewModel = new NeedsAttentionSectionViewModel(new NeedsAttentionPresenter());
        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            AttentionSignals = [Signal(1, "ONE"), Signal(2, "TWO"), Signal(3, "THREE"), Signal(4, "FOUR")]
        });
        viewModel.ToggleAttentionItemsCommand.Execute(null);

        viewModel.ApplySignals(new UsageSignalsSnapshot
        {
            AttentionSignals = [Signal(1, "ONE"), Signal(2, "TWO")]
        });

        Assert.False(viewModel.HasHiddenAttentionItems);
        Assert.False(viewModel.IsShowingAll);
        Assert.Equal(2, viewModel.VisibleNeedsAttentionItems.Count);
    }

    [Fact]
    public void DismissIdleAlert_ShowsUndoAndDefersTrackerDismissal()
    {
        var timerFactory = new StubTimerFactory();
        var tracker = new RecordingUsageSignalsTracker();
        var viewModel = new NeedsAttentionSectionViewModel(
            new NeedsAttentionPresenter(),
            tracker,
            timerFactory: timerFactory);
        viewModel.ApplySignals(new UsageSignalsSnapshot { AttentionSignals = [IdleSignal()] });

        viewModel.DismissSignalCommand.Execute(Assert.Single(viewModel.NeedsAttentionItems));

        Assert.Empty(viewModel.NeedsAttentionItems);
        Assert.True(viewModel.HasPendingDismissal);
        Assert.Equal("Idle alert dismissed.", viewModel.DismissUndoText);
        Assert.True(viewModel.UndoDismissCommand.CanExecute(null));
        Assert.Equal(0, tracker.DismissCount);
        Assert.True(timerFactory.DismissTimer.Started);
        Assert.Equal(TimeSpan.FromSeconds(8), timerFactory.DismissTimer.Interval);

        viewModel.UndoDismissCommand.Execute(null);

        Assert.Single(viewModel.NeedsAttentionItems);
        Assert.False(viewModel.HasPendingDismissal);
        Assert.False(viewModel.UndoDismissCommand.CanExecute(null));
        Assert.False(timerFactory.DismissTimer.Started);
        Assert.Equal(0, tracker.DismissCount);
    }

    [Fact]
    public void DismissIdleAlert_WhenUndoWindowExpires_CommitsTrackerDismissal()
    {
        var timerFactory = new StubTimerFactory();
        var tracker = new RecordingUsageSignalsTracker();
        var viewModel = new NeedsAttentionSectionViewModel(
            new NeedsAttentionPresenter(),
            tracker,
            timerFactory: timerFactory);
        viewModel.ApplySignals(new UsageSignalsSnapshot { AttentionSignals = [IdleSignal()] });

        viewModel.DismissSignalCommand.Execute(Assert.Single(viewModel.NeedsAttentionItems));
        timerFactory.DismissTimer.RaiseTick();

        Assert.Empty(viewModel.NeedsAttentionItems);
        Assert.False(viewModel.HasPendingDismissal);
        Assert.False(timerFactory.DismissTimer.Started);
        Assert.Equal(1, tracker.DismissCount);
    }

    [Fact]
    public void DismissIdleAlert_WhenSignalNaturallyClears_CancelsPendingUndo()
    {
        var timerFactory = new StubTimerFactory();
        var tracker = new RecordingUsageSignalsTracker();
        var viewModel = new NeedsAttentionSectionViewModel(
            new NeedsAttentionPresenter(),
            tracker,
            timerFactory: timerFactory);
        viewModel.ApplySignals(new UsageSignalsSnapshot { AttentionSignals = [IdleSignal()] });
        viewModel.DismissSignalCommand.Execute(Assert.Single(viewModel.NeedsAttentionItems));

        viewModel.ApplySignals(UsageSignalsSnapshot.Empty);

        Assert.False(viewModel.HasPendingDismissal);
        Assert.False(timerFactory.DismissTimer.Started);
        Assert.Equal(0, tracker.DismissCount);
        Assert.Empty(viewModel.NeedsAttentionItems);
    }

    private static NeedsAttentionSectionViewModel CreateViewModel(
        IClipboardService clipboard,
        IPulseMeterTimerFactory timerFactory)
    {
        return new NeedsAttentionSectionViewModel(
            new NeedsAttentionPresenter(),
            clipboardService: clipboard,
            timerFactory: timerFactory);
    }

    private static UsageAttentionSignal Signal(int priority, string badgeText)
    {
        return new UsageAttentionSignal(
            priority,
            badgeText,
            $"Signal {badgeText}",
            "Detail",
            "#F97316");
    }

    private static UsageAttentionSignal IdleSignal()
    {
        return new UsageAttentionSignal(
            1,
            "IDLE",
            "Usage moved while idle",
            "Usage moved while idle: 92% -> 96% in 11m",
            "#F97316",
            "diagnostic",
            "idle-drain",
            UsageAttentionSignalKind.Idle);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class ThrowingClipboardService : IClipboardService
    {
        public void SetText(string text) => throw new InvalidOperationException("Clipboard busy at C:\\private\\path");
    }

    private sealed class StubTimerFactory : IPulseMeterTimerFactory
    {
        public List<StubTimer> Timers { get; } = [];

        public StubTimer Timer => Timers[0];

        public StubTimer DismissTimer => Timers[1];

        public IPulseMeterTimer Create(TimeSpan interval)
        {
            var timer = new StubTimer { Interval = interval };
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class RecordingUsageSignalsTracker : IUsageSignalsTracker
    {
        public int DismissCount { get; private set; }

        public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
        {
            return UsageSignalsSnapshot.Empty;
        }

        public void DismissIdleDrain() => DismissCount++;
    }

    private sealed class StubTimer : IPulseMeterTimer
    {
        public event EventHandler? Tick;

        public TimeSpan Interval { get; set; }

        public bool Started { get; private set; }

        public void Start() => Started = true;

        public void Stop() => Started = false;

        public void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);
    }
}
