using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using PulseMeter.Slices.SupportSnapshot.Business;

[assembly: InternalsVisibleTo("PulseMeter.Tests")]

namespace PulseMeter.Platform.Windows;

/// <summary>On-demand Windows probe. Public output is aggregate-safe only.</summary>
public sealed class CodexDesktopProcessSnapshotProbe : ICodexDesktopProcessSnapshotProbe
{
    private readonly IWindowsCodexDesktopNativeAdapter _native;

    public CodexDesktopProcessSnapshotProbe() : this(new WindowsCodexDesktopNativeAdapter()) { }

    internal CodexDesktopProcessSnapshotProbe(IWindowsCodexDesktopNativeAdapter native) => _native = native ?? throw new ArgumentNullException(nameof(native));

    public CodexDesktopProcessProbeResult Capture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var enumeration = _native.Enumerate(cancellationToken);
        if (enumeration.Source == NativeEnumerationSource.Unavailable)
        {
            return CodexDesktopProcessProbeResult.Unavailable;
        }

        var records = new List<CodexDesktopProcessProbeRecord>();
        var seen = new HashSet<(int ProcessId, long CreationTicks)>();
        foreach (var evidence in enumeration.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add((evidence.CandidateProcessId, evidence.CandidateCreationUtcTicks)))
            {
                continue;
            }

            var decision = CodexDesktopProcessIdentityEvaluator.Evaluate(evidence);
            if (decision == NativeCandidateDecision.Verified)
            {
                records.Add(new CodexDesktopProcessProbeRecord(CodexDesktopProcessProbeVerdict.Verified, evidence.WorkingSetBytes));
            }
            else if (decision == NativeCandidateDecision.Indeterminate)
            {
                records.Add(new CodexDesktopProcessProbeRecord(CodexDesktopProcessProbeVerdict.Indeterminate, null));
            }
        }

        return new CodexDesktopProcessProbeResult(CodexDesktopProcessProbeSource.Succeeded, records);
    }
}

internal interface IWindowsCodexDesktopNativeAdapter
{
    NativeCandidateEnumeration Enumerate(CancellationToken cancellationToken);
}

internal enum NativeEnumerationSource { Succeeded, Unavailable }

internal sealed record NativeCandidateEnumeration(NativeEnumerationSource Source, IReadOnlyList<NativeCandidateEvidence> Candidates)
{
    public static NativeCandidateEnumeration Unavailable { get; } = new(NativeEnumerationSource.Unavailable, Array.Empty<NativeCandidateEvidence>());
}

internal enum NativePackageState { Available, NotPackaged, Unavailable }

internal enum NativePackageOrigin { Store, NonStore, Unavailable }
internal sealed record NativePackageEvidence(NativePackageState State, NativePackageOrigin Origin, string? FullName, string? Family, string? Name, string? Publisher, string? PublisherId, string? Root)
{
    public static NativePackageEvidence Unavailable { get; } = new(NativePackageState.Unavailable, NativePackageOrigin.Unavailable, null, null, null, null, null, null);
    public static NativePackageEvidence NotPackaged { get; } = new(NativePackageState.NotPackaged, NativePackageOrigin.Unavailable, null, null, null, null, null, null);
}

/// <summary>Internal-only raw evidence used by the native adapter and deterministic friend-assembly tests.</summary>
internal sealed record NativeCandidateEvidence(
    int CandidateProcessId,
    long CandidateCreationUtcTicks,
    string? CandidateCanonicalPath,
    NativePackageEvidence CandidatePackage,
    int ParentProcessId,
    long ParentCreationUtcTicks,
    string? ParentCanonicalPath,
    NativePackageEvidence ParentPackage,
    bool InspectionIndeterminate,
    bool CandidateIdentityStable,
    bool ImmediateParentRechecked,
    NativeTrustState CandidateTrust,
    NativeTrustState ParentTrust,
    long? WorkingSetBytes);

internal enum NativeCandidateDecision { Ignored, Indeterminate, Verified }
internal enum NativeTrustState { Trusted, Untrusted, Unavailable }

