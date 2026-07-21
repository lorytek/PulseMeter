using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Composition;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Bootstrap.Startup;

public sealed class PulseMeterApplication : IDisposable, IAsyncDisposable
{
    private readonly Action _shutdown;
    private readonly Func<Action, ServiceProvider> _serviceProviderFactory;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private LifecycleState _state = LifecycleState.Created;
    private ServiceProvider? _serviceProvider;
    private IPulseMeterWindowLifecycleCoordinator? _lifecycleCoordinator;
    private Task? _startTask;
    private Task? _stopTask;

    public PulseMeterApplication(Action shutdown)
        : this(shutdown, PulseMeterCompositionRoot.BuildServiceProvider)
    {
    }

    internal PulseMeterApplication(
        Action shutdown,
        Func<Action, ServiceProvider> serviceProviderFactory)
    {
        _shutdown = shutdown;
        _serviceProviderFactory = serviceProviderFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            switch (_state)
            {
                case LifecycleState.Created:
                    _state = LifecycleState.Starting;
                    _startTask = StartCoreAsync(cancellationToken);
                    return _startTask;

                case LifecycleState.Starting:
                    return _startTask!;

                case LifecycleState.Started:
                    return Task.CompletedTask;

                default:
                    return Task.FromException(new InvalidOperationException(
                        "PulseMeterApplication cannot be started after shutdown has begun."));
            }
        }
    }

    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public Task StopAsync()
    {
        lock (_stateLock)
        {
            switch (_state)
            {
                case LifecycleState.Created:
                    _state = LifecycleState.Stopped;
                    _stopTask = Task.CompletedTask;
                    return _stopTask;

                case LifecycleState.Starting:
                case LifecycleState.Started:
                    _state = LifecycleState.Stopping;
                    _stopTask ??= StopCoreAsync();
                    return _stopTask;

                case LifecycleState.Stopping:
                    return _stopTask!;

                case LifecycleState.Stopped:
                    return _stopTask ?? Task.CompletedTask;

                default:
                    throw new InvalidOperationException("Unknown PulseMeterApplication lifecycle state.");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _serviceProvider = _serviceProviderFactory(_shutdown);
                _lifecycleCoordinator = _serviceProvider.GetRequiredService<IPulseMeterWindowLifecycleCoordinator>();

                await _lifecycleCoordinator.StartAsync(cancellationToken).ConfigureAwait(false);

                lock (_stateLock)
                {
                    if (_state == LifecycleState.Starting)
                    {
                        _state = LifecycleState.Started;
                    }
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (Exception startupException)
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(startupException, cleanupException);
            }

            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                _lifecycleCoordinator?.Stop();
            }
            finally
            {
                if (_serviceProvider is not null)
                {
                    await _serviceProvider.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();

            lock (_stateLock)
            {
                _state = LifecycleState.Stopped;
            }
        }
    }

    private enum LifecycleState
    {
        Created,
        Starting,
        Started,
        Stopping,
        Stopped
    }
}
