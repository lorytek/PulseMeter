using System.Globalization;
using PulseMeter.Platform.Persistence;
using PulseMeter.Shared.Formatting;
using PulseMeter.Shared.RateLimits;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.BudgetAlerts.Business;

public interface IBudgetAlertTracker
{
    BudgetAlertsSnapshot Observe(UsageSnapshot snapshot, BudgetAlertSettings settings, DateTimeOffset now);
}

public sealed class BudgetAlertTracker : IBudgetAlertTracker
{
    public BudgetAlertsSnapshot Observe(UsageSnapshot snapshot, BudgetAlertSettings settings, DateTimeOffset now)
    {
        var sanitized = settings.Sanitized();
        if (!sanitized.IsEnabled || !ShouldEvaluate(snapshot))
        {
            return BudgetAlertsSnapshot.Empty;
        }

        var rows = new List<BudgetAlertDisplayRow>();
        var signals = new List<UsageAttentionSignal>();

        AddDailyBudget(snapshot, sanitized, now, rows, signals);
        AddRateLimitBudgets(snapshot, sanitized, rows, signals);

        return new BudgetAlertsSnapshot
        {
            Rows = rows,
            AttentionSignals = signals
        };
    }

    private void AddDailyBudget(
        UsageSnapshot snapshot,
        BudgetAlertSettings settings,
        DateTimeOffset now,
        List<BudgetAlertDisplayRow> rows,
        List<UsageAttentionSignal> signals)
    {
        if (settings.DailyTokenBudget is not long dailyBudget)
        {
            return;
        }

        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var todayTokens = snapshot.DailyBuckets
            .Where(bucket => DateOnly.TryParse(bucket.StartDate, out var bucketDate) && bucketDate == today)
            .Sum(bucket => bucket.TotalTokens ?? 0);
        var percent = dailyBudget <= 0 ? 0 : todayTokens / (double)dailyBudget * 100;
        var level = LevelFor(percent, settings);
        if (level == BudgetAlertLevel.Normal)
        {
            return;
        }

        var detail = $"{FormatTokenDetail(todayTokens)} of {MeterDisplayFormatter.FormatTokens(dailyBudget)} daily token budget used ({percent:0}%).";
        var title = level == BudgetAlertLevel.Critical
            ? "Daily token budget is critical"
            : "Daily token budget warning";
        var accent = AccentFor(level);

        rows.Add(new BudgetAlertDisplayRow(
            "daily-token-budget",
            "Daily token budget",
            detail,
            LevelText(level),
            accent,
            percent));
        signals.Add(new UsageAttentionSignal(
            2,
            "BUDGET",
            title,
            detail,
            accent));

    }

    private void AddRateLimitBudgets(
        UsageSnapshot snapshot,
        BudgetAlertSettings settings,
        List<BudgetAlertDisplayRow> rows,
        List<UsageAttentionSignal> signals)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            if (bucket.UsedPercent is not double usedPercent)
            {
                continue;
            }

            var level = LevelFor(usedPercent, settings);
            if (level == BudgetAlertLevel.Normal)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(bucket.Label) ? bucket.WindowLabel : bucket.Label;
            var detail = bucket.ResetCountdown == "reset unknown"
                ? $"{label} is {usedPercent:0}% used."
                : $"{label} is {usedPercent:0}% used; {bucket.ResetText}.";
            var title = level == BudgetAlertLevel.Critical
                ? "Rate limit budget is critical"
                : "Rate limit budget warning";
            var accent = AccentFor(level);
            var key = BuildRateLimitRowKey(bucket, level);

            rows.Add(new BudgetAlertDisplayRow(
                key,
                label,
                detail,
                LevelText(level),
                accent,
                usedPercent));
            signals.Add(new UsageAttentionSignal(
                level == BudgetAlertLevel.Critical ? 2 : 3,
                "BUDGET",
                title,
                detail,
                accent));

        }
    }

    private static bool ShouldEvaluate(UsageSnapshot snapshot)
    {
        return snapshot.SyncStatus is SyncStatus.Live or SyncStatus.Mocked;
    }

    private static BudgetAlertLevel LevelFor(double percent, BudgetAlertSettings settings)
    {
        if (percent >= settings.CriticalPercent)
        {
            return BudgetAlertLevel.Critical;
        }

        return percent >= settings.WarningPercent
            ? BudgetAlertLevel.Warning
            : BudgetAlertLevel.Normal;
    }

    private static string LevelText(BudgetAlertLevel level)
    {
        return level switch
        {
            BudgetAlertLevel.Critical => "Critical",
            BudgetAlertLevel.Warning => "Warning",
            _ => "Normal"
        };
    }

    private static string AccentFor(BudgetAlertLevel level)
    {
        return level == BudgetAlertLevel.Critical ? "#EF4444" : "#F97316";
    }

    private static string BuildRateLimitRowKey(RateLimitBucket bucket, BudgetAlertLevel level)
    {
        var window = bucket.WindowDurationMins?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        var reset = bucket.ResetsAtUnixSeconds?.ToString(CultureInfo.InvariantCulture)
            ?? bucket.ResetsAtUtc?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            ?? "unknown";

        return $"rate|{RateLimitBucketKeys.Get(bucket)}|{window}|{reset}|{level}";
    }

    private static string FormatTokenDetail(long tokens)
    {
        return tokens < 1_000
            ? $"{tokens.ToString("N0", CultureInfo.InvariantCulture)} tokens"
            : $"{MeterDisplayFormatter.FormatTokens(tokens)} tokens";
    }
}