internal static class NativeTrustClassifier
{
    public static NativeTrustState Classify(uint status) => status switch
    {
        0 => NativeTrustState.Trusted,
        0x800B0100 or 0x80096010 or 0x800B0111 or 0x800B0109 or 0x800B010C or 0x800B0101 or 0x800B010A or 0x800B0004 => NativeTrustState.Untrusted,
        _ => NativeTrustState.Unavailable
    };
}

internal static class NativePackageOriginClassifier
{
    public static NativePackageOrigin Classify(uint origin) => origin switch
    {
        3 => NativePackageOrigin.Store,
        1 or 2 or 4 or 5 or 6 => NativePackageOrigin.NonStore,
        _ => NativePackageOrigin.Unavailable
    };
}

internal static class CodexDesktopProcessIdentityEvaluator
{
    internal const string PackageName = "OpenAI.Codex";
    internal const string PackageFamily = "OpenAI.Codex_2p2nqsd0c76g0";
    internal const string PublisherId = "2p2nqsd0c76g0";
    internal const string PackagePublisher = "CN=50BDFD77-8903-4850-9FFE-6E8522F64D5B";

    public static NativeCandidateDecision Evaluate(NativeCandidateEvidence evidence)
    {
        var candidatePackageState = MatchRequiredPackage(evidence.CandidatePackage);
        var parentPackageState = MatchRequiredPackage(evidence.ParentPackage);
        var pathState = MatchRequiredPaths(evidence);
        if (candidatePackageState == NativeCandidateDecision.Ignored || parentPackageState == NativeCandidateDecision.Ignored
            || pathState == NativeCandidateDecision.Ignored || evidence.CandidateTrust == NativeTrustState.Untrusted || evidence.ParentTrust == NativeTrustState.Untrusted) return NativeCandidateDecision.Ignored;
        if (evidence.InspectionIndeterminate || !evidence.CandidateIdentityStable || !evidence.ImmediateParentRechecked || evidence.ParentCreationUtcTicks > evidence.CandidateCreationUtcTicks
            || candidatePackageState == NativeCandidateDecision.Indeterminate || parentPackageState == NativeCandidateDecision.Indeterminate
            || pathState == NativeCandidateDecision.Indeterminate || evidence.CandidateTrust == NativeTrustState.Unavailable || evidence.ParentTrust == NativeTrustState.Unavailable) return NativeCandidateDecision.Indeterminate;

        return evidence.WorkingSetBytes is >= 0 ? NativeCandidateDecision.Verified : NativeCandidateDecision.Indeterminate;
    }

    public static bool IsEligibleBeforeSignature(NativeCandidateEvidence evidence)
        => !evidence.InspectionIndeterminate
            && evidence.CandidateIdentityStable
            && evidence.ImmediateParentRechecked
            && evidence.ParentCreationUtcTicks <= evidence.CandidateCreationUtcTicks
            && MatchRequiredPackage(evidence.CandidatePackage) == NativeCandidateDecision.Verified
            && MatchRequiredPackage(evidence.ParentPackage) == NativeCandidateDecision.Verified
            && MatchRequiredPaths(evidence) == NativeCandidateDecision.Verified;

    private static NativeCandidateDecision MatchRequiredPackage(NativePackageEvidence package)
    {
        if (package.State == NativePackageState.NotPackaged) return NativeCandidateDecision.Ignored;
        if (package.State == NativePackageState.Unavailable || package.Origin == NativePackageOrigin.Unavailable) return NativeCandidateDecision.Indeterminate;
        if (package.Origin != NativePackageOrigin.Store) return NativeCandidateDecision.Ignored;
        return MatchesRequiredPackage(package) ? NativeCandidateDecision.Verified : NativeCandidateDecision.Ignored;
    }

    private static bool MatchesRequiredPackage(NativePackageEvidence package)
        => package.State == NativePackageState.Available
            && string.Equals(package.Name, PackageName, StringComparison.Ordinal)
            && string.Equals(package.Family, PackageFamily, StringComparison.Ordinal)
            && string.Equals(package.PublisherId, PublisherId, StringComparison.Ordinal)
            && string.Equals(package.Publisher, PackagePublisher, StringComparison.Ordinal);

