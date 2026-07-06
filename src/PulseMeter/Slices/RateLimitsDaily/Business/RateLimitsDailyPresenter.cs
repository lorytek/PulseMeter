using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.RateLimitsDaily.Business;

public interface IRateLimitsDailyPresenter
{
    IReadOnlyList<DailyRateLimitDisplayRow> BuildRows(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now);

    string BuildSummaryText(bool hasRows);

    string BuildWarningText(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now);
}

public sealed class RateLimitsDailyPresenter : IRateLimitsDailyPresenter
{
    public IReadOnlyList<DailyRateLimitDisplayRow> BuildRows(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        return RateLimitsDailyDisplayBuilder.BuildRows(selectedBuckets, now);
    }

    public string BuildSummaryText(bool hasRows)
    {
        return hasRows
            ? "Daily allowance to stay within your weekly limit."
            : "Weekly usage unavailable for this track.";
    }

    public string BuildWarningText(IEnumerable<RateLimitBucket> selectedBuckets, DateTimeOffset now)
    {
        return RateLimitsDailyDisplayBuilder.BuildWarningText(selectedBuckets, now);
    }
}
