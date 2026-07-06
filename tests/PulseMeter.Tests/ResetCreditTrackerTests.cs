using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Slices.ResetCredits;
using System.Globalization;

namespace PulseMeter.Tests;

public sealed class ResetCreditTrackerTests
{
    [Fact]
    public void ApplySnapshot_DisplaysExistingCreditsAsUnknownWhenExpiryIsUnavailable()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 2
        });

        Assert.Equal("2 reset credits available", viewModel.ResetCreditsHeaderText);
        Assert.Equal(
            ["Credit 1 - expiry unavailable", "Credit 2 - expiry unavailable"],
            viewModel.ResetCredits.Select(credit => credit.DisplayText));
    }

    [Fact]
    public void Update_AssignsThirtyDayExpiryToNewlyDetectedCredits()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        tracker.Update(2, null, now);
        var updated = tracker.Update(3, null, now);

        Assert.Equal(
            [
                "Credit 1 - expiry unavailable",
                "Credit 2 - expiry unavailable",
                "Credit 3 - expires in 30 days"
            ],
            updated.Select(credit => credit.DisplayText));
    }

    [Fact]
    public void Refresh_UpdatesExpiryTextWhenClockMovesAcrossDisplayBoundary()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        var initial = tracker.Update(1, now.AddHours(2), now);
        var refreshed = tracker.Refresh(now.AddHours(1).AddMinutes(1));

        Assert.Equal("Credit 1 - expires in 2 hours", Assert.Single(initial).DisplayText);
        Assert.Equal("Credit 1 - expires in 59 minutes", Assert.Single(refreshed).DisplayText);
    }

    [Fact]
    public void Update_RemovesAVisibleCreditWhenAvailableCountDropsAndDoesNotReuseItsLocalNumber()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        tracker.Update(2, null, now);
        tracker.Update(3, null, now.AddHours(1));

        var afterUse = tracker.Update(2, null, now.AddHours(2));

        Assert.Equal(
            ["Credit 1 - expiry unavailable", "Credit 2 - expiry unavailable"],
            afterUse.Select(credit => credit.DisplayText));

        var afterNewCredit = tracker.Update(3, null, now.AddHours(3));

        Assert.Equal(
            [
                "Credit 1 - expiry unavailable",
                "Credit 2 - expiry unavailable",
                "Credit 4 - expires in 30 days"
            ],
            afterNewCredit.Select(credit => credit.DisplayText));
    }

    [Fact]
    public void CaptureState_RestoresDetectedCreditExpiryAfterRestart()
    {
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
        var tracker = new ResetCreditTracker();

        tracker.Update(2, null, now);
        tracker.Update(3, null, now);

        var restored = new ResetCreditTracker(tracker.CaptureState());
        var restoredCredits = restored.Refresh(now.AddHours(1));

        Assert.Equal("Credit 3 - expires in 30 days", restoredCredits[2].DisplayText);
    }

    [Fact]
    public void PulseMeterWindowViewModel_LoadsPersistedResetCreditStateOnStartup()
    {
        var store = new StubResetCreditStateStore(new ResetCreditTrackerState(
            HasObservedAvailableCount: true,
            NextCreditNumber: 4,
            Credits:
            [
                new ResetCreditState(3, DateTimeOffset.UtcNow.AddDays(30))
            ]));

        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), resetCreditStateStore: store);

        Assert.Equal("Credit 3 - expires in 30 days", Assert.Single(viewModel.ResetCredits).DisplayText);
    }

    [Fact]
    public void PulseMeterWindowViewModel_DisplaysCountdownForNewlyDetectedCredit()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 0,
            SyncStatus = SyncStatus.Live
        });
        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 1,
            SyncStatus = SyncStatus.Live
        });

        Assert.Equal("Credit 1 - expires in 30 days", Assert.Single(viewModel.ResetCredits).DisplayText);
    }

    [Fact]
    public void PulseMeterWindowViewModel_PersistsLiveResetCreditStateWhenSnapshotUpdates()
    {
        var store = new StubResetCreditStateStore();
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), resetCreditStateStore: store);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 2,
            SyncStatus = SyncStatus.Live
        });

        Assert.NotNull(store.SavedState);
        Assert.Equal(2, store.SavedState.Credits.Count);
    }

    [Fact]
    public void PulseMeterWindowViewModel_DoesNotPersistMockResetCreditState()
    {
        var store = new StubResetCreditStateStore();
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), resetCreditStateStore: store);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 2,
            SyncStatus = SyncStatus.Mocked
        });

        Assert.Null(store.SavedState);
    }

    [Fact]
    public void PulseMeterWindowViewModel_ShowsCountdownForMockThirdCreditWhenTwoCreditsWereAlreadyKnown()
    {
        var store = new StubResetCreditStateStore(new ResetCreditTrackerState(
            HasObservedAvailableCount: true,
            NextCreditNumber: 3,
            Credits:
            [
                new ResetCreditState(1, null),
                new ResetCreditState(2, null)
            ]));
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), resetCreditStateStore: store);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ResetCreditsAvailable = 3,
            SyncStatus = SyncStatus.Mocked
        });

        Assert.Equal(
            [
                "Credit 1 - expiry unavailable",
                "Credit 2 - expiry unavailable",
                "Credit 3 - expires in 30 days"
            ],
            viewModel.ResetCredits.Select(credit => credit.DisplayText));
        Assert.Null(store.SavedState);
    }

    [Fact]
    public void Update_UsesAppServerExpiryWhenProvided()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

        var updated = tracker.Update(1, now.AddDays(10), now);

        Assert.Equal("Credit 1 - expires in 10 days", Assert.Single(updated).DisplayText);
    }

    [Fact]
    public void Update_UsesExactPerCreditExpiryDatesWhenProvided()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
        var laterExpiry = now.AddDays(10);
        var earlierExpiry = now.AddDays(3).AddHours(2);

        var updated = tracker.Update(
            2,
            null,
            [
                new ResetCreditSnapshot(now.AddDays(-1), laterExpiry, "available"),
                new ResetCreditSnapshot(now.AddDays(-2), earlierExpiry, "available")
            ],
            now);

        Assert.Equal(
            [
                $"Credit 1 - expires {earlierExpiry.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture)} (in 4 days)",
                $"Credit 2 - expires {laterExpiry.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture)} (in 10 days)"
            ],
            updated.Select(credit => credit.DisplayText));
    }

    [Fact]
    public void Update_ReplacesVisibleCreditsFromExactBackendListWhenOneWasUsed()
    {
        var tracker = new ResetCreditTracker();
        var now = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
        var remainingExpiry = now.AddDays(6);

        tracker.Update(
            2,
            null,
            [
                new ResetCreditSnapshot(now.AddDays(-2), now.AddDays(3), "available"),
                new ResetCreditSnapshot(now.AddDays(-1), remainingExpiry, "available")
            ],
            now);

        var afterUse = tracker.Update(
            1,
            null,
            [new ResetCreditSnapshot(now.AddDays(-1), remainingExpiry, "available")],
            now.AddHours(1));

        Assert.Equal(
            $"Credit 1 - expires {remainingExpiry.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture)} (in 6 days)",
            Assert.Single(afterUse).DisplayText);
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new UsageSnapshot();
            SnapshotUpdated?.Invoke(this, snapshot);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubResetCreditStateStore : IResetCreditStateStore
    {
        private readonly ResetCreditTrackerState? _loadedState;

        public StubResetCreditStateStore(ResetCreditTrackerState? loadedState = null)
        {
            _loadedState = loadedState;
        }

        public ResetCreditTrackerState? SavedState { get; private set; }

        public ResetCreditTrackerState? Load()
        {
            return _loadedState;
        }

        public void Save(ResetCreditTrackerState state)
        {
            SavedState = state;
        }
    }
}