    private static NativeCandidateDecision MatchRequiredPaths(NativeCandidateEvidence evidence)
    {
        var candidateAvailable = evidence.CandidateCanonicalPath is not null && evidence.CandidatePackage.Root is not null;
        var parentAvailable = evidence.ParentCanonicalPath is not null && evidence.ParentPackage.Root is not null;

        if (candidateAvailable)
        {
            var candidateRelative = Path.GetRelativePath(evidence.CandidatePackage.Root!, evidence.CandidateCanonicalPath!);
            if (!string.Equals(candidateRelative, Path.Combine("resources", "codex.exe"), StringComparison.OrdinalIgnoreCase)) return NativeCandidateDecision.Ignored;
        }

        if (parentAvailable)
        {
            var parentRelative = Path.GetRelativePath(evidence.ParentPackage.Root!, evidence.ParentCanonicalPath!);
            if (!IsInsideRoot(parentRelative)) return NativeCandidateDecision.Ignored;
        }

        if (evidence.CandidatePackage.Root is not null && evidence.ParentPackage.Root is not null
            && !string.Equals(evidence.CandidatePackage.Root, evidence.ParentPackage.Root, StringComparison.OrdinalIgnoreCase)) return NativeCandidateDecision.Ignored;

        return candidateAvailable && parentAvailable ? NativeCandidateDecision.Verified : NativeCandidateDecision.Indeterminate;
    }

    private static bool IsInsideRoot(string relative)
        => !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}

internal delegate uint PackageStringReader(ref int length, StringBuilder? buffer);

internal enum PackageStringReadState { Available, NotPackaged, Unavailable }

internal readonly record struct PackageStringRead(PackageStringReadState State, string? Value)
{
    public static PackageStringRead NotPackaged { get; } = new(PackageStringReadState.NotPackaged, null);
    public static PackageStringRead Unavailable { get; } = new(PackageStringReadState.Unavailable, null);
}

internal static class PackageStringReadHelper
{
    internal const uint ErrorInsufficientBuffer = 122;
    internal const uint AppModelErrorNoPackage = 15700;
    internal const int MaximumCharacters = 32768;

    public static PackageStringRead Read(PackageStringReader reader)
    {
        var length = 0;
        var result = reader(ref length, null);
        if (result == AppModelErrorNoPackage) return PackageStringRead.NotPackaged;
        if (result != ErrorInsufficientBuffer || length <= 0 || length > MaximumCharacters) return PackageStringRead.Unavailable;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var capacity = length;
            var buffer = new StringBuilder(capacity);
            var requestedLength = capacity;
            result = reader(ref requestedLength, buffer);
            if (result == 0)
            {
                return requestedLength > capacity ? PackageStringRead.Unavailable : new PackageStringRead(PackageStringReadState.Available, buffer.ToString());
            }
            if (result != ErrorInsufficientBuffer || requestedLength <= capacity || requestedLength > MaximumCharacters) return PackageStringRead.Unavailable;
            length = requestedLength;
        }

        return PackageStringRead.Unavailable;
    }
}

internal static class PackageIdParser
{
    public static (string Name, string Publisher, string PublisherId)? Parse(IntPtr buffer, int bufferLength)
    {
        var headerSize = Marshal.SizeOf<WindowsCodexDesktopNativeAdapter.PackageId>();
        if (buffer == IntPtr.Zero || bufferLength < headerSize) return null;
        var id = Marshal.PtrToStructure<WindowsCodexDesktopNativeAdapter.PackageId>(buffer);
        var name = ReadBoundedUnicode(buffer, bufferLength, headerSize, id.Name, required: true);
        var publisher = ReadBoundedUnicode(buffer, bufferLength, headerSize, id.Publisher, required: true);
        var resourceId = ReadBoundedUnicode(buffer, bufferLength, headerSize, id.ResourceId, required: false);
        var publisherId = ReadBoundedUnicode(buffer, bufferLength, headerSize, id.PublisherId, required: true);
        return name is null || publisher is null || publisherId is null || resourceId == InvalidOptionalString ? null : (name, publisher, publisherId);
    }

    private const string InvalidOptionalString = "\u0001";

