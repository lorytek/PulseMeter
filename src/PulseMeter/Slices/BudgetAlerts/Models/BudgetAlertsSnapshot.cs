namespace PulseMeter.Slices.BudgetAlerts.Models;

public sealed class BudgetAlertsSnapshot
{
    public static BudgetAlertsSnapshot Empty { get; } = new();

    public IReadOnlyList<BudgetAlertDisplayRow> Rows { get; init; } = Array.Empty<BudgetAlertDisplayRow>();

    public IReadOnlyList<UsageAttentionSignal> AttentionSignals { get; init; } = Array.Empty<UsageAttentionSignal>();

    public bool HasAlerts => Rows.Count > 0;
}
