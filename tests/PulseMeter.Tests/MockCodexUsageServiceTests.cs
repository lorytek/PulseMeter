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
        Assert.True(snapshot.Buckets.Count >= 2);
        Assert.All(snapshot.Buckets, bucket => Assert.False(string.IsNullOrWhiteSpace(bucket.Label)));
        Assert.Equal(3, snapshot.ResetCreditsAvailable);
        Assert.Null(snapshot.ResetCreditsExpiresAtUtc);
    }
}