    private static string? ReadBoundedUnicode(IntPtr buffer, int bufferLength, int headerSize, IntPtr value, bool required)
    {
        if (value == IntPtr.Zero) return required ? null : string.Empty;
        var start = unchecked((ulong)buffer.ToInt64());
        var address = unchecked((ulong)value.ToInt64());
        var end = start + (uint)bufferLength;
        var stringsStart = start + (uint)headerSize;
        if (stringsStart < start || end < start || address < stringsStart || address >= end || ((address - start) & 1) != 0) return required ? null : InvalidOptionalString;
        var availableBytes = end - address;
        if (availableBytes < 2) return required ? null : InvalidOptionalString;
        var characters = checked((int)(availableBytes / 2));
        for (var index = 0; index < characters; index++)
        {
            if (Marshal.ReadInt16(value, index * 2) == 0)
            {
                return Marshal.PtrToStringUni(value, index) ?? string.Empty;
            }
        }

        return required ? null : InvalidOptionalString;
    }
}

internal delegate uint PackageIdReader(ref int length, IntPtr buffer);

internal sealed class OwnedPackageIdBuffer : IDisposable
{
    public OwnedPackageIdBuffer(IntPtr buffer, int capacity) { Buffer = buffer; Capacity = capacity; }
    public IntPtr Buffer { get; private set; }
    public int Capacity { get; }
    public void Dispose() { if (Buffer != IntPtr.Zero) { Marshal.FreeHGlobal(Buffer); Buffer = IntPtr.Zero; } }
}

internal static class PackageIdBufferReader
{
    internal const int MaximumBytes = 65536;
    public static OwnedPackageIdBuffer? Read(PackageIdReader reader)
    {
        var requestedLength = 0;
        var result = reader(ref requestedLength, IntPtr.Zero);
        if (result != PackageStringReadHelper.ErrorInsufficientBuffer || requestedLength < Marshal.SizeOf<WindowsCodexDesktopNativeAdapter.PackageId>() || requestedLength > MaximumBytes) return null;
        var capacity = requestedLength;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(capacity);
            requestedLength = capacity;
            result = reader(ref requestedLength, buffer);
            if (result == 0)
            {
                if (requestedLength <= capacity) return new OwnedPackageIdBuffer(buffer, capacity);
                Marshal.FreeHGlobal(buffer);
                return null;
            }
            Marshal.FreeHGlobal(buffer);
            if (result != PackageStringReadHelper.ErrorInsufficientBuffer || requestedLength <= capacity || requestedLength > MaximumBytes) return null;
            capacity = requestedLength;
        }

        return null;
    }
}

internal readonly record struct ProcessMeasurementRequest(int ProcessId, long ExpectedCreationUtcTicks, uint DesiredAccess);

internal interface IWorkingSetMeasurementFacade
{
    long? Read(ProcessMeasurementRequest request, CancellationToken cancellationToken);
}

internal static class WorkingSetMeasurement
{
    internal const uint RequiredAccess = 0x0400 | 0x0010; // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, required by GetProcessMemoryInfo.

    public static long? ReadVerified(IWorkingSetMeasurementFacade facade, int processId, long expectedCreationUtcTicks, CancellationToken cancellationToken)
        => facade.Read(new ProcessMeasurementRequest(processId, expectedCreationUtcTicks, RequiredAccess), cancellationToken);
}

internal static class NativePathReadBounds
{
    internal const int MaximumCharacters = 32768;
    public static bool CanRetry(uint reportedLength) => reportedLength > 0 && reportedLength < MaximumCharacters;
    public static bool IsSuccessful(uint reportedLength, int capacity) => reportedLength > 0 && reportedLength < capacity;
}

internal sealed class WindowsCodexDesktopNativeAdapter : IWindowsCodexDesktopNativeAdapter
{
    internal const string PackageOriginNativeLibrary = "Kernelbase.dll";
    internal const uint PackageInformationFull = 0x00000100;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareReadWriteDelete = 7;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint WtdUiNone = 2;
    private const uint WtdCacheOnlyUrlRetrieval = 0x00001000;
    private readonly IWorkingSetMeasurementFacade _measurement;

    public WindowsCodexDesktopNativeAdapter() : this(new WindowsWorkingSetMeasurementFacade()) { }

