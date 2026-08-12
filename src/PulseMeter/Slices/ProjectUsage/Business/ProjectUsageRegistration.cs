using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Platform.Windows;

namespace PulseMeter.Slices.ProjectUsage.Business;

internal static class ProjectUsageRegistration
{
    internal static IServiceCollection AddProjectUsageSlice(this IServiceCollection services)
    {
        services.AddSingleton<IProjectUsagePresenter, ProjectUsagePresenter>();
        services.AddSingleton<IProjectLocationPresenter>(sp => new ProjectLocationPresenter(
            sp.GetRequiredService<IProjectLocationActionService>(),
            sp.GetRequiredService<IProjectFolderPicker>(),
            () => sp.GetService<IPulseMeterWindow>() as System.Windows.Window));
        services.AddSingleton<ProjectUsageSectionViewModel>();

        return services;
    }
}
