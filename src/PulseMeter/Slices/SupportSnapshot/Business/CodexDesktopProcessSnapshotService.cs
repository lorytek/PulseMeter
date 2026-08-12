using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using PulseMeter.Slices.SupportSnapshot.Models;

namespace PulseMeter.Slices.SupportSnapshot.Business;

/// <summary>Aggregate-safe per-candidate outcome. It is not a process identity.</summary>
public enum CodexDesktopProcessProbeVerdict { Verified, Indeterminate }

public sealed record CodexDesktopProcessProbeRecord(CodexDesktopProcessProbeVerdict Verdict, long? WorkingSetBytes);

public enum CodexDesktopProcessProbeSource { Succeeded, Unavailable }

/// <summary>Typed source result; unavailable means enumeration itself could not be completed.</summary>
public sealed record CodexDesktopProcessProbeResult(
    CodexDesktopProcessProbeSource Source,
    IReadOnlyList<CodexDesktopProcessProbeRecord> Records)
{
    public static CodexDesktopProcessProbeResult Unavailable { get; } = new(CodexDesktopProcessProbeSource.Unavailable, Array.Empty<CodexDesktopProcessProbeRecord>());
}

/// <summary>On-demand port only. It has no timer, startup, persistence, network, or process-control operation.</summary>
public interface ICodexDesktopProcessSnapshotProbe
{
    CodexDesktopProcessProbeResult Capture(CancellationToken cancellationToken);
}

public interface ICodexDesktopProcessSnapshotService
{
    Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexDesktopProcessSnapshotService : ICodexDesktopProcessSnapshotService
{
    private readonly ICodexDesktopProcessSnapshotProbe _probe;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _measurementGate = new(1, 1);

    public CodexDesktopProcessSnapshotService(ICodexDesktopProcessSnapshotProbe probe, Func<DateTimeOffset>? utcNow = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public CodexDesktopProcessSnapshot Capture(CancellationToken cancellationToken = default)
    {
        _measurementGate.Wait(cancellationToken);
        try
        {
            return CaptureCore(cancellationToken);
        }
        finally
        {
            _measurementGate.Release();
        }
    }

    /// <summary>Runs the explicit native measurement away from the WPF dispatcher; this does not schedule polling.</summary>
    public async Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        await _measurementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => CaptureCore(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _measurementGate.Release();
        }
    }

    private CodexDesktopProcessSnapshot CaptureCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var probeResult = _probe.Capture(cancellationToken) ?? throw new InvalidOperationException("The process probe returned no result.");
        cancellationToken.ThrowIfCancellationRequested();

        if (probeResult.Source == CodexDesktopProcessProbeSource.Unavailable)
        {
            if (probeResult.Records.Count != 0)
            {
                throw new InvalidOperationException("An unavailable process probe cannot publish candidate records.");
            }

            return NewSnapshot(CodexDesktopProcessSnapshotStatus.Unavailable, 0, null, 0);
        }

        if (probeResult.Source != CodexDesktopProcessProbeSource.Succeeded)
        {
            throw new InvalidOperationException("Unknown process probe source state.");
        }

        var verified = 0;
        var indeterminate = 0;
        long sum = 0;
        var hasSum = true;
        var aggregateOverflow = false;
        foreach (var record in probeResult.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record is null || record.Verdict == CodexDesktopProcessProbeVerdict.Indeterminate)
            {
                indeterminate = IncrementBounded(indeterminate);
                continue;
            }

            if (record.Verdict != CodexDesktopProcessProbeVerdict.Verified || record.WorkingSetBytes is not long bytes || bytes < 0)
            {
                indeterminate = IncrementBounded(indeterminate);
                continue;
            }

            verified = IncrementBounded(verified);
            if (!hasSum)
            {
                continue;
            }

            try { sum = checked(sum + bytes); }
            catch (OverflowException)
            {
                hasSum = false;
                aggregateOverflow = true;
            }
        }

