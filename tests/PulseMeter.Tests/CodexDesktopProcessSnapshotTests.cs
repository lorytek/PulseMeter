using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.SupportSnapshot.Business;
using PulseMeter.Slices.SupportSnapshot.Models;

namespace PulseMeter.Tests;

public sealed class CodexDesktopProcessSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PackageOriginInterop_UsesKernelbaseExport()
        => Assert.Equal("Kernelbase.dll", WindowsCodexDesktopNativeAdapter.PackageOriginNativeLibrary);

    [Fact]
    public void PackageMetadataInterop_RequestsFullPackageInformation()
        => Assert.Equal(0x00000100u, WindowsCodexDesktopNativeAdapter.PackageInformationFull);




    [Fact]
    public void Formatter_ProducesExactSchemaV1SnakeCaseJson()
    {
        var snapshot = new CodexDesktopProcessSnapshot(1, Now, CodexDesktopProcessSnapshotStatus.Partial, 0, null, true, 1, CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);
        var expected = """
        {
          "schema_version": 1,
          "captured_at_utc": "2026-08-11T12:00:00.0000000Z",
          "status": "partial",
          "verified_process_count": 0,
          "summed_working_set_bytes": null,
          "shared_pages_may_overlap": true,
          "unavailable_or_changed_count": 1,
          "identity_rule_version": "openai_codex_package_resource_v1"
        }
        """;
        Assert.Equal(
            expected.ReplaceLineEndings("\n"),
            CodexDesktopProcessSnapshotFormatter.Format(snapshot).ReplaceLineEndings("\n"));
    }

    [Theory]
    [MemberData(nameof(StatusTable))]
    public void Service_UsesSourceAwareTruthTable(object enumeration, CodexDesktopProcessSnapshotStatus status, int verified, long? sum, int indeterminate)
    {
        var result = CreateService((NativeCandidateEnumeration)enumeration).Capture();
        Assert.Equal(status, result.Status);
        Assert.Equal(verified, result.VerifiedProcessCount);
        Assert.Equal(sum, result.SummedWorkingSetBytes);
        Assert.Equal(indeterminate, result.UnavailableOrChangedCount);
    }

    public static IEnumerable<object[]> StatusTable()
    {
        yield return Row(NativeCandidateEnumeration.Unavailable, CodexDesktopProcessSnapshotStatus.Unavailable, 0, null, 0);
        yield return Row(new NativeCandidateEnumeration(NativeEnumerationSource.Succeeded, Array.Empty<NativeCandidateEvidence>()), CodexDesktopProcessSnapshotStatus.NoVerified, 0, null, 0);
        yield return Row(new NativeCandidateEnumeration(NativeEnumerationSource.Succeeded, new[] { Valid(101, 10, 2048) }), CodexDesktopProcessSnapshotStatus.Complete, 1, 2048, 0);
        yield return Row(new NativeCandidateEnumeration(NativeEnumerationSource.Succeeded, new[] { Valid(101, 10, 2048), Indeterminate(102, 11) }), CodexDesktopProcessSnapshotStatus.Partial, 1, 2048, 1);
        yield return Row(new NativeCandidateEnumeration(NativeEnumerationSource.Succeeded, new[] { Indeterminate(102, 11) }), CodexDesktopProcessSnapshotStatus.Partial, 0, null, 1);
    }

    [Theory]
    [InlineData("candidate package name mismatch")]
    [InlineData("candidate family is ChatGPT Desktop")]
    [InlineData("PulseMeter local CLI parent")]
    [InlineData("generic CLI helper parent")]
    [InlineData("root escape")]
    [InlineData("resources suffix lookalike")]
    [InlineData("invalid candidate trust")]
    [InlineData("definitively untrusted")]
    public void RealIdentityEvaluator_FailsClosedForExcludedCandidates(string excludedCase)
    {
        var evidence = Valid(101, 10, 2048);
        evidence = excludedCase switch
        {
            "candidate package name mismatch" => evidence with { CandidatePackage = Package(name: "OpenAI.Other") },
            "candidate family is ChatGPT Desktop" => evidence with { CandidatePackage = Package(family: "ChatGPT_2p2nqsd0c76g0") },
            "PulseMeter local CLI parent" => evidence with { ParentPackage = Package(name: "PulseMeter") },
            "generic CLI helper parent" => evidence with { ParentPackage = NativePackageEvidence.NotPackaged },
            "root escape" => evidence with { CandidateCanonicalPath = "C:\\outside\\resources\\codex.exe" },
            "resources suffix lookalike" => evidence with { CandidateCanonicalPath = "C:\\pkg\\resources\\codex.exe.bak" },
            "invalid candidate trust" => evidence with { CandidateTrust = NativeTrustState.Untrusted },
            _ => evidence with { ParentTrust = NativeTrustState.Untrusted }
        };

        Assert.Equal(NativeCandidateDecision.Ignored, CodexDesktopProcessIdentityEvaluator.Evaluate(evidence));
        Assert.Empty(new CodexDesktopProcessSnapshotProbe(new FakeNativeAdapter(Succeeded(evidence))).Capture(CancellationToken.None).Records);
    }

    [Fact]
    public void RealIdentityEvaluator_AllowsPackageVersionDriftWhenOtherEvidenceIsExact()
    {
        var evidence = Valid(101, 10, 2048) with
        {
            CandidatePackage = Package(fullName: "OpenAI.Codex_1.2.3.4_x64__2p2nqsd0c76g0"),
            ParentPackage = Package(fullName: "OpenAI.Codex_9.8.7.6_x64__2p2nqsd0c76g0")
        };
        Assert.Equal(NativeCandidateDecision.Verified, CodexDesktopProcessIdentityEvaluator.Evaluate(evidence));
    }

    [Theory]
    [InlineData("access denied")]
    [InlineData("process exit")]
    [InlineData("PID reused")]
    public void RealIdentityEvaluator_MapsChurnAndAccessToIndeterminate(string reason)
    {
        var evidence = reason == "PID reused" ? Valid(101, 10, 2048) with { CandidateIdentityStable = false } : Indeterminate(101, 10);
        Assert.Equal(NativeCandidateDecision.Indeterminate, CodexDesktopProcessIdentityEvaluator.Evaluate(evidence));
    }

    [Fact]
    public void RealIdentityEvaluator_RequiresParentRecheckAndCreationOrder()
    {
        var noParentRecheck = Valid(101, 10, 2048) with { ImmediateParentRechecked = false };
        var parentCreatedAfterChild = Valid(102, 10, 2048) with { ParentCreationUtcTicks = 11 };

        Assert.Equal(NativeCandidateDecision.Indeterminate, CodexDesktopProcessIdentityEvaluator.Evaluate(noParentRecheck));
        Assert.Equal(NativeCandidateDecision.Indeterminate, CodexDesktopProcessIdentityEvaluator.Evaluate(parentCreatedAfterChild));
    }

    [Fact]
    public void RealIdentityEvaluator_PreservesPackagePathOriginAndTrustUncertainty()
    {
        var unavailablePackage = Valid(1, 10, 1) with { CandidatePackage = NativePackageEvidence.Unavailable };
        var unavailableOrigin = Valid(2, 10, 1) with { CandidatePackage = Package() with { Origin = NativePackageOrigin.Unavailable } };
        var unavailableRoot = Valid(3, 10, 1) with { CandidatePackage = Package() with { Root = null } };
        var unavailableTrust = Valid(4, 10, 1) with { CandidateTrust = NativeTrustState.Unavailable };
        Assert.All(new[] { unavailablePackage, unavailableOrigin, unavailableRoot, unavailableTrust }, item => Assert.Equal(NativeCandidateDecision.Indeterminate, CodexDesktopProcessIdentityEvaluator.Evaluate(item)));
    }

    [Fact]
    public void RealIdentityEvaluator_DefinitiveExclusionWinsOverOtherUncertainty()
    {
        var packageMismatchWithUnavailableParent = Valid(1, 10, 1) with { CandidatePackage = Package(name: "Other"), ParentPackage = NativePackageEvidence.Unavailable };
        var rootEscapeWithUnavailableTrust = Valid(2, 10, 1) with { CandidateCanonicalPath = "C:\\outside\\resources\\codex.exe", ParentTrust = NativeTrustState.Unavailable };
        var untrustedWithUnavailableOrigin = Valid(3, 10, 1) with { CandidateTrust = NativeTrustState.Untrusted, ParentPackage = Package() with { Origin = NativePackageOrigin.Unavailable } };
        Assert.All(new[] { packageMismatchWithUnavailableParent, rootEscapeWithUnavailableTrust, untrustedWithUnavailableOrigin }, item => Assert.Equal(NativeCandidateDecision.Ignored, CodexDesktopProcessIdentityEvaluator.Evaluate(item)));
    }

    [Fact]
    public void RealIdentityEvaluator_PathExclusionWinsWhenTheOtherPathIsUnavailable()
    {
        var candidateSuffixMismatch = Valid(1, 10, 1) with { CandidateCanonicalPath = "C:\\pkg\\resources\\codex.exe.bak", ParentCanonicalPath = null };
        var parentRootEscape = Valid(2, 10, 1) with { CandidateCanonicalPath = null, ParentCanonicalPath = "C:\\outside\\parent.exe" };
        var unequalRoots = Valid(3, 10, 1) with { ParentPackage = Package(root: "C:\\other") };

        Assert.All(new[] { candidateSuffixMismatch, parentRootEscape, unequalRoots }, item => Assert.Equal(NativeCandidateDecision.Ignored, CodexDesktopProcessIdentityEvaluator.Evaluate(item)));
    }

    [Theory]
    [InlineData(0u, 2)]
    [InlineData(1u, 1)]
    [InlineData(2u, 1)]
    [InlineData(3u, 0)]
    [InlineData(6u, 1)]
    [InlineData(99u, 2)]
    public void PackageOriginClassifier_UsesClosedVocabulary(uint value, int expected) => Assert.Equal((NativePackageOrigin)expected, NativePackageOriginClassifier.Classify(value));

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(0x800B0100u, 1)]
    [InlineData(0x80096010u, 1)]
    [InlineData(0xDEADBEEFu, 2)]
    public void TrustClassifier_OnlyTreatsKnownRejectionsAsDefinitive(uint value, int expected) => Assert.Equal((NativeTrustState)expected, NativeTrustClassifier.Classify(value));

    [Fact]
    public void PackageIdBufferReader_PreservesCapacityAcrossRetryAndRejectsInflatedSuccess()
    {
        var calls = 0;
        PackageIdReader retry = (ref int length, IntPtr buffer) =>
        {
            calls++;
            if (buffer == IntPtr.Zero) { length = 64; return PackageStringReadHelper.ErrorInsufficientBuffer; }
            if (calls == 2) { length = 80; return PackageStringReadHelper.ErrorInsufficientBuffer; }
            length = 72; return 0;
        };
        using var owned = PackageIdBufferReader.Read(retry);
        Assert.NotNull(owned);
        Assert.Equal(80, owned!.Capacity);
        PackageIdReader inflated = (ref int length, IntPtr buffer) => { if (buffer == IntPtr.Zero) { length = 64; return PackageStringReadHelper.ErrorInsufficientBuffer; } length = 65; return 0; };
        Assert.Null(PackageIdBufferReader.Read(inflated));
        PackageIdReader tooLarge = (ref int length, IntPtr buffer) => { length = PackageIdBufferReader.MaximumBytes + 1; return PackageStringReadHelper.ErrorInsufficientBuffer; };
        Assert.Null(PackageIdBufferReader.Read(tooLarge));
    }

    [Fact]
    public void Service_LeavesCandidateCountsTruthfulOnAggregateOverflow()
    {
        var probe = new AggregateProbe(new CodexDesktopProcessProbeResult(CodexDesktopProcessProbeSource.Succeeded, new[]
        {
            new CodexDesktopProcessProbeRecord(CodexDesktopProcessProbeVerdict.Verified, long.MaxValue),
            new CodexDesktopProcessProbeRecord(CodexDesktopProcessProbeVerdict.Verified, 1)
        }));
        var result = new CodexDesktopProcessSnapshotService(probe, () => Now).Capture();
        Assert.Equal(CodexDesktopProcessSnapshotStatus.Partial, result.Status);
        Assert.Equal(2, result.VerifiedProcessCount);
        Assert.Equal(0, result.UnavailableOrChangedCount);
        Assert.Null(result.SummedWorkingSetBytes);
        Assert.Equal(CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion, result.IdentityRuleVersion);
    }

    [Fact]
    public void WorkingSetFacade_ReceivesDocumentedAccessAndPinnedIdentity()
    {
        var facade = new RecordingMeasurementFacade();
        Assert.Equal(42, WorkingSetMeasurement.ReadVerified(facade, 7, 99, CancellationToken.None));
        Assert.Equal(0x0410u, facade.Request.DesiredAccess);
        Assert.Equal(7, facade.Request.ProcessId);
        Assert.Equal(99, facade.Request.ExpectedCreationUtcTicks);
    }

    [Fact]
    public void NativeProbe_DeduplicatesSamePidAndCreationIdentity()
    {
        var evidence = Valid(101, 10, 2048);
        var result = new CodexDesktopProcessSnapshotProbe(new FakeNativeAdapter(Succeeded(evidence, evidence))).Capture(CancellationToken.None);
        Assert.Single(result.Records);
    }

    [Fact]
    public void PackageStringReader_PropagatesFirstPassLengthAndRetriesResizeForFullNameFamilyAndPath()
    {
        foreach (var value in new[] { "OpenAI.Codex_1.2.3.4_x64__2p2nqsd0c76g0", "OpenAI.Codex_2p2nqsd0c76g0", "C:\\Program Files\\WindowsApps\\OpenAI.Codex" })
        {
            var fake = new ResizingStringReader(value);
            var result = PackageStringReadHelper.Read(fake.Read);
            Assert.Equal(PackageStringReadState.Available, result.State);
            Assert.Equal(value, result.Value);
            Assert.Equal(new[] { 0, value.Length + 1, value.Length + 3 }, fake.RequestedLengths);
        }
    }

    [Fact]
    public void PackageId_UsesSdkHeaderLayoutAndParsesControlledFixture()
    {
        Assert.Equal(IntPtr.Size == 8 ? 48 : 32, Marshal.SizeOf<WindowsCodexDesktopNativeAdapter.PackageId>());
        Assert.Equal(8, Marshal.OffsetOf<WindowsCodexDesktopNativeAdapter.PackageId>(nameof(WindowsCodexDesktopNativeAdapter.PackageId.Version)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<WindowsCodexDesktopNativeAdapter.PackageId>(nameof(WindowsCodexDesktopNativeAdapter.PackageId.Name)).ToInt32());

        var strings = new[] { "OpenAI.Codex", "CN=50BDFD77-8903-4850-9FFE-6E8522F64D5B", "", "2p2nqsd0c76g0" };
        var structureSize = Marshal.SizeOf<WindowsCodexDesktopNativeAdapter.PackageId>();
        var byteCount = structureSize + strings.Sum(value => Encoding.Unicode.GetByteCount(value) + 2);
        var buffer = Marshal.AllocHGlobal(byteCount);
        try
        {
            var offsets = new int[strings.Length];
            var offset = structureSize;
            for (var index = 0; index < strings.Length; index++)
            {
                offsets[index] = offset;
                var text = Encoding.Unicode.GetBytes(strings[index] + "\0");
                Marshal.Copy(text, 0, IntPtr.Add(buffer, offset), text.Length);
                offset += text.Length;
            }

            Marshal.StructureToPtr(new WindowsCodexDesktopNativeAdapter.PackageId
            {
                Name = IntPtr.Add(buffer, offsets[0]), Publisher = IntPtr.Add(buffer, offsets[1]), ResourceId = IntPtr.Add(buffer, offsets[2]), PublisherId = IntPtr.Add(buffer, offsets[3])
            }, buffer, false);
            Assert.Equal(("OpenAI.Codex", "CN=50BDFD77-8903-4850-9FFE-6E8522F64D5B", "2p2nqsd0c76g0"), PackageIdParser.Parse(buffer, byteCount));
            Assert.Null(PackageIdParser.Parse(IntPtr.Zero, 0));
            Assert.Null(PackageIdParser.Parse(buffer, structureSize - 1));
            Marshal.StructureToPtr(new WindowsCodexDesktopNativeAdapter.PackageId { Name = buffer, Publisher = IntPtr.Add(buffer, offsets[1]), ResourceId = IntPtr.Add(buffer, offsets[2]), PublisherId = IntPtr.Add(buffer, offsets[3]) }, buffer, false);
            Assert.Null(PackageIdParser.Parse(buffer, byteCount));
            Marshal.StructureToPtr(new WindowsCodexDesktopNativeAdapter.PackageId { Name = IntPtr.Add(buffer, byteCount), Publisher = IntPtr.Add(buffer, offsets[1]), ResourceId = IntPtr.Add(buffer, offsets[2]), PublisherId = IntPtr.Add(buffer, offsets[3]) }, buffer, false);
            Assert.Null(PackageIdParser.Parse(buffer, byteCount));
            Marshal.StructureToPtr(new WindowsCodexDesktopNativeAdapter.PackageId { Name = IntPtr.Add(buffer, byteCount - 1), Publisher = IntPtr.Add(buffer, offsets[1]), ResourceId = IntPtr.Add(buffer, offsets[2]), PublisherId = IntPtr.Add(buffer, offsets[3]) }, buffer, false);
            Assert.Null(PackageIdParser.Parse(buffer, byteCount));
            Marshal.Copy(Enumerable.Repeat((byte)0x41, byteCount - structureSize).ToArray(), 0, IntPtr.Add(buffer, structureSize), byteCount - structureSize);
            Marshal.StructureToPtr(new WindowsCodexDesktopNativeAdapter.PackageId { Name = IntPtr.Add(buffer, structureSize), Publisher = IntPtr.Add(buffer, offsets[1]), ResourceId = IntPtr.Add(buffer, offsets[2]), PublisherId = IntPtr.Add(buffer, offsets[3]) }, buffer, false);
            Assert.Null(PackageIdParser.Parse(buffer, byteCount));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public async Task CaptureAsync_RunsBlockingNativeProbeOffCallerStaThread()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var returnedToStaCaller = new ManualResetEventSlim();
        var adapter = new BlockingNativeAdapter(entered, release);
        var service = CreateService(adapter);
        Task<CodexDesktopProcessSnapshot>? task = null;
        Exception? invocationError = null;
        var callerThread = 0;
        var sta = new Thread(() =>
        {
            try
            {
                callerThread = Environment.CurrentManagedThreadId;
                task = service.CaptureAsync();
            }
            catch (Exception exception)
            {
                invocationError = exception;
            }
            finally
            {
                returnedToStaCaller.Set();
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        Assert.True(returnedToStaCaller.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(invocationError);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotNull(task);
        Assert.False(task!.IsCompleted);
        Assert.NotEqual(callerThread, adapter.CaptureThreadId);
        release.Set();
        Assert.Equal(CodexDesktopProcessSnapshotStatus.NoVerified, (await task!).Status);
        Assert.True(sta.Join(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Capture_HonorsCancellationBeforeNativeAdapter()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var adapter = new FakeNativeAdapter(Succeeded());
        Assert.Throws<OperationCanceledException>(() => CreateService(adapter).Capture(cancelled.Token));
        Assert.False(adapter.WasCalled);
    }

    [Fact]
    public async Task CaptureAsync_CancelsWhileWaitingAndAllowsLaterMeasurement()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var adapter = new BlockingNativeAdapter(entered, release);
        var service = CreateService(adapter);
        var first = service.CaptureAsync();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        using var waitingCancellation = new CancellationTokenSource();
        var second = service.CaptureAsync(waitingCancellation.Token);
        waitingCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        release.Set();
        await first;
        Assert.Equal(CodexDesktopProcessSnapshotStatus.NoVerified, (await service.CaptureAsync()).Status);
    }

    [Fact]
    public void PackageStringReader_RejectsOversizedSuccessLength()
    {
        PackageStringReader reader = (ref int length, StringBuilder? buffer) =>
        {
            if (buffer is null) { length = 4; return PackageStringReadHelper.ErrorInsufficientBuffer; }
            length = 5;
            buffer.Append("abc");
            return 0;
        };
        Assert.Equal(PackageStringReadState.Unavailable, PackageStringReadHelper.Read(reader).State);
    }

    [Fact]
    public void FinalPathBounds_RejectsOversizedRetryAndSecondResult()
    {
        Assert.False(NativePathReadBounds.CanRetry((uint)NativePathReadBounds.MaximumCharacters));
        Assert.True(NativePathReadBounds.CanRetry(42));
        Assert.False(NativePathReadBounds.IsSuccessful(42, 42));
        Assert.True(NativePathReadBounds.IsSuccessful(41, 42));
    }

    [Fact]
    public void PublicContract_HasNoSensitiveFieldsOrPascalCaseJson()
    {
        var properties = typeof(CodexDesktopProcessSnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name).ToArray();
        Assert.DoesNotContain(properties, name => name.Contains("Pid", StringComparison.OrdinalIgnoreCase) || name.Contains("Path", StringComparison.OrdinalIgnoreCase) || name.Contains("Package", StringComparison.OrdinalIgnoreCase) || name.Contains("Signer", StringComparison.OrdinalIgnoreCase) || name.Contains("Exception", StringComparison.OrdinalIgnoreCase));
        var json = CodexDesktopProcessSnapshotFormatter.Format(new CodexDesktopProcessSnapshot(1, Now, CodexDesktopProcessSnapshotStatus.NoVerified, 0, null, true, 0, CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion));
        Assert.DoesNotContain("CapturedAtUtc", json);
        Assert.Contains("\"captured_at_utc\"", json);
    }

    private static CodexDesktopProcessSnapshotService CreateService(NativeCandidateEnumeration enumeration) => CreateService(new FakeNativeAdapter(enumeration));
    private static CodexDesktopProcessSnapshotService CreateService(IWindowsCodexDesktopNativeAdapter adapter) => new(new CodexDesktopProcessSnapshotProbe(adapter), () => Now);
    private static object[] Row(NativeCandidateEnumeration e, CodexDesktopProcessSnapshotStatus s, int v, long? sum, int i) => new object[] { e, s, v, sum!, i };
    private static NativeCandidateEnumeration Succeeded(params NativeCandidateEvidence[] values) => new(NativeEnumerationSource.Succeeded, values);
    private static NativePackageEvidence Package(string? fullName = null, string? family = null, string? name = null, string? publisher = null, string? publisherId = null, string? root = null)
        => new(NativePackageState.Available, NativePackageOrigin.Store, fullName ?? "OpenAI.Codex_1.0.0.0_x64__2p2nqsd0c76g0", family ?? CodexDesktopProcessIdentityEvaluator.PackageFamily, name ?? CodexDesktopProcessIdentityEvaluator.PackageName, publisher ?? CodexDesktopProcessIdentityEvaluator.PackagePublisher, publisherId ?? CodexDesktopProcessIdentityEvaluator.PublisherId, root ?? "C:\\pkg");
    private static NativeCandidateEvidence Valid(int pid, long created, long bytes)
        => new(pid, created, "C:\\pkg\\resources\\codex.exe", Package(), 77, created - 1, "C:\\pkg\\Codex.exe", Package(), false, true, true, NativeTrustState.Trusted, NativeTrustState.Trusted, bytes);
    private static NativeCandidateEvidence Indeterminate(int pid, long created) => Valid(pid, created, 0) with { InspectionIndeterminate = true, WorkingSetBytes = null };

    private sealed class FakeNativeAdapter : IWindowsCodexDesktopNativeAdapter
    {
        private readonly NativeCandidateEnumeration _enumeration;
        public FakeNativeAdapter(NativeCandidateEnumeration enumeration) => _enumeration = enumeration;
        public bool WasCalled { get; private set; }
        public NativeCandidateEnumeration Enumerate(CancellationToken cancellationToken) { WasCalled = true; cancellationToken.ThrowIfCancellationRequested(); return _enumeration; }
    }

    private sealed class AggregateProbe : ICodexDesktopProcessSnapshotProbe
    {
        private readonly CodexDesktopProcessProbeResult _result;
        public AggregateProbe(CodexDesktopProcessProbeResult result) => _result = result;
        public CodexDesktopProcessProbeResult Capture(CancellationToken cancellationToken) => _result;
    }

    private sealed class RecordingMeasurementFacade : IWorkingSetMeasurementFacade
    {
        public ProcessMeasurementRequest Request { get; private set; }
        public long? Read(ProcessMeasurementRequest request, CancellationToken cancellationToken) { Request = request; return 42; }
    }

    private sealed class BlockingNativeAdapter : IWindowsCodexDesktopNativeAdapter
    {
        private readonly ManualResetEventSlim _entered, _release;
        public BlockingNativeAdapter(ManualResetEventSlim entered, ManualResetEventSlim release) { _entered = entered; _release = release; }
        public int CaptureThreadId { get; private set; }
        public NativeCandidateEnumeration Enumerate(CancellationToken cancellationToken) { CaptureThreadId = Environment.CurrentManagedThreadId; _entered.Set(); _release.Wait(cancellationToken); return Succeeded(); }
    }

    private sealed class ResizingStringReader
    {
        private readonly string _value;
        private int _calls;
        public ResizingStringReader(string value) => _value = value;
        public List<int> RequestedLengths { get; } = new();
        public uint Read(ref int length, StringBuilder? buffer)
        {
            RequestedLengths.Add(length);
            _calls++;
            if (buffer is null) { length = _value.Length + 1; return PackageStringReadHelper.ErrorInsufficientBuffer; }
            if (_calls == 2) { length = _value.Length + 3; return PackageStringReadHelper.ErrorInsufficientBuffer; }
            buffer.Append(_value);
            return 0;
        }
    }
}
