namespace PulseMeter.Slices.UsageCollection.Models;

public sealed record ResetCreditSnapshot(
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? Status);
