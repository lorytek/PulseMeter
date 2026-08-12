namespace PulseMeter.Slices.SupportSnapshot.Models;

/// <summary>Closed health-neutral vocabulary for one explicitly requested measurement.</summary>
public enum CodexDesktopProcessSnapshotStatus
{
    Complete,
    Partial,
    NoVerified,
    Unavailable
}

/// <summary>
/// Schema v1 aggregate-only result. This public type intentionally has no identifiers, paths,
/// package metadata, signer data, process-tree data, or native exception details.
/// </summary>
public sealed record CodexDesktopProcessSnapshot(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    CodexDesktopProcessSnapshotStatus Status,
    int VerifiedProcessCount,
    long? SummedWorkingSetBytes,
    bool SharedPagesMayOverlap,
    int UnavailableOrChangedCount,
    string IdentityRuleVersion)
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentIdentityRuleVersion = "openai_codex_package_resource_v1";
}
