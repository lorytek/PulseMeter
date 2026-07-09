namespace PulseMeter.Slices.BudgetAlerts.Models;

public sealed record BudgetAlertDisplayRow(
    string Key,
    string Label,
    string DetailText,
    string LevelText,
    string AccentBrush,
    double PercentValue);
