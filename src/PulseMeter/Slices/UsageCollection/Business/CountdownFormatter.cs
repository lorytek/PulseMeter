namespace PulseMeter.Slices.UsageCollection.Business;

public static class CountdownFormatter
{
    public static string FormatResetCountdown(long? resetsAtUnixSeconds, DateTimeOffset? now = null)
    {
        if (resetsAtUnixSeconds is null)
        {
            return "reset unknown";
        }

        var current = now ?? DateTimeOffset.UtcNow;
        var resetAt = DateTimeOffset.FromUnixTimeSeconds(resetsAtUnixSeconds.Value);
        var remaining = resetAt - current;

        if (remaining <= TimeSpan.Zero)
        {
            return "now";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes:00}m";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m";
    }
}
