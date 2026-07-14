using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Slices.AccountUsage;
using PulseMeter.Slices.DataBar;
using PulseMeter.Slices.DailyUsage;
using PulseMeter.Slices.ExpandedHeader;
using PulseMeter.Slices.NavigationRail;
using PulseMeter.Slices.NeedsAttention;
using PulseMeter.Slices.ProjectUsage;
using PulseMeter.Slices.RateLimits;
using PulseMeter.Slices.RateLimitsDaily;
using PulseMeter.Slices.ResetCredits;
using PulseMeter.Slices.PulseMeterWindow;
using PulseMeter.Platform.Persistence;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;
using PulseMeter.Shared.Formatting;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Slices.UsageSignals;

namespace PulseMeter.Slices.PulseMeterWindow.UI;

public sealed class PulseMeterWindowViewModel : INotifyPropertyChanged
{
    private static readonly BudgetAlertSettings AutomaticBudgetSignalSettings = BudgetAlertSettings.Default with
    {
        DailyTokenBudget = null
    };

    private readonly IUsageService _usageService;
    private readonly IUsageSignalsTracker _usageSignalsTracker;
    private readonly IBudgetAlertTracker _budgetAlertTracker;
    private bool _autoHideWhenFocusLeaves;
    private bool _autoShowWhenCodexFocused = true;
    private int _autoSyncSeconds;
    private bool _isAlwaysOnTop;
    private bool _isExpanded;
    private bool _isHiddenByUser;
    private bool _isRefreshing;
    private string _syncFeedbackText = string.Empty;
    private bool _useMockMode;
    private double _expandedLayoutScale = 1.0;
    private double _expandedWindowHeight = PulseMeterWindowLayoutCalculator.DefaultExpandedWindowHeight;
    private double _expandedWindowWidth = PulseMeterWindowLayoutCalculator.DefaultExpandedWindowWidth;
    private double? _windowLeft;
    private double? _windowTop;
    private UsageSnapshot _snapshot = new()
    {
        SyncStatus = SyncStatus.Unavailable,
        Source = "Starting",
        StatusMessage = "Starting PulseMeter."
    };
    private UsageSignalsSnapshot _usageSignals = UsageSignalsSnapshot.Empty;

