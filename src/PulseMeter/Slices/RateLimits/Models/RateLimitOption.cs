namespace PulseMeter.Slices.RateLimits.Models;

public sealed record RateLimitOption(string Key, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
