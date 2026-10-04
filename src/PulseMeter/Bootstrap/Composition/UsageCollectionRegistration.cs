using Microsoft.Extensions.DependencyInjection;
using System.IO;
using PulseMeter.Platform.Codex;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Bootstrap.Composition;

internal static class UsageCollectionRegistration
{
    internal static IServiceCollection AddUsageCollection(this IServiceCollection services)
    {
        services.AddSingleton<CodexUsageService>();
        services.AddSingleton<IClaudeUsageApiClient>(_ => new ClaudeUsageApiClient());
        services.AddSingleton<IClaudeLocalUsageSource>(_ => new ClaudeLocalUsageSource());
        services.AddSingleton<ClaudeUsageService>();
        services.AddSingleton(provider => new UsageProviderRouter(
            provider.GetRequiredService<CodexUsageService>(),
            provider.GetRequiredService<ClaudeUsageService>(),
            LoadSelectedProvider(provider)));
        services.AddSingleton<IUsageService>(provider => provider.GetRequiredService<UsageProviderRouter>());
        services.AddSingleton<IUsageProviderSwitch>(provider => provider.GetRequiredService<UsageProviderRouter>());
        services.AddSingleton<ICodexResetCreditService, CodexResetCreditService>();
        services.AddSingleton<SharedRolloutAnalyticsSource>();
        services.AddSingleton<IProjectUsageService>(provider =>
            new ProjectUsageService(provider.GetRequiredService<SharedRolloutAnalyticsSource>()));
        services.AddSingleton<IUsageAttributionService>(provider =>
            new UsageAttributionService(provider.GetRequiredService<SharedRolloutAnalyticsSource>()));
        services.AddSingleton<IMockUsageService, MockCodexUsageService>();
        services.AddSingleton<IAppServerProcessFactory, AppServerProcessFactory>();
        services.AddSingleton<IJsonRpcClientFactory, JsonRpcClientFactory>();

        return services;
    }

    private static UsageProvider LoadSelectedProvider(IServiceProvider provider)
    {
        try
        {
            return UsageProviderNames.Parse(
                provider.GetService<IPulseMeterAppSettingsStore>()?.Load()?.UsageProvider);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return UsageProvider.Codex;
        }
    }
}