    public PulseMeterWindowViewModel(
        IUsageService usageService,
        TimeSpan? autoSyncInterval = null,
        IResetCreditStateStore? resetCreditStateStore = null,
        PulseMeterWindowState? windowState = null,
        bool isAlwaysOnTop = false,
        DataBarViewModel? dataBar = null,
        ExpandedHeaderViewModel? expandedHeader = null,
        NavigationRailViewModel? navigationRail = null,
        RateLimitsSectionViewModel? rateLimits = null,
        RateLimitsDailySectionViewModel? rateLimitsDaily = null,
        NeedsAttentionSectionViewModel? needsAttention = null,
        AccountUsageSectionViewModel? accountUsage = null,
        DailyUsageSectionViewModel? dailyUsage = null,
        ResetCreditsSectionViewModel? resetCreditsSection = null,
        ProjectUsageSectionViewModel? projectUsage = null,
        UsageAttributionSectionViewModel? usageAttribution = null,
        IUsageSignalsTracker? usageSignalsTracker = null,
        IBudgetAlertTracker? budgetAlertTracker = null)
    {
        _usageService = usageService;
        _usageSignalsTracker = usageSignalsTracker ?? new UsageSignalsTracker(new ZeroUserIdleTimeProvider());
        _budgetAlertTracker = budgetAlertTracker ?? new BudgetAlertTracker();
        _autoSyncSeconds = SecondsFrom(autoSyncInterval ?? TimeSpan.FromSeconds(90));
        _isAlwaysOnTop = isAlwaysOnTop;
        DataBar = dataBar ?? new DataBarViewModel();
        ExpandedHeader = expandedHeader ?? new ExpandedHeaderViewModel();
        NavigationRail = navigationRail ?? new NavigationRailViewModel();
        RateLimits = rateLimits ?? new RateLimitsSectionViewModel(new RateLimitsPresenter());
        RateLimitsDaily = rateLimitsDaily ?? new RateLimitsDailySectionViewModel(new RateLimitsDailyPresenter());
        NeedsAttention = needsAttention ?? new NeedsAttentionSectionViewModel(new NeedsAttentionPresenter());
        ResetCreditsSection = resetCreditsSection ?? new ResetCreditsSectionViewModel(new ResetCreditsPresenter(resetCreditStateStore));
        AccountUsage = accountUsage ?? new AccountUsageSectionViewModel(new AccountUsagePresenter());
        ProjectUsage = projectUsage ?? new ProjectUsageSectionViewModel(new ProjectUsagePresenter());
        UsageAttribution = usageAttribution ?? new UsageAttributionSectionViewModel(new UsageAttributionPresenter());
        DailyUsage = dailyUsage ?? new DailyUsageSectionViewModel(new DailyUsagePresenter());
        NavigationRail.PropertyChanged += OnNavigationRailPropertyChanged;
        RateLimits.PropertyChanged += OnRateLimitsPropertyChanged;
        DailyUsage.PropertyChanged += OnDailyUsagePropertyChanged;
        UsageAttribution.PropertyChanged += OnUsageAttributionPropertyChanged;
        _useMockMode = usageService.UseMockMode;
        if (windowState is not null)
        {
            ApplyInitialWindowState(windowState);
        }

        SyncNowCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        RefreshTopChromeViewModels();
        RefreshResetCredits(DateTimeOffset.UtcNow, updateFromSnapshot: false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DataBarViewModel DataBar { get; }

    public ExpandedHeaderViewModel ExpandedHeader { get; }

    public NavigationRailViewModel NavigationRail { get; }

    public RateLimitsSectionViewModel RateLimits { get; }

    public RateLimitsDailySectionViewModel RateLimitsDaily { get; }

    public NeedsAttentionSectionViewModel NeedsAttention { get; }

    public ResetCreditsSectionViewModel ResetCreditsSection { get; }

    public AccountUsageSectionViewModel AccountUsage { get; }

    public ProjectUsageSectionViewModel ProjectUsage { get; }

    public UsageAttributionSectionViewModel UsageAttribution { get; }

    public DailyUsageSectionViewModel DailyUsage { get; }

    public ObservableCollection<RateLimitBucket> Buckets { get; } = new();

    public ObservableCollection<DailyUsageBucket> DailyBuckets { get; } = new();

    public ObservableCollection<RateLimitOption> LimitOptions => RateLimits.LimitOptions;

    public ObservableCollection<QuotaDisplayRow> CompactQuotaRows => RateLimits.CompactQuotaRows;

    public ObservableCollection<DailyRateLimitDisplayRow> DailyRateLimitRows => RateLimitsDaily.DailyRateLimitRows;

    public ObservableCollection<DailyUsageDisplayRow> DailyUsageRows => DailyUsage.DailyUsageRows;

    public ObservableCollection<ProjectUsageDisplayRow> ProjectUsageRows => ProjectUsage.ProjectUsageRows;

    public ObservableCollection<UsageAttributionSessionDisplayRow> UsageAttributionSessionRows => UsageAttribution.SessionRows;

    public ObservableCollection<UsageAttributionBurnEventDisplayRow> UsageAttributionBurnEventRows => UsageAttribution.BurnEventRows;

    public ObservableCollection<RateLimitBucket> SelectedBuckets => RateLimits.SelectedBuckets;

    public ObservableCollection<QuotaDisplayRow> SelectedQuotaRows => RateLimits.SelectedQuotaRows;

    public ObservableCollection<ResetCreditListItem> ResetCredits => ResetCreditsSection.ResetCredits;

    public AsyncRelayCommand SyncNowCommand { get; }

    public bool AutoHideWhenFocusLeaves
    {
        get => _autoHideWhenFocusLeaves;
        set => SetField(ref _autoHideWhenFocusLeaves, value);
    }

    public bool AutoShowWhenCodexFocused
    {
        get => _autoShowWhenCodexFocused;
        set => SetField(ref _autoShowWhenCodexFocused, value);
    }

    public TimeSpan AutoSyncInterval => TimeSpan.FromSeconds(AutoSyncSeconds);

    public int AutoSyncSeconds
    {
        get => _autoSyncSeconds;
        set
        {
            var seconds = Math.Clamp(value, 1, 86_400);
            if (SetField(ref _autoSyncSeconds, seconds))
            {
                OnPropertyChanged(nameof(AutoSyncInterval));
                OnPropertyChanged(nameof(AutoSyncText));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasWindowPosition => WindowLeft.HasValue && WindowTop.HasValue;

    public bool HasDailyUsageMedianSummary => DailyUsage.HasDailyUsageMedianSummary;

    public bool HasProjectUsage => ProjectUsage.HasProjectUsage;

    public bool HasUsageAttribution => UsageAttribution.HasAttribution;

    public bool HasDailyRateLimitRows => RateLimitsDaily.HasDailyRateLimitRows;

    public string RateLimitsDailySummaryText => RateLimitsDaily.RateLimitsDailySummaryText;

    public bool HasRateLimitsDailyWarning => RateLimitsDaily.HasRateLimitsDailyWarning;

    public string RateLimitsDailyWarningText => RateLimitsDaily.RateLimitsDailyWarningText;

    public bool ShouldShowProjectUsage => HasProjectUsage && IsProjectUsageVisible;

    public bool ShouldShowUsageAttribution => IsUsageAttributionVisible;

    public bool IsDailyUsageExpanded => DailyUsage.IsDailyUsageExpanded;

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetField(ref _isAlwaysOnTop, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (SetField(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandCollapseGlyph));
                OnPropertyChanged(nameof(ExpandCollapseTooltip));
                OnPropertyChanged(nameof(WindowHeight));
                OnPropertyChanged(nameof(WindowMinHeight));
                OnPropertyChanged(nameof(WindowMinWidth));
                OnPropertyChanged(nameof(WindowWidth));
                RefreshTopChromeViewModels();
            }
        }
    }

    public bool IsNavigationPanelExpanded => NavigationRail.IsNavigationPanelExpanded;

    public bool IsRateLimitsVisible
    {
        get => NavigationRail.IsRateLimitsVisible;
        set => NavigationRail.IsRateLimitsVisible = value;
    }

    public bool IsRateLimitsDailyVisible
    {
        get => NavigationRail.IsRateLimitsDailyVisible;
        set => NavigationRail.IsRateLimitsDailyVisible = value;
    }

    public bool IsResetCreditsVisible
    {
        get => NavigationRail.IsResetCreditsVisible;
        set => NavigationRail.IsResetCreditsVisible = value;
    }

    public bool IsAccountUsageVisible
    {
        get => NavigationRail.IsAccountUsageVisible;
        set => NavigationRail.IsAccountUsageVisible = value;
    }

    public bool IsProjectUsageVisible
    {
        get => NavigationRail.IsProjectUsageVisible;
        set => NavigationRail.IsProjectUsageVisible = value;
    }

    public bool IsUsageAttributionVisible
    {
        get => NavigationRail.IsUsageAttributionVisible;
        set => NavigationRail.IsUsageAttributionVisible = value;
    }

    public bool IsDailyUsageVisible
    {
        get => NavigationRail.IsDailyUsageVisible;
        set => NavigationRail.IsDailyUsageVisible = value;
    }

    public double WindowMinWidth => PulseMeterWindowLayoutCalculator.WindowMinWidth(IsExpanded);

    public double WindowWidth => PulseMeterWindowLayoutCalculator.WindowWidth(IsExpanded, _expandedWindowWidth);

    public double WindowMinHeight => PulseMeterWindowLayoutCalculator.WindowMinHeight(IsExpanded);

    public double WindowHeight => PulseMeterWindowLayoutCalculator.WindowHeight(IsExpanded, _expandedWindowHeight);

    public double? WindowLeft => _windowLeft;

    public double? WindowTop => _windowTop;

    public double NavigationPanelWidth => NavigationRail.NavigationPanelWidth;

    public double ExpandedLayoutScale => _expandedLayoutScale;

    public string DailyUsageExpandCollapseGlyph => DailyUsage.DailyUsageExpandCollapseGlyph;

    public string DailyUsageExpandCollapseTooltip => DailyUsage.DailyUsageExpandCollapseTooltip;

    public string DailyUsageMedianSummaryText => DailyUsage.DailyUsageMedianSummaryText;

    public string ProjectUsageEstimateText => ProjectUsage.ProjectUsageEstimateText;

    public string ExpandCollapseGlyph => IsExpanded ? "\u25B2" : "\u25BC";

    public string ExpandCollapseTooltip => IsExpanded ? "Collapse details" : "Expand details";

    public string NavigationPanelToggleTooltip => NavigationRail.NavigationPanelToggleTooltip;

    public string NavigationPanelToggleGlyph => NavigationRail.NavigationPanelToggleGlyph;

    public bool IsHiddenByUser
    {
        get => _isHiddenByUser;
        private set => SetField(ref _isHiddenByUser, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetField(ref _isRefreshing, value))
            {
                OnPropertyChanged(nameof(CompactSummary));
                OnPropertyChanged(nameof(CompactQuotaSummaryText));
                OnPropertyChanged(nameof(HasSyncFeedback));
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(SyncButtonText));
                OnPropertyChanged(nameof(SyncFeedbackText));
                OnPropertyChanged(nameof(SyncStatusText));
                RefreshTopChromeViewModels();
            }
        }
    }

    public RateLimitOption? SelectedLimitOption
    {
        get => RateLimits.SelectedLimitOption;
        set
        {
            if (!EqualityComparer<RateLimitOption?>.Default.Equals(RateLimits.SelectedLimitOption, value))
            {
                RateLimits.SelectedLimitOption = value;
                RefreshComputedProperties();
            }
        }
    }

    public bool UseMockMode
    {
        get => _useMockMode;
        set
        {
            if (SetField(ref _useMockMode, value))
            {
                _usageService.UseMockMode = value;
                OnPropertyChanged(nameof(StatusBadgeText));
                RefreshTopChromeViewModels();
                _ = RefreshAsync();
            }
        }
    }

    public string AccountUsageSummaryText => AccountUsage.AccountUsageSummaryText;

    public string CompactSummary
    {
        get
        {
            return CompactQuotaSummaryText == "usage unavailable"
                ? $"PulseMeter - {SyncStatusText} - usage unavailable"
                : $"{CompactTitleText}: {CompactQuotaSummaryText} - {SyncStatusText}";
        }
    }

    public string CompactTitleText
    {
        get
        {
            return RateLimits.CompactTitleText;
        }
    }

    public string CompactQuotaSummaryText
    {
        get
        {
            return RateLimits.CompactQuotaSummaryText;
        }
    }

    public string ExpandedQuotaSummaryText
    {
        get
        {
            return RateLimits.ExpandedQuotaSummaryText;
        }
    }

    public string HeaderText => "PulseMeter";

    public string AutoSyncText => $"Auto every {MeterDisplayFormatter.FormatInterval(AutoSyncInterval)}";

    public bool HasSyncFeedback => IsRefreshing || !string.IsNullOrWhiteSpace(_syncFeedbackText);

    public string LastUpdatedText
    {
        get
        {
            if (_snapshot.LastUpdatedUtc is not DateTimeOffset updated)
            {
                return "Updated unknown";
            }

            var localUpdated = updated.ToLocalTime();
            var dayLabel = localUpdated.Date == DateTimeOffset.Now.Date
                ? "today"
                : localUpdated.ToString("MMM d");

            return $"Updated {dayLabel} {localUpdated:HH:mm}";
        }
    }

    public string ResetCreditsHeaderText => ResetCreditsSection.ResetCreditsHeaderText;

    public string ResetCreditsAvailableText => ResetCreditsSection.ResetCreditsAvailableText;

    public string SafeToStartText => SafeToStartEvaluator.Evaluate(Buckets).Message;

    public string SourceText => $"Source: {DisplaySource(_snapshot.Source)}";

    public string StatusBadgeText => IsRefreshing
        ? "SYNCING"
        : UseMockMode || _snapshot.SyncStatus == SyncStatus.Mocked ? "MOCK DATA" : SyncStatusText.ToUpperInvariant();

    public string StatusMessage => _snapshot.StatusMessage ?? string.Empty;

    public string SyncButtonText => IsRefreshing ? "Syncing..." : "Sync now";

    public string SyncFeedbackText => IsRefreshing ? "Syncing now..." : _syncFeedbackText;

    public string SyncStatusText => EffectiveSyncStatus switch
    {
        SyncStatus.Mocked => "Mock",
        SyncStatus.Live => "Live",
        SyncStatus.Stale => "Stale",
        SyncStatus.Unavailable => "Unavailable",
        _ => "Unknown"
    };

    private SyncStatus EffectiveSyncStatus => _snapshot.SyncStatus == SyncStatus.Live && IsLiveSnapshotOverdue
        ? SyncStatus.Stale
        : _snapshot.SyncStatus;

    private bool IsLiveSnapshotOverdue
    {
        get
        {
            if (_snapshot.LastUpdatedUtc is not DateTimeOffset updated)
            {
                return false;
            }

            var overdueAfter = TimeSpan.FromTicks(Math.Max(AutoSyncInterval.Ticks * 2, TimeSpan.FromMinutes(3).Ticks));
            return DateTimeOffset.UtcNow - updated > overdueAfter;
        }
    }

    public bool HasThreadContext => _snapshot.RecentActiveThread is not null;

    public string ThreadContextText
    {
        get
        {
            var thread = _snapshot.RecentActiveThread;
            if (thread is null)
            {
                return "Waiting for active session usage event.";
            }

            var context = thread.ContextLeftPercent is double left
                ? $"Context: {left:0}% left"
                : "Context: waiting for usage payload";

            return $"Recent active session: {thread.DisplayName} - {context}";
        }
    }

    public string ThreadTokenText
    {
        get
        {
            var thread = _snapshot.RecentActiveThread;
            if (thread?.TotalTokens is long total)
            {
                return $"{MeterDisplayFormatter.FormatTokens(total)} tokens - exact current Desktop thread: no";
            }

            return "Exact current Desktop thread: no";
        }
    }

    public string TodayUsageText => AccountUsage.TodayUsageText;

    public string TodayUsageMetricValueText => AccountUsage.TodayUsageMetricValueText;

    public bool HasDailyUsageFreshnessWarning => AccountUsage.HasDailyUsageFreshnessWarning;

    public string DailyUsageFreshnessWarningText => AccountUsage.DailyUsageFreshnessWarningText;

    public bool HasAccountSummaryFreshnessWarning => AccountUsage.HasAccountSummaryFreshnessWarning;

    public string AccountSummaryFreshnessWarningText => AccountUsage.AccountSummaryFreshnessWarningText;

    public string LifetimeUsageValueText => AccountUsage.LifetimeUsageValueText;

    public string PeakUsageValueText => AccountUsage.PeakUsageValueText;

    public string StreakDaysValueText => AccountUsage.StreakDaysValueText;

    public string LifetimeUsageCaptionText => AccountUsage.LifetimeUsageCaptionText;

    public string PeakUsageCaptionText => AccountUsage.PeakUsageCaptionText;

    public string StreakCaptionText => AccountUsage.StreakCaptionText;

    public double TodayMedianDailyPercentValue
    {
        get
        {
            return AccountUsage.TodayMedianDailyPercentValue;
        }
    }

    public string TodayMedianDailyPercentText
    {
        get
        {
            return AccountUsage.TodayMedianDailyPercentText;
        }
    }

    public double TodayPeakPercentValue => TodayMedianDailyPercentValue;

    public string TodayPeakPercentText => TodayMedianDailyPercentText;

    public string DailyUsageWindowText => DailyUsage.DailyUsageWindowText;

    public string TodayUsageValueText
    {
        get
        {
            return AccountUsage.TodayUsageValueText;
        }
    }

    public void ApplySnapshot(UsageSnapshot snapshot)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        UpdateAccountUsageFreshnessWarnings(snapshot);
        _snapshot = snapshot;
        var usageSignals = _usageSignalsTracker.Observe(snapshot, nowUtc);
        var budgetSignals = _budgetAlertTracker.Observe(snapshot, AutomaticBudgetSignalSettings, nowUtc);
        _usageSignals = MergeSignals(usageSignals, budgetSignals.AttentionSignals);

        Buckets.Clear();
        foreach (var bucket in snapshot.Buckets)
        {
            Buckets.Add(bucket);
        }

        RebuildLimitOptions();
        RateLimits.ApplyUsageSignals(_usageSignals);
        RefreshResetCredits(DateTimeOffset.UtcNow, updateFromSnapshot: true);

        DailyBuckets.Clear();
        foreach (var bucket in snapshot.DailyBuckets.TakeLast(7))
        {
            DailyBuckets.Add(bucket);
        }
        RebuildDailyUsageRows();
        RebuildProjectUsageRows();
        RebuildUsageAttributionRows(nowUtc);
        NeedsAttention.ApplySignals(_usageSignals);

        RefreshComputedProperties();
    }

