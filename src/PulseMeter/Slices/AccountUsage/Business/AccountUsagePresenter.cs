using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.AccountUsage.Business;

public interface IAccountUsagePresenter
{
    AccountUsageFreshnessState EvaluateFreshness(
        UsageSnapshot currentSnapshot,
        UsageSnapshot nextSnapshot,
        DateOnly today,
        bool useMockMode,
        bool currentDailyWarning,
        bool currentSummaryWarning);

    string SummaryText(UsageSnapshot snapshot);

    string DailyFreshnessWarningText(UsageSnapshot snapshot);

    string FreshnessWarningText(bool hasAccountSummary);

    bool HasAccountSummary(UsageSnapshot snapshot);

    string TodayUsageText(UsageSnapshot snapshot, DateOnly today);

    string TodayUsageMetricValueText(UsageSnapshot snapshot, DateOnly today);

    string TodayUsageValueText(UsageSnapshot snapshot, DateOnly today);

    string LifetimeUsageValueText(UsageSnapshot snapshot);

    string PeakUsageValueText(UsageSnapshot snapshot);

    string StreakDaysValueText(UsageSnapshot snapshot);

    string LifetimeUsageCaptionText(UsageSnapshot snapshot);

    string PeakUsageCaptionText(UsageSnapshot snapshot);

    string StreakCaptionText(UsageSnapshot snapshot);

    double TodayMedianDailyPercentValue(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today);

    string TodayMedianDailyPercentText(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today);

    long? GetTodayTokens(UsageSnapshot snapshot, DateOnly today);
}

public sealed class AccountUsagePresenter : IAccountUsagePresenter
{
    public AccountUsageFreshnessState EvaluateFreshness(
        UsageSnapshot currentSnapshot,
        UsageSnapshot nextSnapshot,
        DateOnly today,
        bool useMockMode,
        bool currentDailyWarning,
        bool currentSummaryWarning)
    {
        return AccountUsageFreshnessEvaluator.Evaluate(
            currentSnapshot,
            nextSnapshot,
            today,
            useMockMode,
            currentDailyWarning,
            currentSummaryWarning);
    }

    public string SummaryText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.SummaryText(snapshot);
    }

    public string DailyFreshnessWarningText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.DailyFreshnessWarningText(snapshot);
    }

    public string FreshnessWarningText(bool hasAccountSummary)
    {
        return AccountUsageDisplayBuilder.FreshnessWarningText(hasAccountSummary);
    }

    public bool HasAccountSummary(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.HasAccountSummary(snapshot);
    }

    public string TodayUsageText(UsageSnapshot snapshot, DateOnly today)
    {
        return AccountUsageDisplayBuilder.TodayUsageText(snapshot, today);
    }

    public string TodayUsageMetricValueText(UsageSnapshot snapshot, DateOnly today)
    {
        return AccountUsageDisplayBuilder.TodayUsageMetricValueText(snapshot, today);
    }

    public string TodayUsageValueText(UsageSnapshot snapshot, DateOnly today)
    {
        return AccountUsageDisplayBuilder.TodayUsageValueText(snapshot, today);
    }

    public string LifetimeUsageValueText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.LifetimeUsageValueText(snapshot);
    }

    public string PeakUsageValueText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.PeakUsageValueText(snapshot);
    }

    public string StreakDaysValueText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.StreakDaysValueText(snapshot);
    }

    public string LifetimeUsageCaptionText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.LifetimeUsageCaptionText(snapshot);
    }

    public string PeakUsageCaptionText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.PeakUsageCaptionText(snapshot);
    }

    public string StreakCaptionText(UsageSnapshot snapshot)
    {
        return AccountUsageDisplayBuilder.StreakCaptionText(snapshot);
    }

    public double TodayMedianDailyPercentValue(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today)
    {
        return AccountUsageDisplayBuilder.TodayMedianDailyPercentValue(snapshot, medianBaseline, today);
    }

    public string TodayMedianDailyPercentText(
        UsageSnapshot snapshot,
        DailyUsageMedianBaseline? medianBaseline,
        DateOnly today)
    {
        return AccountUsageDisplayBuilder.TodayMedianDailyPercentText(snapshot, medianBaseline, today);
    }

    public long? GetTodayTokens(UsageSnapshot snapshot, DateOnly today)
    {
        return AccountUsageDisplayBuilder.GetTodayTokens(snapshot, today);
    }
}
