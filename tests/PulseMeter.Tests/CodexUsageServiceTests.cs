using PulseMeter.Platform.Codex;
using System.Reflection;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class CodexUsageServiceTests
{
    [Fact]
    public void ShouldFallbackFromRefreshException_ReturnsTrueForInternalTimeout()
    {
        Assert.True(CodexUsageService.ShouldFallbackFromRefreshException(new TaskCanceledException(), CancellationToken.None));
    }

    [Fact]
    public void ShouldFallbackFromRefreshException_ReturnsFalseWhenCallerCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(CodexUsageService.ShouldFallbackFromRefreshException(new TaskCanceledException(), cts.Token));
    }

    [Fact]
    public async Task FallbackWithoutLastGoodLiveSnapshot_ShowsUnavailableInsteadOfMockData()
    {
        await using var service = CreateService();
        var fallback = await InvokeFallbackAsync(service, new InvalidOperationException("Codex is closed"));

        Assert.Equal(SyncStatus.Unavailable, fallback.SyncStatus);
        Assert.Equal("AppServer", fallback.Source);
        Assert.Empty(fallback.Buckets);
        Assert.Empty(fallback.DailyBuckets);
        Assert.Null(fallback.ResetCreditsAvailable);
        Assert.Contains("The monitored app is not running", fallback.StatusMessage);
        Assert.DoesNotContain("mock", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<UsageSnapshot> InvokeFallbackAsync(CodexUsageService service, Exception exception)
    {
        var method = typeof(CodexUsageService).GetMethod(
            "BuildFallbackSnapshotAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<UsageSnapshot>?)method.Invoke(service, [exception, CancellationToken.None]);
        Assert.NotNull(task);

        return await task;
    }

    private static CodexUsageService CreateService()
    {
        return new CodexUsageService(
            new StubMockUsageService(),
            new StubResetCreditService(),
            new StubProjectUsageService(),
            new StubProcessFactory(),
            new StubJsonRpcClientFactory());
    }

    private sealed class StubMockUsageService : IMockUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new UsageSnapshot { SyncStatus = SyncStatus.Mocked, Source = "Mock" };
            SnapshotUpdated?.Invoke(this, snapshot);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubResetCreditService : ICodexResetCreditService
    {
        public Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ResetCreditFetchResult?>(null);
        }
    }

    private sealed class StubProjectUsageService : IProjectUsageService
    {
        public Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProjectUsageRow>>(Array.Empty<ProjectUsageRow>());
        }
    }

    private sealed class StubProcessFactory : IAppServerProcessFactory
    {
        public IAppServerProcess Start(string? executable = null)
        {
            throw new InvalidOperationException("Not used by fallback test.");
        }
    }

    private sealed class StubJsonRpcClientFactory : IJsonRpcClientFactory
    {
        public IJsonRpcClient Create(StreamReader reader, StreamWriter writer)
        {
            throw new InvalidOperationException("Not used by fallback test.");
        }
    }
}
