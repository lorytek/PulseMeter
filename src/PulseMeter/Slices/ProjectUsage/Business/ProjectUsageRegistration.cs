using Microsoft.Extensions.DependencyInjection;

namespace PulseMeter.Slices.ProjectUsage.Business;

internal static class ProjectUsageRegistration
{
    internal static IServiceCollection AddProjectUsageSlice(this IServiceCollection services)
    {
        services.AddSingleton<IProjectUsagePresenter, ProjectUsagePresenter>();
        services.AddSingleton<ProjectUsageSectionViewModel>();

        return services;
    }
}
