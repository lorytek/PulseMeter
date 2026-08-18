using System.Drawing;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

public sealed class TrayConfidenceBeaconTests
{
    [Theory]
    [InlineData(false, false, SyncStatus.Live, TrayConfidenceState.Live, "PulseMeter — Live")]
    [InlineData(false, false, SyncStatus.Stale, TrayConfidenceState.Stale, "PulseMeter — Stale")]
    [InlineData(false, false, SyncStatus.Unavailable, TrayConfidenceState.Unavailable, "PulseMeter — Unavailable")]
    [InlineData(false, false, SyncStatus.Mocked, TrayConfidenceState.Mock, "PulseMeter — Mock")]
    [InlineData(true, false, SyncStatus.Unavailable, TrayConfidenceState.Starting, "PulseMeter — Starting")]
    [InlineData(false, true, SyncStatus.Live, TrayConfidenceState.Syncing, "PulseMeter — Syncing")]
    public void Mapper_UsesOnlyFixedConfidenceStates(
        bool starting,
        bool refreshing,
        SyncStatus status,
        TrayConfidenceState expected,
        string tooltip)
    {
        var state = TrayConfidenceBeacon.Map(starting, refreshing, status);
        Assert.Equal(expected, state);
        Assert.Equal(tooltip, TrayConfidenceBeacon.Tooltip(state));
        Assert.StartsWith("PulseMeter — ", TrayConfidenceBeacon.Tooltip(state));
    }

    [Fact]
    public void Mapper_RefreshWinsAndOverdueLiveCanBeProjectedAsStale()
    {
        Assert.Equal(TrayConfidenceState.Syncing, TrayConfidenceBeacon.Map(true, true, SyncStatus.Live));
        Assert.Equal(TrayConfidenceState.Stale, TrayConfidenceBeacon.Map(false, false, SyncStatus.Stale));
    }

    [Fact]
    public void Tooltip_NeverIncludesPoisonedSourceOrStatusText()
    {
        const string poison = "api-key=secret C:\\private\\customer 2026-08-11";
        foreach (var state in Enum.GetValues<TrayConfidenceState>())
        {
            Assert.DoesNotContain(poison, TrayConfidenceBeacon.Tooltip(state), StringComparison.Ordinal);
            Assert.Equal(TrayConfidenceBeacon.Tooltip(state), TrayConfidenceBeacon.Tooltip(state));
            Assert.InRange(TrayConfidenceBeacon.Tooltip(state).Length, 1, 63);
        }
    }

    [Theory]
    [InlineData(72.4, 72)]
    [InlineData(72.5, 73)]
    [InlineData(-8, 0)]
    [InlineData(140, 100)]
    public void Presentation_LiveUsageRoundsAndBoundsTheDisplayedNumber(
        double remainingPercent,
        int expected)
    {
        var presentation = TrayIconPresentation.Create(
            TrayConfidenceState.Live,
            remainingPercent);

        Assert.Equal(expected, presentation.RemainingPercent);
        Assert.Equal($"PulseMeter — Weekly {expected}% left", TrayConfidenceBeacon.Tooltip(presentation));
        Assert.InRange(TrayConfidenceBeacon.Tooltip(presentation).Length, 1, 63);
    }

    [Fact]
    public void Presentation_HidesUsageOutsideFreshLiveState()
    {
        Assert.Null(TrayIconPresentation.Create(TrayConfidenceState.Live, null).RemainingPercent);
        Assert.Null(TrayIconPresentation.Create(TrayConfidenceState.Live, double.NaN).RemainingPercent);
        Assert.Null(TrayIconPresentation.Create(TrayConfidenceState.Live, double.PositiveInfinity).RemainingPercent);
        Assert.Null(TrayIconPresentation.Create(TrayConfidenceState.Stale, 72).RemainingPercent);
        Assert.Null(TrayIconPresentation.Create(TrayConfidenceState.Syncing, 72).RemainingPercent);
        Assert.Equal(
            "PulseMeter — Stale",
            TrayConfidenceBeacon.Tooltip(TrayIconPresentation.Create(TrayConfidenceState.Stale, 72)));
    }

    [Theory]
    [InlineData(72, 22, 163, 74)]
    [InlineData(49, 31, 115, 255)]
    [InlineData(24, 217, 119, 6)]
    [InlineData(9, 220, 38, 38)]
    public void UsageBadgeColor_UsesReadableRemainingQuotaBands(
        int remainingPercent,
        int red,
        int green,
        int blue)
    {
        Assert.Equal(
            Color.FromArgb(red, green, blue).ToArgb(),
            TrayConfidenceBeacon.UsageBadgeColor(remainingPercent).ToArgb());
    }

    [Fact]
    public void TransitionTracker_OnlyAppliesStateTransitions()
    {
        var tracker = new TrayConfidenceTransitionTracker();
        Assert.True(tracker.ShouldApply(TrayConfidenceState.Starting));
        tracker.MarkApplied(TrayConfidenceState.Starting);
        Assert.False(tracker.ShouldApply(TrayConfidenceState.Starting));
        Assert.True(tracker.ShouldApply(TrayConfidenceState.Live));
    }

    [Fact]
    public void ViewModel_TrayStateTracksAppliedSnapshotsWithoutUsingSourceOrStatusMessage()
    {
        var viewModel = new PulseMeterWindowViewModel(new NoOpUsageService());
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal(TrayConfidenceState.Starting, viewModel.TrayConfidenceState);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Source = "Starting api-key=poison",
            StatusMessage = "C:\\private\\customer"
        });
        Assert.Equal(TrayConfidenceState.Live, viewModel.TrayConfidenceState);
        Assert.Contains(nameof(PulseMeterWindowViewModel.TrayConfidenceState), changes);

        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Stale, Source = "Starting" });
        Assert.Equal(TrayConfidenceState.Stale, viewModel.TrayConfidenceState);
        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Live, LastUpdatedUtc = DateTimeOffset.UtcNow });
        Assert.Equal(TrayConfidenceState.Live, viewModel.TrayConfidenceState);
        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Live, LastUpdatedUtc = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10) });
        Assert.Equal(TrayConfidenceState.Stale, viewModel.TrayConfidenceState);
        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Unavailable });
        Assert.Equal(TrayConfidenceState.Unavailable, viewModel.TrayConfidenceState);
        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Mocked });
        Assert.Equal(TrayConfidenceState.Mock, viewModel.TrayConfidenceState);
    }

    private sealed class NoOpUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated { add { } remove { } }
        public bool UseMockMode { get; set; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new UsageSnapshot());
    }
}
