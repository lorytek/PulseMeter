using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class MockCodexUsageServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsObviousMockData()
    {
        var service = new MockCodexUsageService();

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Mocked, snapshot.SyncStatus);
        Assert.Equal("Mock", snapshot.Source);
        Assert.True(snapshot.Buckets.Count >= 5);
        Assert.All(snapshot.Buckets, bucket => Assert.False(string.IsNullOrWhiteSpace(bucket.Label)));
        Assert.Equal(3, snapshot.ResetCreditsAvailable);
        Assert.NotNull(snapshot.ResetCreditsExpiresAtUtc);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsShowcaseDataForEveryDashboardSlice()
    {
        var service = new MockCodexUsageService();

        var snapshot = await service.GetSnapshotAsync();
        var snapshotNow = Assert.IsType<DateTimeOffset>(snapshot.LastUpdatedUtc);
        var snapshotToday = DateOnly.FromDateTime(snapshotNow.LocalDateTime).ToString("yyyy-MM-dd");

        Assert.Contains(snapshot.Buckets, bucket => bucket.UsedPercent >= 90);
        Assert.Contains(snapshot.Buckets, bucket => bucket.UsedPercent is >= 75 and < 90);
        Assert.Contains(snapshot.Buckets, bucket => bucket.IsReached);
        Assert.Contains(snapshot.Buckets, bucket => bucket.WindowDurationMins >= 10_080 && bucket.RemainingPercentValue <= 25);
        Assert.True(snapshot.DailyBuckets.Count >= 7);
        Assert.Contains(snapshot.DailyBuckets, bucket => bucket.StartDate == snapshotToday && bucket.TotalTokens >= 900_000);
        Assert.True(snapshot.ProjectUsageRows.Count >= 3);
        Assert.Contains(snapshot.ProjectUsageRows, row => row.SharePercent >= 55);
        Assert.True(snapshot.UsageAttribution.HasAttribution);
        Assert.True(snapshot.UsageAttribution.Sessions.Count >= 3);
        Assert.True(snapshot.UsageAttribution.BurnEvents.Count >= 3);
        Assert.Equal("Estimated from local chats, scaled to account usage", snapshot.UsageAttribution.EvidenceText);
        Assert.Contains(snapshot.UsageAttribution.Sessions, row => row.InputTokens > 0 && row.OutputTokens > 0 && row.CachedInputTokens > 0 && row.ReasoningTokens > 0);
        Assert.NotEmpty(snapshot.ResetCredits);
        Assert.Contains(snapshot.ResetCredits, credit => credit.ExpiresAtUtc <= snapshotNow.AddDays(3));
        Assert.NotNull(snapshot.RecentActiveThread);
        Assert.True(snapshot.RecentActiveThread.ContextUsedPercent >= 80);
        Assert.True(snapshot.LifetimeTokens > 0);
        Assert.True(snapshot.PeakDailyTokens > 0);
        Assert.True(snapshot.LongestRunningTurnSec > 0);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.StatusMessage));
    }
}