    internal WindowsCodexDesktopNativeAdapter(IWorkingSetMeasurementFacade measurement) => _measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));

    public NativeCandidateEnumeration Enumerate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return NativeCandidateEnumeration.Unavailable;
        Process[] processes;
        try { processes = Process.GetProcessesByName("codex"); }
        catch (Win32Exception) { return NativeCandidateEnumeration.Unavailable; }
        catch (InvalidOperationException) { return NativeCandidateEnumeration.Unavailable; }

        var evidence = new List<NativeCandidateEvidence>();
        foreach (var process in processes)
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                evidence.Add(CaptureCandidate(process.Id, cancellationToken));
            }
        }

        return new NativeCandidateEnumeration(NativeEnumerationSource.Succeeded, evidence);
    }

    private NativeCandidateEvidence CaptureCandidate(int candidateProcessId, CancellationToken cancellationToken)
    {
        try
        {
            using var candidate = OpenProcess(ProcessQueryLimitedInformation, false, candidateProcessId);
            if (candidate.IsInvalid) return Indeterminate(candidateProcessId);
            var creation = TryGetCreationTicks(candidate);
            var path = CanonicalizeExistingPath(TryGetImagePath(candidate), false);
            var package = ReadPackage(candidate);
            var parentProcessId = GetImmediateParentProcessId(candidate);
            if (creation is null || path is null || parentProcessId <= 0) return Indeterminate(candidateProcessId, creation ?? 0, path, package, parentProcessId);

            using var parent = OpenProcess(ProcessQueryLimitedInformation, false, parentProcessId);
            if (parent.IsInvalid) return Indeterminate(candidateProcessId, creation.Value, path, package, parentProcessId);
            var parentCreation = TryGetCreationTicks(parent);
            var parentPath = CanonicalizeExistingPath(TryGetImagePath(parent), false);
            var parentPackage = ReadPackage(parent);
            var candidateStable = TryGetCreationTicks(candidate) == creation;
            var parentRechecked = GetImmediateParentProcessId(candidate) == parentProcessId;
            if (parentCreation is null || parentPath is null) return Indeterminate(candidateProcessId, creation.Value, path, package, parentProcessId, parentCreation ?? 0, parentPath, parentPackage, candidateStable, parentRechecked);

            var preliminary = new NativeCandidateEvidence(candidateProcessId, creation.Value, path, package, parentProcessId, parentCreation.Value, parentPath, parentPackage, false, candidateStable, parentRechecked, NativeTrustState.Unavailable, NativeTrustState.Unavailable, null);
            if (!CodexDesktopProcessIdentityEvaluator.IsEligibleBeforeSignature(preliminary)) return preliminary;

            cancellationToken.ThrowIfCancellationRequested();
            var candidateTrust = ReadTrustedOffline(path);
            var parentTrust = ReadTrustedOffline(parentPath);
            candidateStable = candidateStable && TryGetCreationTicks(candidate) == creation;
            parentRechecked = parentRechecked && GetImmediateParentProcessId(candidate) == parentProcessId;
            var workingSet = candidateStable && parentRechecked
                ? WorkingSetMeasurement.ReadVerified(_measurement, candidateProcessId, creation.Value, cancellationToken)
                : null;
            return preliminary with
            {
                CandidateIdentityStable = candidateStable,
                ImmediateParentRechecked = parentRechecked,
                CandidateTrust = candidateTrust,
                ParentTrust = parentTrust,
                WorkingSetBytes = workingSet
            };
        }
        catch (Win32Exception) { return Indeterminate(candidateProcessId); }
        catch (InvalidOperationException) { return Indeterminate(candidateProcessId); }
        catch (UnauthorizedAccessException) { return Indeterminate(candidateProcessId); }
        catch (IOException) { return Indeterminate(candidateProcessId); }
        catch (ArgumentException) { return Indeterminate(candidateProcessId); }
    }

    private static NativeCandidateEvidence Indeterminate(int candidateId, long creation = 0, string? path = null, NativePackageEvidence? package = null, int parentId = 0, long parentCreation = 0, string? parentPath = null, NativePackageEvidence? parentPackage = null, bool stable = false, bool parentRechecked = false)
        => new(candidateId, creation, path, package ?? NativePackageEvidence.Unavailable, parentId, parentCreation, parentPath, parentPackage ?? NativePackageEvidence.Unavailable, true, stable, parentRechecked, NativeTrustState.Unavailable, NativeTrustState.Unavailable, null);

    private static NativePackageEvidence ReadPackage(NativeHandle process)
    {
        var fullName = PackageStringReadHelper.Read((ref int length, StringBuilder? buffer) => GetPackageFullName(process, ref length, buffer));
        if (fullName.State == PackageStringReadState.NotPackaged) return NativePackageEvidence.NotPackaged;
        if (fullName.State != PackageStringReadState.Available) return NativePackageEvidence.Unavailable;
        var family = PackageStringReadHelper.Read((ref int length, StringBuilder? buffer) => GetPackageFamilyName(process, ref length, buffer));
        var root = PackageStringReadHelper.Read((ref int length, StringBuilder? buffer) => GetPackagePathByFullName(fullName.Value!, ref length, buffer));
        var metadata = ReadPackageMetadata(fullName.Value!);
        var origin = ReadStagedPackageOrigin(fullName.Value!);
        if (family.State != PackageStringReadState.Available || root.State != PackageStringReadState.Available || metadata is null) return NativePackageEvidence.Unavailable;
        return new NativePackageEvidence(NativePackageState.Available, origin, fullName.Value, family.Value, metadata.Value.Name, metadata.Value.Publisher, metadata.Value.PublisherId, CanonicalizeExistingPath(root.Value, true));
    }

    private static NativePackageOrigin ReadStagedPackageOrigin(string fullName)
    {
        var origin = 0u;
        try
        {
            return GetStagedPackageOrigin(fullName, out origin) == 0 ? NativePackageOriginClassifier.Classify(origin) : NativePackageOrigin.Unavailable;
        }
        catch (EntryPointNotFoundException) { return NativePackageOrigin.Unavailable; }
        catch (DllNotFoundException) { return NativePackageOrigin.Unavailable; }
    }

    private static (string Name, string Publisher, string PublisherId)? ReadPackageMetadata(string fullName)
    {
        using var buffer = PackageIdBufferReader.Read((ref int length, IntPtr value) => PackageIdFromFullName(fullName, PackageInformationFull, ref length, value));
        return buffer is null ? null : PackageIdParser.Parse(buffer.Buffer, buffer.Capacity);
    }

    private static string? TryGetImagePath(NativeHandle process)
    {
        var length = 32768;
        var buffer = new StringBuilder(length);
        return QueryFullProcessImageName(process, 0, buffer, ref length) ? buffer.ToString() : null;
    }

    private static long? TryGetCreationTicks(NativeHandle process)
        => GetProcessTimes(process, out var creation, out _, out _, out _) ? DateTime.FromFileTimeUtc(((long)creation.HighDateTime << 32) | creation.LowDateTime).Ticks : null;

    private static long? TryGetWorkingSet(NativeHandle process)
    {
        var counters = new ProcessMemoryCountersEx { StructSize = (uint)Marshal.SizeOf<ProcessMemoryCountersEx>() };
        return GetProcessMemoryInfo(process, out counters, counters.StructSize) ? counters.WorkingSetSize.ToInt64() : null;
    }

    private static int GetImmediateParentProcessId(NativeHandle process)
    {
        var info = new ProcessBasicInformation();
        return NtQueryInformationProcess(process, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _) == 0 && info.InheritedFromUniqueProcessId != IntPtr.Zero
            ? unchecked((int)info.InheritedFromUniqueProcessId.ToInt64()) : 0;
    }

    private static string? CanonicalizeExistingPath(string? path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        using var handle = CreateFile(path, GenericRead, FileShareReadWriteDelete, IntPtr.Zero, OpenExisting, directory ? FileFlagBackupSemantics : 0, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        var buffer = new StringBuilder(NativePathReadBounds.MaximumCharacters);
        var result = GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
        if (result == 0) return null;
        if (result >= buffer.Capacity)
        {
            if (!NativePathReadBounds.CanRetry(result)) return null;
            buffer = new StringBuilder((int)result + 1);
            result = GetFinalPathNameByHandle(handle, buffer, buffer.Capacity, 0);
            if (!NativePathReadBounds.IsSuccessful(result, buffer.Capacity)) return null;
        }

        var value = buffer.ToString();
        if (value.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) value = "\\\\" + value[8..];
        else if (value.StartsWith("\\\\?\\", StringComparison.Ordinal)) value = value[4..];
        return Path.TrimEndingDirectorySeparator(value);
    }

    private static NativeTrustState ReadTrustedOffline(string path)
    {
        try
        {
            return NativeTrustClassifier.Classify(GetTrustStatusOffline(path));
        }
        catch (DllNotFoundException) { return NativeTrustState.Unavailable; }
        catch (EntryPointNotFoundException) { return NativeTrustState.Unavailable; }
    }

    private static uint GetTrustStatusOffline(string path)
    {
        var fileInfo = new WinTrustFileInfo(path);
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, pointer, false);
            var data = new WinTrustData(pointer);
            var action = WinTrustActionGenericVerifyV2;
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
            Marshal.FreeCoTaskMem(fileInfo.FilePath);
        }
    }

    private sealed class WindowsWorkingSetMeasurementFacade : IWorkingSetMeasurementFacade
    {
        public long? Read(ProcessMeasurementRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = OpenProcess(request.DesiredAccess, false, request.ProcessId);
            if (process.IsInvalid || TryGetCreationTicks(process) != request.ExpectedCreationUtcTicks) return null;
            return TryGetWorkingSet(process);
        }
    }
    private sealed class NativeHandle : SafeHandleZeroOrMinusOneIsInvalid { public NativeHandle() : base(true) { } protected override bool ReleaseHandle() => CloseHandle(handle); }
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint LowDateTime; public int HighDateTime; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessBasicInformation { public IntPtr Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1, UniqueProcessId, InheritedFromUniqueProcessId; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessMemoryCountersEx { public uint StructSize, PageFaultCount; public IntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage; }
    [StructLayout(LayoutKind.Explicit)] internal struct PackageVersion { [FieldOffset(0)] public ulong Version; }
    // appmodel.h: reserved, processorArchitecture, PACKAGE_VERSION, then the four PWSTR fields.
    [StructLayout(LayoutKind.Sequential)] internal struct PackageId { public uint Reserved; public uint ProcessorArchitecture; public PackageVersion Version; public IntPtr Name; public IntPtr Publisher; public IntPtr ResourceId; public IntPtr PublisherId; }
    [StructLayout(LayoutKind.Sequential)] private struct WinTrustFileInfo { public uint StructSize; public IntPtr FilePath, FileHandle, KnownSubject; public WinTrustFileInfo(string path) { StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(); FilePath = Marshal.StringToCoTaskMemUni(path); FileHandle = IntPtr.Zero; KnownSubject = IntPtr.Zero; } }
    [StructLayout(LayoutKind.Sequential)] private struct WinTrustData { public uint StructSize; public IntPtr PolicyCallbackData, SipClientData; public uint UiChoice, RevocationChecks, UnionChoice; public IntPtr FileInfo; public uint StateAction; public IntPtr StateData, UrlReference; public uint ProvFlags, UiContext; public WinTrustData(IntPtr fileInfo) { StructSize = (uint)Marshal.SizeOf<WinTrustData>(); PolicyCallbackData = SipClientData = IntPtr.Zero; UiChoice = WtdUiNone; RevocationChecks = 0; UnionChoice = 1; FileInfo = fileInfo; StateAction = 0; StateData = UrlReference = IntPtr.Zero; ProvFlags = WtdCacheOnlyUrlRetrieval; UiContext = 0; } }
    private static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
    [DllImport("kernel32.dll", SetLastError = true)] private static extern NativeHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);
    [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(NativeHandle process, int infoClass, ref ProcessBasicInformation info, int length, out int returnLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetPackageFullName(NativeHandle process, ref int length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetPackageFamilyName(NativeHandle process, ref int length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetPackagePathByFullName(string fullName, ref int length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint PackageIdFromFullName(string fullName, uint flags, ref int length, IntPtr buffer);
    [DllImport(PackageOriginNativeLibrary, CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetStagedPackageOrigin(string fullName, out uint origin);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(NativeHandle process, uint flags, StringBuilder value, ref int length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(NativeHandle process, out FileTime creation, out FileTime exit, out FileTime kernel, out FileTime user);
    [DllImport("psapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessMemoryInfo(NativeHandle process, out ProcessMemoryCountersEx counters, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern NativeHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandle(NativeHandle handle, StringBuilder path, int length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)] private static extern uint WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);
}
