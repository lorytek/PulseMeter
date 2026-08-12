using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Slices.ReturnNote.UI;

namespace PulseMeter.Slices.ReturnNote.Business;

internal static class ReturnNoteRegistration
{
    internal static IServiceCollection AddReturnNoteSlice(this IServiceCollection services)
    {
        services.AddSingleton<ReturnNoteSectionViewModel>();
        return services;
    }
}
