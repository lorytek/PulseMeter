using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Composition;
using PulseMeter.Bootstrap.Startup;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

public sealed class PulseMeterApplicationTests
{
    [Fact]
    public async Task CompositionRoot_ResolvesLiveUsageService()
    {
        await using var provider = PulseMeterCompositionRoot.BuildServiceProvider(() => { });

        var usageService = provider.GetRequiredService<IUsageService>();

        Assert.IsType<CodexUsageService>(usageService);
    }

    [Fact]
    public async Task StopAsync_WaitsForAsyncOnlySingletonDisposalAndDisposesOnce()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: false);
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.CompletedTask);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        await application.StartAsync();
        await application.StartAsync();
        Assert.Equal(1, lifecycleCoordinator.StartCount);

        var stopTask = application.StopAsync();
        await asyncOnlySingleton.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(stopTask.IsCompleted);
        Assert.Same(stopTask, application.StopAsync());
        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);

        asyncOnlySingleton.ReleaseDisposal();
        await stopTask;

        Assert.Equal(1, asyncOnlySingleton.DisposeCount);
    }

    [Fact]
    public async Task StopAsync_DuringStartup_WaitsForStartupBeforeDisposing()
    {
        var startCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: true);
        var lifecycleCoordinator = new TestLifecycleCoordinator(startCompletion.Task);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        var startTask = application.StartAsync();
        await lifecycleCoordinator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stopTask = application.StopAsync();

        Assert.False(stopTask.IsCompleted);
        Assert.Equal(0, lifecycleCoordinator.StopCount);
        Assert.Equal(0, asyncOnlySingleton.DisposeCount);

        startCompletion.SetResult();
        await Task.WhenAll(startTask, stopTask);

        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);
    }

    [Fact]
    public async Task StopBeforeStart_PreventsLaterStartup()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: true);
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.CompletedTask);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        await application.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => application.StartAsync());
        Assert.Equal(0, lifecycleCoordinator.StartCount);
        Assert.Equal(0, asyncOnlySingleton.DisposeCount);
    }

    [Fact]
    public async Task StartAsyncFailure_DisposesProviderOnceAndPreservesStartupException()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: true);
        var startupException = new InvalidOperationException("Startup failed.");
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.FromException(startupException));
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => application.StartAsync());

        Assert.Same(startupException, exception);
        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);

        await application.StopAsync();
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);
    }

    private static PulseMeterApplication CreateApplication(
        AsyncOnlySingleton asyncOnlySingleton,
        TestLifecycleCoordinator lifecycleCoordinator)
    {
        return new PulseMeterApplication(
            () => { },
            unusedShutdown =>
            {
                _ = unusedShutdown;
                var services = new ServiceCollection();
                services.AddSingleton<AsyncOnlySingleton>(_ => asyncOnlySingleton);
                services.AddSingleton<IPulseMeterWindowLifecycleCoordinator>(serviceProvider =>
                {
                    _ = serviceProvider.GetRequiredService<AsyncOnlySingleton>();
                    return lifecycleCoordinator;
                });
                return services.BuildServiceProvider();
            });
    }

    private sealed class TestLifecycleCoordinator : IPulseMeterWindowLifecycleCoordinator
    {
        private readonly Task _startTask;

        public TestLifecycleCoordinator(Task startTask)
        {
            _startTask = startTask;
        }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            Started.TrySetResult();
            return _startTask;
        }

        public void Stop()
        {
            StopCount++;
        }
    }

    private sealed class AsyncOnlySingleton : IAsyncDisposable
    {
        private readonly bool _completeOnDispose;

        public AsyncOnlySingleton(bool completeOnDispose)
        {
            _completeOnDispose = completeOnDispose;
        }

        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            DisposalStarted.TrySetResult();

            return _completeOnDispose
                ? ValueTask.CompletedTask
                : new ValueTask(DisposalCompletion.Task);
        }

        public void ReleaseDisposal()
        {
            DisposalCompletion.TrySetResult();
        }

        private TaskCompletionSource DisposalCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
