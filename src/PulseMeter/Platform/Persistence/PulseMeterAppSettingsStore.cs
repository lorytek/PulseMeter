using System.IO;
using System.Text.Json;

namespace PulseMeter.Platform.Persistence;

public sealed record PulseMeterAppSettings(
    int AutoSyncSeconds = 90,
    bool IsAlwaysOnTop = false,
    DashboardVisibilitySettings? DashboardVisibility = null,
    string? SelectedRateLimitKey = null,
    bool IsNavigationPanelExpanded = true,
    IReadOnlyList<RecoveryWatchSettings>? RecoveryWatches = null,
    bool AutoShowWhenCodexFocused = true,
    bool AutoHideWhenFocusLeaves = false);

/// <summary>A one-shot next-block watch, scoped to a stable rate-limit window.</summary>
public sealed record RecoveryWatchSettings(
    string LimitKey,
    int WindowDurationMins,
    int BlockDurationMinutes,
    DateTimeOffset ResetAtUtc);

public sealed record DashboardVisibilitySettings(
    bool RateLimits = true,
    bool WeeklyPace = true,
    bool RunwayForecast = true,
    bool ResetCredits = true,
    bool AccountUsage = true,
    bool ProjectUsage = true,
    bool BurnAnalysis = true,
    bool DailyUsage = true,
    bool BlockPlanner = true);

public sealed record BudgetAlertSettings(
    bool IsEnabled = true,
    long? DailyTokenBudget = null,
    int WarningPercent = 75,
    int CriticalPercent = 90)
{
    public static BudgetAlertSettings Default { get; } = new();

    public BudgetAlertSettings Sanitized()
    {
        var warning = Math.Clamp(WarningPercent, 1, 99);
        var critical = Math.Clamp(CriticalPercent, warning + 1, 100);
        long? dailyBudget = DailyTokenBudget is long value && value > 0 ? value : null;

        return this with
        {
            DailyTokenBudget = dailyBudget,
            WarningPercent = warning,
            CriticalPercent = critical
        };
    }
}

public interface IPulseMeterAppSettingsStore
{
    PulseMeterAppSettings? Load();

    bool Save(PulseMeterAppSettings settings);
}

public sealed class PulseMeterAppSettingsStore : IPulseMeterAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public PulseMeterAppSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseMeter",
            "settings.json");
    }

    public PulseMeterAppSettings? Load()
    {
        return AtomicJsonFileStore.Load<PulseMeterAppSettings>(_filePath, JsonOptions);
    }

    public bool Save(PulseMeterAppSettings settings)
    {
        return AtomicJsonFileStore.Save(_filePath, settings, JsonOptions);
    }
}
