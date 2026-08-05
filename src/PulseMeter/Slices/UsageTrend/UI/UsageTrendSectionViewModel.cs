using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Persistence;
using PulseMeter.Shared.Commands;

using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Slices.UsageTrend.UI;

public sealed class UsageTrendSectionViewModel : INotifyPropertyChanged
{
    private readonly IUsageTrendPresenter _presenter;
    private readonly Dictionary<string, UsageTrendForecastReference> _referenceForecasts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _selectedBlockDurations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RecoveryWatchSettings> _recoveryWatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _observedRecoveryWatchScopes = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LimitUsageTrend> _trends = [];
    private IReadOnlyList<LimitRunwayForecast> _forecasts = [];
    private string? _selectedLimitKey;
    private UsageTrendWindowOption? _selectedWindow;
    private UsageTrendChartModel? _chartModel;
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    private bool _showProjection = true;
    private bool _showRange = true;
    private bool _isRebuildingWindowOptions;
    private string? _recoveryConfirmationText;

    public UsageTrendSectionViewModel(IUsageTrendPresenter presenter)
    {
        _presenter = presenter;
        ResetChartCommand = new RelayCommand(_ => ResetChart());
        SelectBlockDurationCommand = new RelayCommand(SelectBlockDuration);
        ToggleRecoveryWatchCommand = new RelayCommand(_ => ToggleRecoveryWatch());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RecoveryWatchesChanged;

    public event EventHandler<UsageTrendRecoveryCompletedEventArgs>? RecoveryWatchCompleted;

    public ObservableCollection<UsageTrendWindowOption> WindowOptions { get; } = new();

    public RelayCommand ResetChartCommand { get; }

    public RelayCommand SelectBlockDurationCommand { get; }

    public RelayCommand ToggleRecoveryWatchCommand { get; }

    public UsageTrendWindowOption? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (_isRebuildingWindowOptions)
            {
                return;
            }

            if (Equals(_selectedWindow, value))
            {
                return;
            }

            _selectedWindow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBlockDurationMinutes));
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
            OnPropertyChanged(nameof(MomentumTitleText));
            OnPropertyChanged(nameof(MomentumStateText));
            OnPropertyChanged(nameof(MomentumBaselineText));
            OnPropertyChanged(nameof(MomentumGaugeValue));
            OnPropertyChanged(nameof(IsMomentumLearning));
            OnPropertyChanged(nameof(MomentumBaselineProgress));
            OnPropertyChanged(nameof(MomentumAccessibleSummary));
            OnPropertyChanged(nameof(CurrentPaceText));
            OnPropertyChanged(nameof(SustainablePaceText));
            OnPropertyChanged(nameof(PaceComparisonText));
            OnPropertyChanged(nameof(PaceComparisonLabel));
            OnPropertyChanged(nameof(RecommendationText));
            OnPropertyChanged(nameof(CanOpenPacingPlan));
            OnPropertyChanged(nameof(HasBlockAdvisor));
            OnPropertyChanged(nameof(BlockOptions));
            OnPropertyChanged(nameof(BlockAdvisorState));
            OnPropertyChanged(nameof(BlockAdvisorDetail));
            OnPropertyChanged(nameof(BlockAdvisorAccessibleSummary));
            OnPropertyChanged(nameof(NextConstraintHeadline));
            OnPropertyChanged(nameof(NextConstraintDetail));
            OnPropertyChanged(nameof(NextConstraintAccessibleSummary));
            OnPropertyChanged(nameof(HasActiveRecoveryWatch));
            OnPropertyChanged(nameof(CanActivateRecoveryWatch));
            OnPropertyChanged(nameof(CanManageRecoveryWatch));
            OnPropertyChanged(nameof(RecoveryWatchActionText));
            OnPropertyChanged(nameof(RecoveryWatchText));
            OnPropertyChanged(nameof(RecoveryWatchAccessibleSummary));
            OnPropertyChanged(nameof(HasRecoveryConfirmation));
            OnPropertyChanged(nameof(RecoveryConfirmationText));
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

    public string MomentumTitleText => "Usage momentum";

    public string MomentumStateText => ChartModel?.Summary.Momentum.StateText ?? "learning baseline";

    public string MomentumBaselineText => ChartModel?.Summary.Momentum.BaselineText ?? "vs window median";

    public double MomentumGaugeValue => ChartModel?.Summary.Momentum.GaugeValue ?? 0;

    public bool IsMomentumLearning => ChartModel?.Summary.Momentum.IsLearning ?? true;

    public double MomentumBaselineProgress => ChartModel?.Summary.Momentum.BaselineProgress ?? 0;

    public string MomentumAccessibleSummary => ChartModel?.Summary.Momentum.AccessibleSummary
        ?? "Baseline progress: 0% ready. No samples collected.";

    public string CurrentPaceText => ChartModel?.Summary.CurrentPaceText ?? "—";

    public string SustainablePaceText => ChartModel?.Summary.SustainablePaceText ?? "—";

    public string PaceComparisonText => ChartModel?.Summary.PaceComparisonText ?? "—";

    public string PaceComparisonLabel => ChartModel?.Summary.PaceComparisonLabel ?? "pace comparison";

    public string RecommendationText => ChartModel?.Summary.RecommendationText ?? "Keep coding to build a reliable pace estimate";

    public bool CanOpenPacingPlan => ChartModel?.Summary.CanOpenPacingPlan ?? false;

    public bool HasBlockAdvisor => ChartModel?.BlockAdvisor is not null;

    public IReadOnlyList<UsageTrendBlockOption> BlockOptions => ChartModel?.BlockAdvisor?.Options ?? [];

    public string BlockAdvisorState => ChartModel?.BlockAdvisor?.State ?? "Still learning";

    public string BlockAdvisorDetail => ChartModel?.BlockAdvisor?.Detail ?? string.Empty;

    public string BlockAdvisorAccessibleSummary => ChartModel?.BlockAdvisor?.AccessibleSummary ?? string.Empty;

    public string NextConstraintHeadline => ChartModel?.NextConstraint?.Headline ?? "Next constraint · Still learning";

    public string NextConstraintDetail => ChartModel?.NextConstraint?.Detail ?? "Need at least one reliable active 5h or 7d forecast.";

    public string NextConstraintAccessibleSummary => ChartModel?.NextConstraint?.AccessibleSummary ?? NextConstraintDetail;

    public bool HasActiveRecoveryWatch => TryGetCurrentRecoveryWatch(out _);

    public bool CanActivateRecoveryWatch => !HasActiveRecoveryWatch && IsCurrentRecoveryWatchEligible();

    public bool CanManageRecoveryWatch => HasActiveRecoveryWatch || CanActivateRecoveryWatch;

    public string RecoveryWatchActionText => HasActiveRecoveryWatch ? "Stop watching this block" : "Watch for recovery";

    public string RecoveryWatchText
    {
        get
        {
            if (!TryGetCurrentRecoveryWatch(out var watch))
            {
                return CanActivateRecoveryWatch
                    ? "Watch this block and PulseMeter will alert when it likely fits sooner, or when the quota resets."
                    : "Recovery watch is available for live blocks that may be interrupted, are unlikely to fit, or are waiting for reset.";
            }

            var remaining = watch.ResetAtUtc - _now;
            return $"Watching {FormatBlockDurationLabel(watch.BlockDurationMinutes)} block · reset in {FormatCountdown(remaining)}. PulseMeter may alert sooner if pace slows enough.";
        }
    }

    public string RecoveryWatchAccessibleSummary => HasActiveRecoveryWatch
        ? RecoveryWatchText
        : $"{RecoveryWatchText} {RecoveryWatchActionText}.";

    public bool HasRecoveryConfirmation => !string.IsNullOrWhiteSpace(_recoveryConfirmationText);

    public string RecoveryConfirmationText => _recoveryConfirmationText ?? string.Empty;

    public int? SelectedBlockDurationMinutes
    {
        get => _selectedWindow is not null
            && _selectedBlockDurations.TryGetValue(_selectedWindow.BucketId, out var durationMinutes)
                ? durationMinutes
                : null;
        set
        {
            if (value is not int durationMinutes
                || _selectedWindow is null
                || SelectedBlockDurationMinutes == durationMinutes)
            {
                return;
            }

            _selectedBlockDurations[_selectedWindow.BucketId] = durationMinutes;
            OnPropertyChanged();
            RefreshChart();
        }
    }

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

    public IReadOnlyList<RecoveryWatchSettings> CaptureRecoveryWatches() => _recoveryWatches.Values.ToArray();

    public void RestoreRecoveryWatches(IEnumerable<RecoveryWatchSettings>? watches)
    {
        _recoveryWatches.Clear();
        _observedRecoveryWatchScopes.Clear();
        foreach (var watch in watches ?? [])
        {
            if (watch is null
                || string.IsNullOrWhiteSpace(watch.LimitKey)
                || watch.WindowDurationMins <= 0
                || watch.BlockDurationMinutes <= 0)
            {
                continue;
            }

            _recoveryWatches[BuildRecoveryScopeKey(watch.LimitKey, watch.WindowDurationMins)] = watch;
        }

        NotifyRecoveryWatchPropertiesChanged();
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

        var rebuiltOptions = matching
            .Select(trend => new UsageTrendWindowOption(
                trend.BucketId,
                FormatWindowLabel(trend.WindowLabel, trend.WindowDurationMins),
                trend.WindowDurationMins))
            .ToArray();
        if (!WindowOptions.SequenceEqual(rebuiltOptions))
        {
            _isRebuildingWindowOptions = true;
            try
            {
                WindowOptions.Clear();
                foreach (var option in rebuiltOptions)
                {
                    WindowOptions.Add(option);
                }
            }
            finally
            {
                _isRebuildingWindowOptions = false;
            }
        }

        _selectedWindow = WindowOptions.FirstOrDefault(option => option.BucketId.Equals(selectedBucketId, StringComparison.OrdinalIgnoreCase))
            ?? WindowOptions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedWindow));
        OnPropertyChanged(nameof(SelectedBlockDurationMinutes));
        OnPropertyChanged(nameof(EmptyStateText));
        RefreshChart();
    }

    private void RefreshChart()
    {
        if (_selectedWindow is null)
        {
            ChartModel = null;
            EvaluateRecoveryWatches();
            return;
        }

        var trend = _trends.FirstOrDefault(candidate => candidate.BucketId.Equals(_selectedWindow.BucketId, StringComparison.OrdinalIgnoreCase));
        if (trend is null || trend.ResetsAtUtc <= _now)
        {
            ChartModel = null;
            EvaluateRecoveryWatches();
            return;
        }

        var forecast = _forecasts.FirstOrDefault(candidate => candidate.BucketId.Equals(trend.BucketId, StringComparison.OrdinalIgnoreCase));
        _referenceForecasts.TryGetValue(trend.BucketId, out var referenceForecast);
        if (referenceForecast?.ResetAt != trend.ResetsAtUtc)
        {
            _referenceForecasts.Remove(trend.BucketId);
            referenceForecast = null;
        }

        var selectedDuration = _selectedBlockDurations.TryGetValue(trend.BucketId, out var rememberedDuration)
            ? (int?)rememberedDuration
            : null;
        var limitForecasts = _forecasts
            .Where(candidate => candidate.LimitKey.Equals(trend.LimitKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var chart = _presenter.BuildChart(
            trend,
            forecast,
            _now,
            ShowProjection,
            ShowRange,
            referenceForecast,
            selectedDuration,
            limitForecasts);
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

        var resolvedDuration = chart?.BlockAdvisor?.Options
            .FirstOrDefault(option => option.IsSelected)
            ?.DurationMinutes;
        if (resolvedDuration is int durationMinutes
            && (!_selectedBlockDurations.TryGetValue(trend.BucketId, out var previousDuration)
                || previousDuration != durationMinutes))
        {
            _selectedBlockDurations[trend.BucketId] = durationMinutes;
            OnPropertyChanged(nameof(SelectedBlockDurationMinutes));
        }
        ChartModel = chart;
        EvaluateRecoveryWatches();
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

    private void SelectBlockDuration(object? parameter)
    {
        var durationMinutes = parameter switch
        {
            int minutes => minutes,
            string text when int.TryParse(text, out var minutes) => minutes,
            _ => 0
        };
        if (durationMinutes <= 0
            || _selectedWindow is null
            || !BlockOptions.Any(option => option.DurationMinutes == durationMinutes))
        {
            return;
        }

        SelectedBlockDurationMinutes = durationMinutes;
    }

    private void ToggleRecoveryWatch()
    {
        if (TryGetCurrentRecoveryWatch(out var activeWatch))
        {
            var activeScopeKey = BuildRecoveryScopeKey(activeWatch.LimitKey, activeWatch.WindowDurationMins);
            _recoveryWatches.Remove(activeScopeKey);
            _observedRecoveryWatchScopes.Remove(activeScopeKey);
            _recoveryConfirmationText = null;
            NotifyRecoveryWatchChanged();
            return;
        }

        if (!TryGetCurrentRecoveryScope(out var limitKey, out var windowDurationMins, out var resetAt)
            || !IsCurrentRecoveryWatchEligible()
            || SelectedBlockDurationMinutes is not int blockDurationMinutes)
        {
            return;
        }

        var scopeKey = BuildRecoveryScopeKey(limitKey, windowDurationMins);
        _recoveryWatches[scopeKey] = new RecoveryWatchSettings(
            limitKey,
            windowDurationMins,
            blockDurationMinutes,
            resetAt);
        _observedRecoveryWatchScopes.Add(scopeKey);
        _recoveryConfirmationText = null;
        NotifyRecoveryWatchChanged();
    }

    private void EvaluateRecoveryWatches()
    {
        var hasAuthoritativeLiveSnapshot = _trends.Any(candidate => !candidate.IsMock);
        foreach (var watch in _recoveryWatches.Values.ToArray())
        {
            var scopeKey = BuildRecoveryScopeKey(watch.LimitKey, watch.WindowDurationMins);
            var trend = _trends.FirstOrDefault(candidate =>
                candidate.LimitKey.Equals(watch.LimitKey, StringComparison.OrdinalIgnoreCase)
                && candidate.WindowDurationMins == watch.WindowDurationMins);
            if (trend is null)
            {
                if (hasAuthoritativeLiveSnapshot && _now >= watch.ResetAtUtc)
                {
                    CompleteRecoveryWatch(watch, recoveredEarly: false);
                }

                continue;
            }

            if (trend.IsMock)
            {
                continue;
            }

            var activeWatch = watch;
            if (_now >= activeWatch.ResetAtUtc && _observedRecoveryWatchScopes.Contains(scopeKey))
            {
                CompleteRecoveryWatch(activeWatch, recoveredEarly: false);
                continue;
            }

            if (activeWatch.ResetAtUtc != trend.ResetsAtUtc)
            {
                activeWatch = activeWatch with { ResetAtUtc = trend.ResetsAtUtc };
                _recoveryWatches[scopeKey] = activeWatch;
                NotifyRecoveryWatchChanged();
            }

            if (_now >= activeWatch.ResetAtUtc)
            {
                CompleteRecoveryWatch(activeWatch, recoveredEarly: false);
                continue;
            }

            _observedRecoveryWatchScopes.Add(scopeKey);

            var forecast = _forecasts.FirstOrDefault(candidate =>
                candidate.BucketId.Equals(trend.BucketId, StringComparison.OrdinalIgnoreCase));
            if (forecast is null || forecast.IsMock)
            {
                continue;
            }

            var watchedChart = _presenter.BuildChart(
                trend,
                forecast,
                _now,
                ShowProjection,
                ShowRange,
                referenceForecast: null,
                selectedBlockDurationMinutes: activeWatch.BlockDurationMinutes,
                liveForecasts: _forecasts.Where(candidate =>
                    candidate.LimitKey.Equals(trend.LimitKey, StringComparison.OrdinalIgnoreCase)).ToArray());
            if (watchedChart?.BlockAdvisor?.Status == UsageTrendBlockAdvisorStatus.LikelyFits)
            {
                CompleteRecoveryWatch(activeWatch, recoveredEarly: true);
            }
        }
    }

    private void CompleteRecoveryWatch(RecoveryWatchSettings watch, bool recoveredEarly)
    {
        var scopeKey = BuildRecoveryScopeKey(watch.LimitKey, watch.WindowDurationMins);
        if (!_recoveryWatches.Remove(scopeKey))
        {
            return;
        }

        _observedRecoveryWatchScopes.Remove(scopeKey);
        var block = FormatBlockDurationLabel(watch.BlockDurationMinutes);
        var limit = FormatWindowLabel(string.Empty, watch.WindowDurationMins);
        var title = recoveredEarly ? "Ready to code again" : "Quota reset reached";
        var message = recoveredEarly
            ? $"A {block} block now likely fits in your {limit}."
            : $"Your {limit} reset was reached for this {block} block; re-sync if no fresh sample is available.";
        _recoveryConfirmationText = message;
        NotifyRecoveryWatchChanged();
        PublishRecoveryWatchCompleted(title, message);
    }

    private bool IsCurrentRecoveryWatchEligible()
    {
        if (!TryGetCurrentRecoveryScope(out _, out _, out _)
            || ChartModel?.BlockAdvisor is not { } advisor)
        {
            return false;
        }

        var forecast = _selectedWindow is null
            ? null
            : _forecasts.FirstOrDefault(candidate => candidate.BucketId.Equals(_selectedWindow.BucketId, StringComparison.OrdinalIgnoreCase));
        if (forecast is null || forecast.IsMock)
        {
            return false;
        }

        return advisor.Status is UsageTrendBlockAdvisorStatus.MayBeInterrupted
            or UsageTrendBlockAdvisorStatus.UnlikelyToFit
            or UsageTrendBlockAdvisorStatus.WaitForReset;
    }

    private bool TryGetCurrentRecoveryWatch(out RecoveryWatchSettings watch)
    {
        if (TryGetCurrentRecoveryScope(out var limitKey, out var windowDurationMins, out _)
            && _recoveryWatches.TryGetValue(BuildRecoveryScopeKey(limitKey, windowDurationMins), out watch!))
        {
            return true;
        }

        watch = null!;
        return false;
    }

    private bool TryGetCurrentRecoveryScope(out string limitKey, out int windowDurationMins, out DateTimeOffset resetAt)
    {
        limitKey = string.Empty;
        windowDurationMins = 0;
        resetAt = default;
        if (_selectedWindow is null)
        {
            return false;
        }

        var trend = _trends.FirstOrDefault(candidate => candidate.BucketId.Equals(_selectedWindow.BucketId, StringComparison.OrdinalIgnoreCase));
        if (trend is null || trend.IsMock || trend.WindowDurationMins is not int duration || duration <= 0)
        {
            return false;
        }

        limitKey = trend.LimitKey;
        windowDurationMins = duration;
        resetAt = trend.ResetsAtUtc;
        return true;
    }

    private void NotifyRecoveryWatchChanged()
    {
        NotifyRecoveryWatchPropertiesChanged();
        PublishRecoveryWatchesChanged();
    }

    private void PublishRecoveryWatchCompleted(string title, string message)
    {
        var handlers = RecoveryWatchCompleted;
        if (handlers is null)
        {
            return;
        }

        var eventArgs = new UsageTrendRecoveryCompletedEventArgs(title, message);
        foreach (EventHandler<UsageTrendRecoveryCompletedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception exception)
            {
                PrivacySafeDiagnostics.WriteFailure("recovery notification subscriber failed", exception);
            }
        }
    }

    private void PublishRecoveryWatchesChanged()
    {
        var handlers = RecoveryWatchesChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                PrivacySafeDiagnostics.WriteFailure("recovery watch subscriber failed", exception);
            }
        }
    }

    private void NotifyRecoveryWatchPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasActiveRecoveryWatch));
        OnPropertyChanged(nameof(CanActivateRecoveryWatch));
        OnPropertyChanged(nameof(CanManageRecoveryWatch));
        OnPropertyChanged(nameof(RecoveryWatchActionText));
        OnPropertyChanged(nameof(RecoveryWatchText));
        OnPropertyChanged(nameof(RecoveryWatchAccessibleSummary));
        OnPropertyChanged(nameof(HasRecoveryConfirmation));
        OnPropertyChanged(nameof(RecoveryConfirmationText));
    }

    private static string BuildRecoveryScopeKey(string limitKey, int windowDurationMins) => $"{limitKey}\u001f{windowDurationMins}";

    private static string FormatBlockDurationLabel(int minutes) => minutes switch
    {
        60 => "1h",
        480 => "1 day (8h)",
        _ => FormatCountdown(TimeSpan.FromMinutes(minutes))
    };

    private static string FormatCountdown(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "now";
        }

        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}h {value.Minutes}m";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(value.TotalMinutes))}m";
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

public sealed record UsageTrendRecoveryCompletedEventArgs(string Title, string Message);
