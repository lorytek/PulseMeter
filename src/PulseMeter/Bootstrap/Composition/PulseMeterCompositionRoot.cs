using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Bootstrap.Composition;

internal static class PulseMeterCompositionRoot
{
    public static ServiceProvider BuildServiceProvider(Action shutdown)
    {
        var services = new ServiceCollection();

        services.AddUsageCollection();
        services.AddPulseMeterSlices();
        services.AddPulseMeterPlatform();
        services.AddPulseMeterWindow(shutdown);

        return services.BuildServiceProvider();
    }
}