    public void RefreshClock()
    {
        RefreshQuotaRows();
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(CompactQuotaSummaryText));
        OnPropertyChanged(nameof(ExpandedQuotaSummaryText));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(SyncStatusText));
        RefreshResetCredits(DateTimeOffset.UtcNow, updateFromSnapshot: false);
        OnPropertyChanged(nameof(HasThreadContext));
        OnPropertyChanged(nameof(ThreadContextText));
        OnPropertyChanged(nameof(ThreadTokenText));
        RebuildDailyUsageRows();
        RefreshUsageAttributionRows(DateTimeOffset.UtcNow);
        NeedsAttention.Refresh(DateTimeOffset.UtcNow);
        RefreshAccountDashboardProperties();
        OnPropertyChanged(nameof(TodayUsageText));
        OnPropertyChanged(nameof(TodayUsageValueText));
        RefreshTopChromeViewModels();
    }

    public PulseMeterWindowState CaptureWindowState()
    {
        return new PulseMeterWindowState(IsExpanded, _expandedWindowWidth, _expandedWindowHeight, WindowLeft, WindowTop);
    }

    public void RememberWindowSize(double width, double height)
    {
        if (!IsExpanded)
        {
            return;
        }

        var sanitizedWidth = PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowWidth(width);
        var sanitizedHeight = PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowHeight(height);
        var widthChanged = Math.Abs(_expandedWindowWidth - sanitizedWidth) >= 0.5;
        var heightChanged = Math.Abs(_expandedWindowHeight - sanitizedHeight) >= 0.5;

        if (!widthChanged && !heightChanged)
        {
            return;
        }

        _expandedWindowWidth = sanitizedWidth;
        _expandedWindowHeight = sanitizedHeight;

        if (widthChanged)
        {
            OnPropertyChanged(nameof(WindowWidth));
        }

        if (heightChanged)
        {
            OnPropertyChanged(nameof(WindowHeight));
        }
    }

    public void UpdateExpandedLayoutScale(double width, double height)
    {
        SetField(
            ref _expandedLayoutScale,
            PulseMeterWindowLayoutCalculator.CalculateExpandedLayoutScale(IsExpanded, width, height),
            nameof(ExpandedLayoutScale));
    }

    public void RestoreExpandedWindowToNormalSize()
    {
        if (!IsExpanded)
        {
            return;
        }

        var widthChanged = Math.Abs(_expandedWindowWidth - PulseMeterWindowLayoutCalculator.NormalExpandedWindowWidth) >= 0.5;
        var heightChanged = Math.Abs(_expandedWindowHeight - PulseMeterWindowLayoutCalculator.NormalExpandedWindowHeight) >= 0.5;

        if (!widthChanged && !heightChanged)
        {
            return;
        }

        _expandedWindowWidth = PulseMeterWindowLayoutCalculator.NormalExpandedWindowWidth;
        _expandedWindowHeight = PulseMeterWindowLayoutCalculator.NormalExpandedWindowHeight;

        if (widthChanged)
        {
            OnPropertyChanged(nameof(WindowWidth));
        }

        if (heightChanged)
        {
            OnPropertyChanged(nameof(WindowHeight));
        }
    }

    public void RememberWindowPosition(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return;
        }

        var leftChanged = !_windowLeft.HasValue || Math.Abs(_windowLeft.Value - left) >= 0.5;
        var topChanged = !_windowTop.HasValue || Math.Abs(_windowTop.Value - top) >= 0.5;

        if (!leftChanged && !topChanged)
        {
            return;
        }

        var hadPosition = HasWindowPosition;
        _windowLeft = left;
        _windowTop = top;

        if (!hadPosition)
        {
            OnPropertyChanged(nameof(HasWindowPosition));
        }

        if (leftChanged)
        {
            OnPropertyChanged(nameof(WindowLeft));
        }

        if (topChanged)
        {
            OnPropertyChanged(nameof(WindowTop));
        }
    }

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    public void ToggleDailyUsageExpanded()
    {
        DailyUsage.ToggleDailyUsageExpanded();
    }

    public void ToggleNavigationPanel()
    {
        NavigationRail.ToggleNavigationPanel();
    }

    public void Collapse()
    {
        IsExpanded = false;
    }

    public void MarkHiddenByUser()
    {
        IsHiddenByUser = true;
    }

    public void MarkShownByUser()
    {
        IsHiddenByUser = false;
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        SyncNowCommand.RaiseCanExecuteChanged();

        try
        {
            var snapshot = await _usageService.GetSnapshotAsync();
            ApplySnapshot(snapshot);
            SetSyncFeedback(BuildRefreshFeedback(snapshot));
        }
        catch (Exception ex)
        {
            SetSyncFeedback($"Sync failed: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            SyncNowCommand.RaiseCanExecuteChanged();
        }
    }

    private static string BuildRefreshFeedback(UsageSnapshot snapshot)
    {
        if (snapshot.SyncStatus == SyncStatus.Unavailable)
        {
            var message = string.IsNullOrWhiteSpace(snapshot.StatusMessage)
                ? "The monitored app is not running. Start it, then sync again."
                : snapshot.StatusMessage;

            return $"Source unavailable: {message}";
        }

        var syncedAt = (snapshot.LastUpdatedUtc ?? DateTimeOffset.UtcNow).ToLocalTime();
        return $"Synced at {syncedAt:HH:mm}";
    }

    private static int SecondsFrom(TimeSpan interval)
    {
        return Math.Clamp((int)Math.Round(interval.TotalSeconds), 1, 86_400);
    }

    private static string DisplaySource(string source)
    {
        return source.Equals("AppServer", StringComparison.OrdinalIgnoreCase)
            || source.Equals("Codex", StringComparison.OrdinalIgnoreCase)
                ? "Live source"
                : source;
    }

    private void RefreshTopChromeViewModels()
    {
        DataBar.ApplyState(
            IsExpanded,
            CompactQuotaRows,
            StatusBadgeText,
            ExpandCollapseTooltip);

        ExpandedHeader.ApplyState(
            CompactTitleText,
            StatusBadgeText,
            ExpandCollapseTooltip,
            SyncNowCommand);
    }

    private void OnRateLimitsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RateLimitsSectionViewModel.SelectedLimitOption))
        {
            RefreshDailyRateLimitRows();
            OnPropertyChanged(nameof(SelectedLimitOption));
        }

        if (e.PropertyName is nameof(RateLimitsSectionViewModel.CompactTitleText)
            or nameof(RateLimitsSectionViewModel.CompactQuotaSummaryText)
            or nameof(RateLimitsSectionViewModel.ExpandedQuotaSummaryText)
            or nameof(RateLimitsSectionViewModel.SelectedLimitOption))
        {
            OnPropertyChanged(nameof(CompactSummary));
            OnPropertyChanged(nameof(CompactTitleText));
            OnPropertyChanged(nameof(CompactQuotaSummaryText));
            OnPropertyChanged(nameof(ExpandedQuotaSummaryText));
            RefreshTopChromeViewModels();
        }
    }

    private void OnNavigationRailPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(NavigationRailViewModel.IsProjectUsageVisible))
        {
            OnPropertyChanged(nameof(ShouldShowProjectUsage));
        }

        if (e.PropertyName == nameof(NavigationRailViewModel.IsUsageAttributionVisible))
        {
            OnPropertyChanged(nameof(ShouldShowUsageAttribution));
        }
    }

    private void OnDailyUsagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DailyUsageSectionViewModel.IsDailyUsageExpanded)
            or nameof(DailyUsageSectionViewModel.DailyUsageExpandCollapseGlyph)
            or nameof(DailyUsageSectionViewModel.DailyUsageExpandCollapseTooltip))
        {
            OnPropertyChanged(e.PropertyName);
        }
    }

    private void OnUsageAttributionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UsageAttributionSectionViewModel.HasAttribution)
            or nameof(UsageAttributionSectionViewModel.SummaryText)
            or nameof(UsageAttributionSectionViewModel.EvidenceText))
        {
            OnPropertyChanged(nameof(HasUsageAttribution));
            OnPropertyChanged(nameof(ShouldShowUsageAttribution));
        }
    }

    private void RebuildLimitOptions()
    {
        RateLimits.ApplyBuckets(Buckets, DateTimeOffset.UtcNow);
        RefreshDailyRateLimitRows();
        OnPropertyChanged(nameof(SelectedLimitOption));
    }

    private void RefreshSelectedBuckets()
    {
        RateLimits.Refresh(DateTimeOffset.UtcNow);
        RefreshDailyRateLimitRows();
    }

    private void RefreshQuotaRows()
    {
        RateLimits.Refresh(DateTimeOffset.UtcNow);
        RefreshDailyRateLimitRows();
    }

    private void RefreshDailyRateLimitRows()
    {
        RateLimitsDaily.ApplySelectedBuckets(SelectedBuckets, DateTimeOffset.UtcNow);

        OnPropertyChanged(nameof(HasDailyRateLimitRows));
        OnPropertyChanged(nameof(RateLimitsDailySummaryText));
        OnPropertyChanged(nameof(HasRateLimitsDailyWarning));
        OnPropertyChanged(nameof(RateLimitsDailyWarningText));
    }

    private void RebuildDailyUsageRows()
    {
        DailyUsage.ApplyBuckets(_snapshot.DailyBuckets, Today);
        AccountUsage.ApplySnapshot(_snapshot, DailyUsage.MedianBaseline, Today);

        OnPropertyChanged(nameof(DailyUsageWindowText));
        OnPropertyChanged(nameof(DailyUsageMedianSummaryText));
        OnPropertyChanged(nameof(HasDailyUsageMedianSummary));
    }

    private void RebuildProjectUsageRows()
    {
        ProjectUsage.ApplyRows(_snapshot.ProjectUsageRows);

        OnPropertyChanged(nameof(HasProjectUsage));
        OnPropertyChanged(nameof(ShouldShowProjectUsage));
        OnPropertyChanged(nameof(ProjectUsageEstimateText));
    }

    private void RebuildUsageAttributionRows(DateTimeOffset nowUtc)
    {
        UsageAttribution.ApplySnapshot(_snapshot.UsageAttribution, nowUtc);

        OnPropertyChanged(nameof(HasUsageAttribution));
        OnPropertyChanged(nameof(ShouldShowUsageAttribution));
        OnPropertyChanged(nameof(UsageAttributionSessionRows));
        OnPropertyChanged(nameof(UsageAttributionBurnEventRows));
    }

    private void RefreshUsageAttributionRows(DateTimeOffset nowUtc)
    {
        UsageAttribution.Refresh(nowUtc);

        OnPropertyChanged(nameof(HasUsageAttribution));
        OnPropertyChanged(nameof(ShouldShowUsageAttribution));
    }

    private void UpdateAccountUsageFreshnessWarnings(UsageSnapshot nextSnapshot)
    {
        AccountUsage.EvaluateFreshness(
            _snapshot,
            nextSnapshot,
            Today,
            UseMockMode);

        OnPropertyChanged(nameof(HasDailyUsageFreshnessWarning));
        OnPropertyChanged(nameof(DailyUsageFreshnessWarningText));
        OnPropertyChanged(nameof(HasAccountSummaryFreshnessWarning));
        OnPropertyChanged(nameof(AccountSummaryFreshnessWarningText));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private void ApplyInitialWindowState(PulseMeterWindowState state)
    {
        _expandedWindowWidth = PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowWidth(state.Width);
        _expandedWindowHeight = PulseMeterWindowLayoutCalculator.ShouldUpgradeLegacyReferenceHeight(state)
            ? PulseMeterWindowLayoutCalculator.DefaultExpandedWindowHeight
            : PulseMeterWindowLayoutCalculator.SanitizeExpandedWindowHeight(state.Height);
        if (state.Left is double left && state.Top is double top
            && double.IsFinite(left) && double.IsFinite(top))
        {
            _windowLeft = left;
            _windowTop = top;
        }

        // Launch compact even when the last session ended expanded; keep size for the next expand.
        _isExpanded = false;
    }

    private void RefreshResetCredits(DateTimeOffset nowUtc, bool updateFromSnapshot)
    {
        if (updateFromSnapshot)
        {
            ResetCreditsSection.ApplySnapshot(_snapshot, nowUtc, ShouldPersistResetCreditState());
        }
        else
        {
            ResetCreditsSection.Refresh(nowUtc);
        }

        OnPropertyChanged(nameof(ResetCreditsHeaderText));
        OnPropertyChanged(nameof(ResetCreditsAvailableText));
    }

    private sealed class ZeroUserIdleTimeProvider : IUserIdleTimeProvider
    {
        public TimeSpan GetIdleTime()
        {
            return TimeSpan.Zero;
        }
    }

    private bool ShouldPersistResetCreditState()
    {
        return _snapshot.ResetCreditsAvailable is not null
            && _snapshot.SyncStatus != SyncStatus.Mocked
            && !UseMockMode;
    }

    private void RefreshComputedProperties()
    {
        OnPropertyChanged(nameof(AccountUsageSummaryText));
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(CompactTitleText));
        OnPropertyChanged(nameof(CompactQuotaSummaryText));
        OnPropertyChanged(nameof(ExpandedQuotaSummaryText));
        OnPropertyChanged(nameof(HasStatusMessage));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(ResetCreditsHeaderText));
        OnPropertyChanged(nameof(ResetCreditsAvailableText));
        OnPropertyChanged(nameof(SafeToStartText));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(SyncFeedbackText));
        OnPropertyChanged(nameof(SyncStatusText));
        OnPropertyChanged(nameof(HasThreadContext));
        OnPropertyChanged(nameof(ThreadContextText));
        OnPropertyChanged(nameof(ThreadTokenText));
        OnPropertyChanged(nameof(HasDailyRateLimitRows));
        OnPropertyChanged(nameof(RateLimitsDailySummaryText));
        OnPropertyChanged(nameof(HasRateLimitsDailyWarning));
        OnPropertyChanged(nameof(RateLimitsDailyWarningText));
        OnPropertyChanged(nameof(HasProjectUsage));
        OnPropertyChanged(nameof(ShouldShowProjectUsage));
        OnPropertyChanged(nameof(ProjectUsageEstimateText));
        OnPropertyChanged(nameof(HasUsageAttribution));
        OnPropertyChanged(nameof(ShouldShowUsageAttribution));
        RefreshTopChromeViewModels();
        RefreshAccountDashboardProperties();
        OnPropertyChanged(nameof(TodayUsageText));
        OnPropertyChanged(nameof(TodayUsageValueText));
    }

    private static UsageSignalsSnapshot MergeSignals(
        UsageSignalsSnapshot usageSignals,
        IReadOnlyList<UsageAttentionSignal> budgetSignals)
    {
        if (budgetSignals.Count == 0)
        {
            return usageSignals;
        }

        return new UsageSignalsSnapshot
        {
            RunwaySignals = usageSignals.RunwaySignals,
            IdleDrainIncident = usageSignals.IdleDrainIncident,
            ShowAllAttentionSignals = usageSignals.ShowAllAttentionSignals,
            AttentionSignals = usageSignals.AttentionSignals
                .Concat(budgetSignals)
                .OrderBy(signal => signal.Priority)
                .ToList()
        };
    }

    private void RefreshAccountDashboardProperties()
    {
        AccountUsage.RefreshDisplayProperties();
        OnPropertyChanged(nameof(DailyUsageWindowText));
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
    }

    private void SetSyncFeedback(string text)
    {
        if (_syncFeedbackText == text)
        {
            return;
        }

        _syncFeedbackText = text;
        OnPropertyChanged(nameof(HasSyncFeedback));
        OnPropertyChanged(nameof(SyncFeedbackText));
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
