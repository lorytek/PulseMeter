using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.DailyUsage.Business;

public interface IDailyUsagePresenter
{
    DailyUsageDisplayResult BuildRows(IReadOnlyList<DailyUsageBucket> buckets, DateOnly today);

    string FormatMedianSummaryText(DailyUsageMedianBaseline? baseline);
}

public sealed class DailyUsagePresenter : IDailyUsagePresenter
{
    public DailyUsageDisplayResult BuildRows(IReadOnlyList<DailyUsageBucket> buckets, DateOnly today)
    {
        return DailyUsageDisplayBuilder.BuildRows(buckets, today);
    }

    public string FormatMedianSummaryText(DailyUsageMedianBaseline? baseline)
    {
        return DailyUsageDisplayBuilder.FormatMedianSummaryText(baseline);
    }
}
