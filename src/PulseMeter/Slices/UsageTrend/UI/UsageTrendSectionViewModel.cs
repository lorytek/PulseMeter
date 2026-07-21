using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Shared.Commands;

namespace PulseMeter.Slices.UsageTrend.UI;

public sealed class UsageTrendSectionViewModel : INotifyPropertyChanged
{
    private readonly IUsageTrendPresenter _presenter;
    private readonly Dictionary<string, UsageTrendForecastReference> _referenceForecasts = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LimitUsageTrend> _trends = [];
    private IReadOnlyList<LimitRunwayForecast> _forecasts = [];
    private string? _selectedLimitKey;
    private UsageTrendWindowOption? _selectedWindow;
    private UsageTrendChartModel? _chartModel;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private bool _showProjection = true;
    private bool _showRange = true;

    public UsageTrendSectionViewModel(IUsageTrendPresenter presenter)
    {
        _presenter = presenter;
        ResetChartCommand = new RelayCommand(_ => ResetChart());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UsageTrendWindowOption> WindowOptions { get; } = new();

    public RelayCommand ResetChartCommand { get; }

    public UsageTrendWindowOption? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (Equals(_selectedWindow, value))
            {
                return;
            }

            _selectedWindow = value;
            OnPropertyChanged();
            RefreshChart();
        }
    }

    public UsageTrendChartModel? ChartModel
    {
        get => _chartModel;
        private set
        {
            if (Equals(_chartModel, value))
            {
                return;
            }

            _chartModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChart));
            OnPropertyChanged(nameof(HasForecastRange));
            OnPropertyChanged(nameof(AccessibleSummary));
            OnPropertyChanged(nameof(RunwayHeadline));
            OnPropertyChanged(nameof(ForecastLeadText));
            OnPropertyChanged(nameof(ForecastWhenText));
            OnPropertyChanged(nameof(ConfidenceText));
            OnPropertyChanged(nameof(UsedPercentText));
            OnPropertyChanged(nameof(MomentumValueText));
            OnPropertyChanged(nameof(MomentumStateText));
            OnPropertyChanged(nameof(MomentumBaselineText));
            OnPropertyChanged(nameof(MomentumGaugeValue));
            OnPropertyChanged(nameof(CurrentPaceText));
            OnPropertyChanged(nameof(SustainablePaceText));
            OnPropertyChanged(nameof(PaceComparisonText));
            OnPropertyChanged(nameof(PaceComparisonLabel));
            OnPropertyChanged(nameof(RecommendationText));
            OnPropertyChanged(nameof(CanOpenPacingPlan));
        }
    }

    public bool ShowProjection
    {
        get => _showProjection;
        set
        {
            if (_showProjection == value)
            {
                return;
            }

            _showProjection = value;
            OnPropertyChanged();
            RefreshChart();
        }
    }

    public bool ShowRange
    {
        get => _showRange;
        set
        {
            if (_showRange == value)
            {
                return;
            }

            _showRange = value;
            OnPropertyChanged();
            RefreshChart();
        }
    }

    public bool HasChart => ChartModel is not null;

    public bool HasForecastRange => ChartModel?.TypicalRange.Count > 0;

    public string EmptyStateText => "Coding runway will appear after live quota samples arrive.";

    public string AccessibleSummary => ChartModel?.AccessibleSummary ?? EmptyStateText;

    public string RunwayHeadline => ChartModel?.Summary.Headline ?? "Runway is still learning";

    public string ForecastLeadText => ChartModel?.Summary.ForecastLeadText ?? EmptyStateText;

    public string ForecastWhenText => ChartModel?.Summary.ForecastWhenText ?? string.Empty;

    public string ConfidenceText => ChartModel?.Summary.ConfidenceText ?? "Collecting live samples";

    public string UsedPercentText => ChartModel?.Summary.UsedPercentText ?? "—";

    public string MomentumValueText => ChartModel?.Summary.Momentum.ValueText ?? "—";

    public string MomentumStateText => ChartModel?.Summary.Momentum.StateText ?? "learning baseline";

    public string MomentumBaselineText => ChartModel?.Summary.Momentum.BaselineText ?? "vs window median";

    public double MomentumGaugeValue => ChartModel?.Summary.Momentum.GaugeValue ?? 0;

    public string CurrentPaceText => ChartModel?.Summary.CurrentPaceText ?? "—";

    public string SustainablePaceText => ChartModel?.Summary.SustainablePaceText ?? "—";

    public string PaceComparisonText => ChartModel?.Summary.PaceComparisonText ?? "—";

    public string PaceComparisonLabel => ChartModel?.Summary.PaceComparisonLabel ?? "pace comparison";

    public string RecommendationText => ChartModel?.Summary.RecommendationText ?? "Keep coding to build a reliable pace estimate";

    public bool CanOpenPacingPlan => ChartModel?.Summary.CanOpenPacingPlan ?? false;

    public void ApplySignals(UsageSignalsSnapshot signals, string? selectedLimitKey, DateTimeOffset now)
    {
        _trends = signals.UsageTrends;
        _forecasts = signals.RunwayForecasts;
        _selectedLimitKey = selectedLimitKey;
        _now = now;
        PruneForecastReferences();
        RebuildWindowOptions();
    }

    public void SelectLimit(string? selectedLimitKey, DateTimeOffset now)
    {
        if (string.Equals(_selectedLimitKey, selectedLimitKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedLimitKey = selectedLimitKey;
        _now = now;
        RebuildWindowOptions();
    }

    public void Refresh(DateTimeOffset now)
    {
        _now = now;
        if (_selectedWindow is not null
            && _trends.Any(trend => trend.BucketId.Equals(_selectedWindow.BucketId, StringComparison.OrdinalIgnoreCase)
                && trend.ResetsAtUtc > now))
        {
            RefreshChart();
            return;
        }

        RebuildWindowOptions();
    }

    private void RebuildWindowOptions()
    {
        var selectedBucketId = _selectedWindow?.BucketId;
        var matching = _trends
            .Where(trend => string.IsNullOrWhiteSpace(_selectedLimitKey)
                || trend.LimitKey.Equals(_selectedLimitKey, StringComparison.OrdinalIgnoreCase))
            .Where(trend => trend.ResetsAtUtc > _now)
            .OrderBy(trend => trend.WindowDurationMins ?? int.MaxValue)
            .ThenBy(trend => trend.WindowLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        WindowOptions.Clear();
        foreach (var trend in matching)
        {
            WindowOptions.Add(new UsageTrendWindowOption(
                trend.BucketId,
                FormatWindowLabel(trend.WindowLabel, trend.WindowDurationMins),
                trend.WindowDurationMins));
        }

        _selectedWindow = WindowOptions.FirstOrDefault(option => option.BucketId.Equals(selectedBucketId, StringComparison.OrdinalIgnoreCase))
            ?? WindowOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedWindow));
        OnPropertyChanged(nameof(EmptyStateText));
        RefreshChart();
    }

    private void RefreshChart()
    {
        if (_selectedWindow is null)
        {
            ChartModel = null;
            return;
        }

        var trend = _trends.FirstOrDefault(candidate => candidate.BucketId.Equals(_selectedWindow.BucketId, StringComparison.OrdinalIgnoreCase));
        if (trend is null || trend.ResetsAtUtc <= _now)
        {
            ChartModel = null;
            return;
        }

        var forecast = _forecasts.FirstOrDefault(candidate => candidate.BucketId.Equals(trend.BucketId, StringComparison.OrdinalIgnoreCase));
        _referenceForecasts.TryGetValue(trend.BucketId, out var referenceForecast);
        if (referenceForecast?.ResetAt != trend.ResetsAtUtc)
        {
            _referenceForecasts.Remove(trend.BucketId);
            referenceForecast = null;
        }

        var chart = _presenter.BuildChart(trend, forecast, _now, ShowProjection, ShowRange, referenceForecast);
        if (chart is not null
            && referenceForecast is null
            && chart.ProjectedPoints.Count > 1
            && chart.ActualPoints.Count > 0)
        {
            var capturedAt = chart.ActualPoints[^1].Timestamp;
            var referencePoints = chart.ProjectedPoints
                .Where(point => point.Timestamp >= capturedAt && point.Timestamp <= chart.ResetAt)
                .ToArray();
            if (referencePoints.Length > 1)
            {
                _referenceForecasts[trend.BucketId] = new UsageTrendForecastReference(
                    capturedAt,
                    chart.ResetAt,
                    referencePoints);
            }
        }

        ChartModel = chart;
    }

    private void PruneForecastReferences()
    {
        foreach (var bucketId in _referenceForecasts.Keys.ToArray())
        {
            var activeTrend = _trends.FirstOrDefault(trend =>
                trend.BucketId.Equals(bucketId, StringComparison.OrdinalIgnoreCase));
            if (activeTrend is null
                || activeTrend.ResetsAtUtc <= _now
                || activeTrend.ResetsAtUtc != _referenceForecasts[bucketId].ResetAt)
            {
                _referenceForecasts.Remove(bucketId);
            }
        }
    }

    private void ResetChart()
    {
        _showProjection = true;
        _showRange = true;
        OnPropertyChanged(nameof(ShowProjection));
        OnPropertyChanged(nameof(ShowRange));
        RefreshChart();
    }

    private static string FormatWindowLabel(string label, int? windowDurationMins)
    {
        if (windowDurationMins is int minutes && minutes > 0)
        {
            if (minutes % 10_080 == 0)
            {
                var weeks = minutes / 10_080;
                return weeks == 1 ? "7-day limit" : $"{weeks}-week limit";
            }

            if (minutes % 1_440 == 0)
            {
                var days = minutes / 1_440;
                return $"{days}-day limit";
            }

            if (minutes % 60 == 0)
            {
                var hours = minutes / 60;
                return $"{hours}-hour limit";
            }
        }

        var trimmed = string.IsNullOrWhiteSpace(label) ? "Usage" : label.Trim();
        return trimmed.Contains("limit", StringComparison.OrdinalIgnoreCase) ? trimmed : $"{trimmed} limit";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record UsageTrendWindowOption(string BucketId, string Label, int? WindowDurationMins)
{
    public override string ToString() => Label;
}
