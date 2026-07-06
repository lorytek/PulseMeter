namespace PulseMeter.Slices.UsageCollection.Models;

public sealed class RateLimitBucket
{
    public string? LimitId { get; init; }

    public string? LimitName { get; init; }

    public double? UsedPercent { get; init; }

    public int? WindowDurationMins { get; init; }

    public long? ResetsAtUnixSeconds { get; init; }

    public DateTimeOffset? ResetsAtUtc { get; init; }

    public string? RateLimitReachedType { get; init; }

    public string GroupLabel { get; init; } = "Usage";

    public string WindowLabel { get; init; } = "Usage";

    public string Label { get; init; } = "Usage";

    public string ResetCountdown { get; init; } = "reset unknown";

    public bool IsReached => !string.IsNullOrWhiteSpace(RateLimitReachedType);

    public double PercentValue => Math.Clamp(UsedPercent ?? 0, 0, 100);

    public double RemainingPercentValue => UsedPercent is double value ? Math.Clamp(100 - value, 0, 100) : 0;

    public string RemainingPercentText => UsedPercent is double ? $"{RemainingPercentValue:0}% left" : "usage unknown";

    public string PercentText => RemainingPercentText;

    public string ResetText => ResetCountdown == "reset unknown" ? ResetCountdown : $"resets in {ResetCountdown}";
}
