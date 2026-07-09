namespace PulseMeter.Slices.UsageSignals.Models;

public sealed class UsageSignalsSnapshot
{
    public static UsageSignalsSnapshot Empty { get; } = new();

    public IReadOnlyList<LimitRunwaySignal> RunwaySignals { get; init; } = Array.Empty<LimitRunwaySignal>();

    public IdleDrainIncident? IdleDrainIncident { get; init; }

    public IReadOnlyList<UsageAttentionSignal> AttentionSignals { get; init; } = Array.Empty<UsageAttentionSignal>();

    public bool ShowAllAttentionSignals { get; init; }

    public bool HasSignals => RunwaySignals.Count > 0 || IdleDrainIncident is not null || AttentionSignals.Count > 0;
}
