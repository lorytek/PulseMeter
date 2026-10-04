namespace PulseMeter.Slices.UsageCollection.Models;

/// <summary>
/// The coding assistant whose usage limits PulseMeter monitors.
/// </summary>
public enum UsageProvider
{
    Codex,
    Claude
}

public static class UsageProviderNames
{
    public static string DisplayName(UsageProvider provider) => provider switch
    {
        UsageProvider.Claude => "Claude Code",
        _ => "Codex"
    };

    public static UsageProvider Parse(string? value) =>
        Enum.TryParse<UsageProvider>(value, ignoreCase: true, out var provider)
        && Enum.IsDefined(provider)
            ? provider
            : UsageProvider.Codex;
}