        var status = indeterminate > 0 || aggregateOverflow ? CodexDesktopProcessSnapshotStatus.Partial
            : verified > 0 ? CodexDesktopProcessSnapshotStatus.Complete
            : CodexDesktopProcessSnapshotStatus.NoVerified;
        return NewSnapshot(status, verified, verified > 0 && hasSum ? sum : null, indeterminate);
    }

    private CodexDesktopProcessSnapshot NewSnapshot(CodexDesktopProcessSnapshotStatus status, int verified, long? sum, int indeterminate)
        => new(CodexDesktopProcessSnapshot.CurrentSchemaVersion, _utcNow().ToUniversalTime(), status, verified, sum, true, indeterminate, CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);

    private static int IncrementBounded(int value) => value == int.MaxValue ? int.MaxValue : value + 1;
}

public static class CodexDesktopProcessSnapshotFormatter
{
    public static string Format(CodexDesktopProcessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", CodexDesktopProcessSnapshot.CurrentSchemaVersion);
            writer.WriteString("captured_at_utc", snapshot.CapturedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("status", StatusText(snapshot.Status));
            writer.WriteNumber("verified_process_count", snapshot.VerifiedProcessCount);
            if (snapshot.SummedWorkingSetBytes is long sum) writer.WriteNumber("summed_working_set_bytes", sum); else writer.WriteNull("summed_working_set_bytes");
            writer.WriteBoolean("shared_pages_may_overlap", true);
            writer.WriteNumber("unavailable_or_changed_count", snapshot.UnavailableOrChangedCount);
            writer.WriteString("identity_rule_version", CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void Validate(CodexDesktopProcessSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != CodexDesktopProcessSnapshot.CurrentSchemaVersion
            || snapshot.IdentityRuleVersion != CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion
            || !snapshot.SharedPagesMayOverlap
            || snapshot.CapturedAtUtc.Offset != TimeSpan.Zero
            || snapshot.VerifiedProcessCount < 0
            || snapshot.UnavailableOrChangedCount < 0
            || snapshot.SummedWorkingSetBytes < 0)
        {
            throw new ArgumentException("The snapshot contradicts the schema-v1 contract.", nameof(snapshot));
        }

        var valid = snapshot.Status switch
        {
            CodexDesktopProcessSnapshotStatus.Unavailable => snapshot.VerifiedProcessCount == 0 && snapshot.UnavailableOrChangedCount == 0 && snapshot.SummedWorkingSetBytes is null,
            CodexDesktopProcessSnapshotStatus.NoVerified => snapshot.VerifiedProcessCount == 0 && snapshot.UnavailableOrChangedCount == 0 && snapshot.SummedWorkingSetBytes is null,
            CodexDesktopProcessSnapshotStatus.Complete => snapshot.VerifiedProcessCount > 0 && snapshot.UnavailableOrChangedCount == 0 && snapshot.SummedWorkingSetBytes is not null,
            CodexDesktopProcessSnapshotStatus.Partial =>
                (snapshot.UnavailableOrChangedCount > 0 && (snapshot.VerifiedProcessCount > 0 || snapshot.SummedWorkingSetBytes is null))
                || (snapshot.UnavailableOrChangedCount == 0 && snapshot.VerifiedProcessCount > 0 && snapshot.SummedWorkingSetBytes is null),
            _ => false
        };
        if (!valid) throw new ArgumentException("The snapshot status contradicts its aggregate counts.", nameof(snapshot));
    }

    private static string StatusText(CodexDesktopProcessSnapshotStatus status) => status switch
    {
        CodexDesktopProcessSnapshotStatus.Complete => "complete",
        CodexDesktopProcessSnapshotStatus.Partial => "partial",
        CodexDesktopProcessSnapshotStatus.NoVerified => "no_verified",
        CodexDesktopProcessSnapshotStatus.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
