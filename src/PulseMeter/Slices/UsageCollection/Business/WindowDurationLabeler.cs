namespace PulseMeter.Slices.UsageCollection.Business;

public static class WindowDurationLabeler
{
    public static string LabelFor(int? windowDurationMins, string? limitId = null, string? limitName = null)
    {
        if (!string.IsNullOrWhiteSpace(limitName))
        {
            return limitName;
        }

        if (windowDurationMins is int minutes and > 0)
        {
            if (minutes % 1440 == 0)
            {
                return $"{minutes / 1440}d";
            }

            if (minutes % 60 == 0)
            {
                return $"{minutes / 60}h";
            }

            return $"{minutes}m";
        }

        return string.IsNullOrWhiteSpace(limitId) ? "Usage" : limitId;
    }
}
