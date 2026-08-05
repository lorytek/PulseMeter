using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Composition;
using PulseMeter.Bootstrap.Startup;
using PulseMeter.Slices.UsageCollection.Business;

namespace PulseMeter.Tests;

public sealed class PulseMeterApplicationTests
{
    [Fact]
    public void ShutdownApi_IsAsyncOnly()
    {
        var applicationType = typeof(PulseMeterApplication);

        Assert.False(typeof(IDisposable).IsAssignableFrom(applicationType));
        Assert.Null(applicationType.GetMethod(nameof(PulseMeterApplication.StopAsync).Replace("Async", string.Empty), Type.EmptyTypes));
        Assert.Null(applicationType.GetMethod(nameof(IDisposable.Dispose), Type.EmptyTypes));
    }

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
    public async Task StopAsync_ConcurrentCallersShareOneShutdown()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: false);
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.CompletedTask);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);
        await application.StartAsync();

        var stopCalls = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(application.StopAsync))
            .ToArray();
        await asyncOnlySingleton.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(stopCalls, stopCall => Assert.False(stopCall.IsCompleted));
        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);

        asyncOnlySingleton.ReleaseDisposal();
        await Task.WhenAll(stopCalls).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, lifecycleCoordinator.StopCount);
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

    [Fact]
    public async Task PrepareForProcessExit_StopsCriticalWindowResourcesBeforeAsyncDisposalCompletes()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: false);
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.CompletedTask);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);
        await application.StartAsync();

        application.PrepareForProcessExit();
        var stopTask = application.StopAsync();
        await asyncOnlySingleton.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.False(stopTask.IsCompleted);

        asyncOnlySingleton.ReleaseDisposal();
        await stopTask;
        Assert.Equal(1, lifecycleCoordinator.StopCount);
    }

    [Fact]
    public async Task CompletePendingStopForExit_KeepsProcessAliveUntilAsyncDisposalFinishes()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: false);
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.CompletedTask);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);
        await application.StartAsync();

        var exitCleanup = Task.Run(() => App.CompletePendingStopForExit(application));
        await asyncOnlySingleton.DisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(exitCleanup.IsCompleted);
        Assert.Equal(1, lifecycleCoordinator.StopCount);

        asyncOnlySingleton.ReleaseDisposal();
        await exitCleanup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, asyncOnlySingleton.DisposeCount);
    }

    [Fact]
    public async Task TryStartAsync_ContainsStartupFailureForTheWpfStartupBoundary()
    {
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: true);
        var startupException = new InvalidOperationException("Startup failed.");
        var lifecycleCoordinator = new TestLifecycleCoordinator(Task.FromException(startupException));
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        var failure = await App.TryStartAsync(application, CancellationToken.None);

        Assert.Same(startupException, failure);
        Assert.Equal(1, lifecycleCoordinator.StopCount);
        Assert.Equal(1, asyncOnlySingleton.DisposeCount);
    }

    [Fact]
    public async Task TryShowMainWindow_ActivatesOnlyAfterStartupCompletes()
    {
        var startCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asyncOnlySingleton = new AsyncOnlySingleton(completeOnDispose: true);
        var lifecycleCoordinator = new TestLifecycleCoordinator(startCompletion.Task);
        await using var application = CreateApplication(asyncOnlySingleton, lifecycleCoordinator);

        var startTask = application.StartAsync();
        await lifecycleCoordinator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(application.TryShowMainWindow());
        Assert.Equal(0, lifecycleCoordinator.ShowAndActivateCount);

        startCompletion.SetResult();
        await startTask;

        Assert.True(application.TryShowMainWindow());
        Assert.Equal(1, lifecycleCoordinator.ShowAndActivateCount);
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

        public int ShowAndActivateCount { get; private set; }

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

        public void ShowAndActivate()
        {
            ShowAndActivateCount++;
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
