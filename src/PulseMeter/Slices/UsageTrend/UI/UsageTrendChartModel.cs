namespace PulseMeter.Slices.UsageTrend.UI;

/// <summary>Chooses the emphasis of a usage trend chart.</summary>
public enum UsageTrendChartMode
{
    UsageTrend,
    RunwayForecast,
    PaceAnalysis
}

/// <summary>One measured usage value in the chart timeline.</summary>
public sealed record UsageTrendPoint(DateTimeOffset Timestamp, double UsedPercent);

/// <summary>A period between recorded endpoints during which PulseMeter was not measuring.</summary>
public sealed record UsageTrendGap(DateTimeOffset StartedAt, DateTimeOffset EndedAt);

/// <summary>One lower/upper typical-usage interval in the chart timeline.</summary>
public sealed record UsageTrendBandPoint(DateTimeOffset Timestamp, double LowerPercent, double UpperPercent);

/// <summary>A single earlier forecast retained for comparison during the active quota window.</summary>
public sealed record UsageTrendForecastReference(
    DateTimeOffset CapturedAt,
    DateTimeOffset ResetAt,
    IReadOnlyList<UsageTrendPoint> ProjectedPoints);

/// <summary>The part of the actual trajectory that ran materially above an earlier forecast.</summary>
public sealed record UsageTrendVarianceSegment(UsageTrendPoint Start, UsageTrendPoint End);

/// <summary>Adaptive pace change relative to the median baseline for the selected quota window.</summary>
public sealed record UsageMomentumSummary(
    string ValueText,
    string StateText,
    string BaselineText,
    double GaugeValue)
{
    /// <summary>True until enough comparable history exists for a measured momentum result.</summary>
    public bool IsLearning { get; init; }

    /// <summary>Baseline evidence collected so far, normalized to the selected window's target.</summary>
    public double BaselineProgress { get; init; } = 1;

    /// <summary>Plain-language description exposed to assistive technology.</summary>
    public string AccessibleSummary { get; init; } = string.Empty;
}

/// <summary>Decision-oriented copy and metrics shown above the usage chart.</summary>
public sealed record UsageTrendRunwaySummary(
    string Headline,
    string ForecastLeadText,
    string ForecastWhenText,
    string ConfidenceText,
    string UsedPercentText,
    UsageMomentumSummary Momentum,
    string CurrentPaceText,
    string SustainablePaceText,
    string PaceComparisonText,
    string PaceComparisonLabel,
    string RecommendationText,
    bool CanOpenPacingPlan);

/// <summary>A forecast-only assessment of whether a selected coding block is likely to fit at the current pace.</summary>
public enum UsageTrendBlockAdvisorStatus
{
    StillLearning,
    LikelyFits,
    MayBeInterrupted,
    UnlikelyToFit,
    WaitForReset
}

/// <summary>A forecast-only assessment of whether a selected coding block is likely to fit at the current pace.</summary>
public sealed record UsageTrendBlockAdvisor(
    string State,
    string Detail,
    string AccessibleSummary,
    IReadOnlyList<UsageTrendBlockOption> Options,
    UsageTrendBlockAdvisorStatus Status);

/// <summary>One compact duration choice in the next-block advisor.</summary>
public sealed record UsageTrendBlockOption(
    int DurationMinutes,
    string Label,
    bool IsSelected,
    string AutomationName,
    string AutomationHelpText);

/// <summary>The earliest reliable constraint across the active five-hour and seven-day limits.</summary>
public sealed record UsageTrendNextConstraint(
    string Headline,
    string Detail,
    string AccessibleSummary);

/// <summary>Immutable data used to render a <see cref="UsageTrendChart"/>.</summary>
public sealed record UsageTrendChartModel(
    IReadOnlyList<UsageTrendPoint> ActualPoints,
    IReadOnlyList<UsageTrendPoint> ProjectedPoints,
    IReadOnlyList<UsageTrendPoint> SustainablePoints,
    IReadOnlyList<UsageTrendBandPoint> TypicalRange,
    DateTimeOffset RangeStart,
    DateTimeOffset RangeEnd,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset ResetAt,
    DateTimeOffset? ForecastWindowStart,
    DateTimeOffset? ForecastWindowEnd,
    DateTimeOffset? ForecastLimitAt,
    UsageTrendChartMode Mode,
    bool ShowProjection,
    bool ShowRange,
    UsageTrendRunwaySummary Summary,
    string AccessibleSummary)
{
    public IReadOnlyList<UsageTrendPoint> ReferenceProjectedPoints { get; init; } = [];

    public IReadOnlyList<UsageTrendVarianceSegment> UnfavorableVarianceSegments { get; init; } = [];

    public IReadOnlyList<UsageTrendGap> MeasurementGaps { get; init; } = [];

    public DateTimeOffset? ReferenceForecastCapturedAt { get; init; }

    public UsageTrendBlockAdvisor? BlockAdvisor { get; init; }

    public UsageTrendNextConstraint? NextConstraint { get; init; }
}
