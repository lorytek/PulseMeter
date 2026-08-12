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

    [Fact]
    public void IconCache_DeduplicatesVariantsAndDisposesOnce()
    {
        using var appIcon = (Icon)SystemIcons.Application.Clone();
        var cache = new TrayConfidenceIconCache(appIcon);
        var live = cache.Get(TrayConfidenceState.Live);
        Assert.Same(live, cache.Get(TrayConfidenceState.Live));
        Assert.NotSame(live, cache.Get(TrayConfidenceState.Stale));
        Assert.Equal(2, cache.CachedIconCount);
        cache.Dispose();
        cache.Dispose();
        Assert.Equal(0, cache.CachedIconCount);
        Assert.Throws<ObjectDisposedException>(() => cache.Get(TrayConfidenceState.Live));
    }

    [Fact]
    public void IconCache_RejectsNullBaseAliasesAndSharedStateIcons()
    {
        using var appIcon = (Icon)SystemIcons.Application.Clone();

        using (var nullCache = new TrayConfidenceIconCache(appIcon, (_, _) => null!))
        {
            Assert.Throws<InvalidOperationException>(() => nullCache.Get(TrayConfidenceState.Live));
            Assert.Equal(0, nullCache.CachedIconCount);
        }

        using (var baseAliasCache = new TrayConfidenceIconCache(appIcon, (baseIcon, _) => baseIcon))
        {
            Assert.Throws<InvalidOperationException>(() => baseAliasCache.Get(TrayConfidenceState.Live));
            Assert.Equal(0, baseAliasCache.CachedIconCount);
        }

        var sharedIcon = (Icon)SystemIcons.Application.Clone();
        using var sharedAliasCache = new TrayConfidenceIconCache(appIcon, (_, _) => sharedIcon);
        var live = sharedAliasCache.Get(TrayConfidenceState.Live);
        Assert.Throws<InvalidOperationException>(() => sharedAliasCache.Get(TrayConfidenceState.Stale));
        Assert.Same(live, sharedAliasCache.Get(TrayConfidenceState.Live));
        Assert.Equal(1, sharedAliasCache.CachedIconCount);
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
