using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.AccountUsage.UI;

public sealed class AccountUsageSectionViewModel : INotifyPropertyChanged
{
    private readonly IAccountUsagePresenter _presenter;
    private UsageSnapshot _snapshot = new();
    private DailyUsageMedianBaseline? _medianBaseline;
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    private bool _hasDailyUsageFreshnessWarning;
    private bool _hasAccountSummaryFreshnessWarning;

    public AccountUsageSectionViewModel(IAccountUsagePresenter presenter)
    {
        _presenter = presenter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AccountUsageSummaryText => _presenter.SummaryText(_snapshot);

    public string TodayUsageText => _presenter.TodayUsageText(_snapshot, _today);

    public string TodayUsageMetricValueText => _presenter.TodayUsageMetricValueText(_snapshot, _today);

    public bool HasDailyUsageFreshnessWarning => _hasDailyUsageFreshnessWarning;

    public string DailyUsageFreshnessWarningText => _presenter.DailyFreshnessWarningText(_snapshot);

    public bool HasAccountSummaryFreshnessWarning => _hasAccountSummaryFreshnessWarning;

    public string AccountSummaryFreshnessWarningText =>
        _presenter.FreshnessWarningText(_presenter.HasAccountSummary(_snapshot));

    public string LifetimeUsageValueText => _presenter.LifetimeUsageValueText(_snapshot);

    public string PeakUsageValueText => _presenter.PeakUsageValueText(_snapshot);

    public string StreakDaysValueText => _presenter.StreakDaysValueText(_snapshot);

    public string LifetimeUsageCaptionText => _presenter.LifetimeUsageCaptionText(_snapshot);

    public string PeakUsageCaptionText => _presenter.PeakUsageCaptionText(_snapshot);

    public string StreakCaptionText => _presenter.StreakCaptionText(_snapshot);

    public double TodayMedianDailyPercentValue =>
        _presenter.TodayMedianDailyPercentValue(_snapshot, _medianBaseline, _today);

    public string TodayMedianDailyPercentText =>
        _presenter.TodayMedianDailyPercentText(_snapshot, _medianBaseline, _today);

    public double TodayPeakPercentValue => TodayMedianDailyPercentValue;

    public string TodayPeakPercentText => TodayMedianDailyPercentText;

    public string TodayUsageValueText => _presenter.TodayUsageValueText(_snapshot, _today);

    public void EvaluateFreshness(
        UsageSnapshot currentSnapshot,
        UsageSnapshot nextSnapshot,
        DateOnly today,
        bool useMockMode)
    {
        var state = _presenter.EvaluateFreshness(
            currentSnapshot,
            nextSnapshot,
            today,
            useMockMode,
            _hasDailyUsageFreshnessWarning,
            _hasAccountSummaryFreshnessWarning);

        if (SetField(
            ref _hasDailyUsageFreshnessWarning,
            state.HasDailyUsageFreshnessWarning,
            nameof(HasDailyUsageFreshnessWarning)))
        {
            OnPropertyChanged(nameof(DailyUsageFreshnessWarningText));
        }

        if (SetField(
            ref _hasAccountSummaryFreshnessWarning,
            state.HasAccountSummaryFreshnessWarning,
            nameof(HasAccountSummaryFreshnessWarning)))
        {
            OnPropertyChanged(nameof(AccountSummaryFreshnessWarningText));
        }
    }

    public void ApplySnapshot(UsageSnapshot snapshot, DailyUsageMedianBaseline? medianBaseline, DateOnly today)
    {
        _snapshot = snapshot;
        _medianBaseline = medianBaseline;
        _today = today;

        RefreshDisplayProperties();
    }

    public long? GetTodayTokens()
    {
        return _presenter.GetTodayTokens(_snapshot, _today);
    }

    public void RefreshDisplayProperties()
    {
        OnPropertyChanged(nameof(AccountUsageSummaryText));
        OnPropertyChanged(nameof(AccountSummaryFreshnessWarningText));
        OnPropertyChanged(nameof(HasAccountSummaryFreshnessWarning));
        OnPropertyChanged(nameof(DailyUsageFreshnessWarningText));
        OnPropertyChanged(nameof(HasDailyUsageFreshnessWarning));
        OnPropertyChanged(nameof(LifetimeUsageCaptionText));
        OnPropertyChanged(nameof(LifetimeUsageValueText));
        OnPropertyChanged(nameof(PeakUsageCaptionText));
        OnPropertyChanged(nameof(PeakUsageValueText));
        OnPropertyChanged(nameof(StreakCaptionText));
        OnPropertyChanged(nameof(StreakDaysValueText));
        OnPropertyChanged(nameof(TodayMedianDailyPercentText));
        OnPropertyChanged(nameof(TodayMedianDailyPercentValue));
        OnPropertyChanged(nameof(TodayPeakPercentText));
        OnPropertyChanged(nameof(TodayPeakPercentValue));
        OnPropertyChanged(nameof(TodayUsageMetricValueText));
        OnPropertyChanged(nameof(TodayUsageText));
        OnPropertyChanged(nameof(TodayUsageValueText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
