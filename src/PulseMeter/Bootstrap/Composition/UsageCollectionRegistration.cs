using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Platform.Codex;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Bootstrap.Composition;

internal static class UsageCollectionRegistration
{
    internal static IServiceCollection AddUsageCollection(this IServiceCollection services)
    {
        services.AddSingleton<IUsageService, CodexUsageService>();
        services.AddSingleton<ICodexResetCreditService, CodexResetCreditService>();
        services.AddSingleton<IProjectUsageService, ProjectUsageService>();
        services.AddSingleton<IMockUsageService, MockCodexUsageService>();
        services.AddSingleton<IAppServerProcessFactory, AppServerProcessFactory>();
        services.AddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>();

        return services;
    }
}
