using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Bootstrap.Composition;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Bootstrap.Startup;

public sealed class PulseMeterApplication : IDisposable
{
    private readonly Action _shutdown;
    private ServiceProvider? _serviceProvider;
    private IPulseMeterWindowLifecycleCoordinator? _lifecycleCoordinator;
    private bool _stopped;

    public PulseMeterApplication(Action shutdown)
    {
        _shutdown = shutdown;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _serviceProvider = PulseMeterCompositionRoot.BuildServiceProvider(_shutdown);
        _lifecycleCoordinator = _serviceProvider.GetRequiredService<IPulseMeterWindowLifecycleCoordinator>();

        await _lifecycleCoordinator.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _lifecycleCoordinator?.Stop();
        _serviceProvider?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}
