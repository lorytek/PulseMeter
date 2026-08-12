using System.Reflection;
using System.Runtime.ExceptionServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.PulseMeterWindow.UI;
using PulseMeter.Slices.SupportSnapshot.Business;
using PulseMeter.Slices.SupportSnapshot.Models;
using PulseMeter.Slices.SupportSnapshot.UI;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class SupportSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Formatter_ProducesExactAllowlistedJson()
    {
        var text = new SupportSnapshotFormatter("1.2.3+private.build", "Release", () => Now).Format(
            new SupportSnapshotFacts(SupportReaderState.Live, Now.AddMinutes(-5), true, false, true));

        var expected = """
            {
              "schema_version": 1,
              "product": "PulseMeter",
              "version": "1.2.3",
              "build_configuration": "Release",
              "reader_state": "live",
              "freshness_age": "1_to_5m",
              "parser_state": "recognized",
              "coverage": {
                "rate_limit_data": "present",
                "account_usage_data": "absent",
                "local_project_history": "present"
              }
            }
            """.ReplaceLineEndings(Environment.NewLine);
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Formatter_SanitizesPublicSemanticVersions_AndDefaultsToNotObserved()
    {
        Assert.Equal("1.2.3-preview.1", SupportSnapshotFormatter.SanitizePublicSemanticVersion("1.2.3-preview.1+private"));
        Assert.Equal("unknown", SupportSnapshotFormatter.SanitizePublicSemanticVersion("build-C:\\private"));

        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(SupportSnapshotFacts.NotObserved);
        Assert.Contains("\"reader_state\": \"not_observed\"", text);
        Assert.Contains("\"freshness_age\": \"unknown\"", text);
        Assert.Contains("\"parser_state\": \"not_observed\"", text);
    }

    [Theory]
    [InlineData(-0.99, "under_1m")]
    [InlineData(-1, "1_to_5m")]
    [InlineData(-5, "1_to_5m")]
    [InlineData(-5.01, "5_to_30m")]
    [InlineData(-30, "5_to_30m")]
    [InlineData(-30.01, "over_30m")]
    public void Formatter_UsesClosedFreshnessBuckets(double minutesAgo, string expected)
    {
        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(
            new SupportSnapshotFacts(SupportReaderState.Live, Now.AddMinutes(minutesAgo), false, false, false));

        Assert.Contains($"\"freshness_age\": \"{expected}\"", text);
    }

    [Fact]
    public void Formatter_TreatsFutureSourceTimestampsAsUnknown()
    {
        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(
            new SupportSnapshotFacts(SupportReaderState.Live, Now.AddSeconds(1), true, false, false));

        Assert.Contains("\"freshness_age\": \"unknown\"", text);
    }

    [Theory]
    [InlineData(SyncStatus.Live, "live", "no_supported_fields", "absent")]
    [InlineData(SyncStatus.Stale, "stale", "no_supported_fields", "absent")]
    [InlineData(SyncStatus.Mocked, "mock", "no_supported_fields", "absent")]
    [InlineData(SyncStatus.Unavailable, "unavailable", "unknown", "unknown")]
    public void FactsStore_ProjectsOnlyClosedStateVocabulary(SyncStatus status, string reader, string parser, string coverage)
    {
        var store = new SupportSnapshotFactsStore();
        store.Observe(new UsageSnapshot { SyncStatus = status, LastUpdatedUtc = Now, Source = "SENTINEL_SOURCE" }, Now);

        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(store.Capture());

        Assert.Contains($"\"reader_state\": \"{reader}\"", text);
        Assert.Contains($"\"parser_state\": \"{parser}\"", text);
        Assert.Contains($"\"rate_limit_data\": \"{coverage}\"", text);
    }

    [Fact]
    public void FactsStore_DropsPoisonedRawSnapshotFields_AndRetainsNoRawTypes()
    {
        const string poison = "C:\\private\\project\\thread-123 SENTINEL_SOURCE SENTINEL_STATUS SENTINEL_RAW";
        var store = new SupportSnapshotFactsStore();
        store.Observe(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = Now,
            Source = poison,
            StatusMessage = poison,
            RawRateLimitsJson = poison,
            ProjectUsageRows = [new ProjectUsageRow(poison, poison, 42, 42, 1, 100)]
        }, Now);

        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(store.Capture());

        Assert.DoesNotContain("SENTINEL", text);
        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(SupportSnapshotFactsStore).GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field => field.FieldType == typeof(UsageSnapshot) || field.FieldType == typeof(string));
    }

    [Fact]
    public void FactsStore_RecognizesStreakOnlyAccountSummaryCoverage()
    {
        var store = new SupportSnapshotFactsStore();
        store.Observe(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = Now,
            CurrentStreakDays = 3
        }, Now);

        var text = new SupportSnapshotFormatter("1.0.0", "Release", () => Now).Format(store.Capture());

        Assert.Contains("\"account_usage_data\": \"present\"", text);
        Assert.Contains("\"parser_state\": \"recognized\"", text);
    }

    [Fact]
    public void ViewModelApplySnapshot_UpdatesInjectedFactsStore()
    {
        var store = new SupportSnapshotFactsStore();
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), supportSnapshotFactsStore: store);

        viewModel.ApplySnapshot(new UsageSnapshot { SyncStatus = SyncStatus.Live, Buckets = [new RateLimitBucket()] });

        Assert.True(store.Capture().HasRateLimitData);
    }

    [Fact]
    public void PreviewViewModel_CopiesVisibleTextExactly_AndHandlesFailureGenerically()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = new SupportSnapshotViewModel("{\n  \"safe\": true\n}", clipboard);
        viewModel.CopyPreview();
        Assert.Equal(viewModel.PreviewText, clipboard.Text);
        Assert.Equal("Preview copied.", viewModel.CopyFeedback);

        var failure = new SupportSnapshotViewModel("{}", new ThrowingClipboard());
        failure.CopyPreview();
        Assert.Equal("Could not copy the preview. Try again.", failure.CopyFeedback);
    }

    [Fact]
    public void SnapshotModal_DeclaresAccessibleKeyboardReachableControls()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseMeter", "Slices", "SupportSnapshot", "UI", "SupportSnapshotWindow.xaml"));
        Assert.Contains("AutomationProperties.Name=\"Support snapshot JSON preview\"", xaml);
        Assert.Contains("Text=\"{Binding PreviewText, Mode=OneWay}\"", xaml);
        Assert.Contains("IsDefault=\"True\"", xaml);
        Assert.Contains("IsCancel=\"True\"", xaml);
    }

    [Fact]
    public void SnapshotModal_ConstructsOnStaThread()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new SupportSnapshotWindow(new SupportSnapshotViewModel("{}", new RecordingClipboard()));
                window.Close();
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public async Task DesktopProcessSnapshotViewModel_IsInertUntilMeasured_AndSuppressesOverlap()
    {
        var service = new DeferredDesktopProcessSnapshotService();
        var viewModel = new DesktopProcessSnapshotViewModel(service, new RecordingClipboard());

        Assert.Equal(DesktopProcessSnapshotPresentationState.Idle, viewModel.State);
        Assert.Equal(0, service.CaptureCount);
        Assert.False(viewModel.CanCopyPreview);

        viewModel.BeginMeasurement();
        viewModel.BeginMeasurement();

        Assert.Equal(DesktopProcessSnapshotPresentationState.Measuring, viewModel.State);
        Assert.Equal(1, service.CaptureCount);
        Assert.False(viewModel.MeasureNowCommand.CanExecute(null));

        service.CompleteNext(CompleteDesktopSnapshot());
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.State == DesktopProcessSnapshotPresentationState.Complete,
            TimeSpan.FromSeconds(2)));
        Assert.True(viewModel.CanCopyPreview);
        Assert.Contains("Measurement complete", viewModel.StatusText);
        Assert.Equal("Verified helper processes: 3", viewModel.VerifiedProcessCountText);
        Assert.Equal("Summed working set: 1.5 GiB", viewModel.WorkingSetText);
        Assert.Equal(CodexDesktopProcessSnapshotFormatter.Format(CompleteDesktopSnapshot()), viewModel.PreviewText);

        await Task.CompletedTask;
    }

    [Fact]
    public void DesktopProcessSnapshotViewModel_CancelIgnoresStaleCompletion_AndCopyStaysDisabled()
    {
        var service = new DeferredDesktopProcessSnapshotService();
        var clipboard = new RecordingClipboard();
        var viewModel = new DesktopProcessSnapshotViewModel(service, clipboard);

        viewModel.BeginMeasurement();
        viewModel.CancelMeasurement();
        service.CompleteNext(CompleteDesktopSnapshot());

        Assert.True(service.WaitForCompletionObserved(TimeSpan.FromSeconds(10)));
        Assert.Equal(DesktopProcessSnapshotPresentationState.Cancelled, viewModel.State);
        Assert.False(viewModel.CanCopyPreview);
        viewModel.CopyPreview();
        Assert.Null(clipboard.Text);
    }

    [Fact]
    public void DesktopProcessSnapshotViewModel_UsesOnlyTerminalCoreJsonAndGenericClipboardFeedback()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = new DesktopProcessSnapshotViewModel(new ImmediateDesktopProcessSnapshotService(PartialDesktopSnapshot()), clipboard);

        viewModel.BeginMeasurement();
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.State == DesktopProcessSnapshotPresentationState.Partial,
            TimeSpan.FromSeconds(2)));
        viewModel.CopyPreview();

        Assert.Equal(viewModel.PreviewText, clipboard.Text);
        Assert.Equal("Partial measurement", viewModel.StatusText);
        Assert.Equal("Summed working set: 768 MiB", viewModel.WorkingSetText);
        Assert.DoesNotContain("C:\\", viewModel.PreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("pid", viewModel.PreviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", viewModel.PreviewText, StringComparison.OrdinalIgnoreCase);

        var failure = new DesktopProcessSnapshotViewModel(new ImmediateDesktopProcessSnapshotService(PartialDesktopSnapshot()), new ThrowingClipboard());
        failure.BeginMeasurement();
        Assert.True(SpinWait.SpinUntil(() => failure.CanCopyPreview, TimeSpan.FromSeconds(2)));
        failure.CopyPreview();
        Assert.Equal("Could not copy the preview. Try again.", failure.CopyFeedback);
    }

    [Fact]
    public async Task DesktopProcessSnapshotViewModel_ClearsPreviousResultBeforeRemeasuring()
    {
        var service = new RemeasureDesktopProcessSnapshotService(CompleteDesktopSnapshot());
        var viewModel = new DesktopProcessSnapshotViewModel(service, new RecordingClipboard());

        viewModel.BeginMeasurement();
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.State == DesktopProcessSnapshotPresentationState.Complete,
            TimeSpan.FromSeconds(2)));

        viewModel.BeginMeasurement();
        await service.SecondCaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DesktopProcessSnapshotPresentationState.Measuring, viewModel.State);
        Assert.False(viewModel.CanCopyPreview);
        Assert.Equal(string.Empty, viewModel.PreviewText);
        Assert.Equal(string.Empty, viewModel.WorkingSetText);
        Assert.Equal("Verified helper processes: —", viewModel.VerifiedProcessCountText);

        service.CompleteSecond(PartialDesktopSnapshot());
        Assert.True(SpinWait.SpinUntil(
            () => viewModel.State == DesktopProcessSnapshotPresentationState.Partial,
            TimeSpan.FromSeconds(2)));
        Assert.Equal(CodexDesktopProcessSnapshotFormatter.Format(PartialDesktopSnapshot()), viewModel.PreviewText);
    }

    [Theory]
    [InlineData(CodexDesktopProcessSnapshotStatus.NoVerified, "No verified helper processes found")]
    [InlineData(CodexDesktopProcessSnapshotStatus.Unavailable, "Measurement unavailable")]
    public void DesktopProcessSnapshotViewModel_TerminalNoDataStatesRemainCopyableWithoutMemory(
        CodexDesktopProcessSnapshotStatus status,
        string expectedStatus)
    {
        var snapshot = new CodexDesktopProcessSnapshot(
            CodexDesktopProcessSnapshot.CurrentSchemaVersion,
            Now,
            status,
            0,
            null,
            true,
            0,
            CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);
        var clipboard = new RecordingClipboard();
        var viewModel = new DesktopProcessSnapshotViewModel(new ImmediateDesktopProcessSnapshotService(snapshot), clipboard);

        viewModel.BeginMeasurement();

        Assert.True(SpinWait.SpinUntil(() => viewModel.CanCopyPreview, TimeSpan.FromSeconds(2)));
        Assert.Equal(expectedStatus, viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.WorkingSetText);
        viewModel.CopyPreview();
        Assert.Equal(viewModel.PreviewText, clipboard.Text);
    }

    [Fact]
    public void DesktopProcessSnapshotViewModel_FormatsMemoryOnBothSidesOfOneGiB()
    {
        var underOneGiB = new CodexDesktopProcessSnapshot(
            CodexDesktopProcessSnapshot.CurrentSchemaVersion, Now, CodexDesktopProcessSnapshotStatus.Partial,
            1, 1_073_741_823, true, 1, CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);
        var exactlyOneGiB = new CodexDesktopProcessSnapshot(
            CodexDesktopProcessSnapshot.CurrentSchemaVersion, Now, CodexDesktopProcessSnapshotStatus.Complete,
            1, 1_073_741_824, true, 0, CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);

        var underOneViewModel = MeasureImmediately(underOneGiB);
        var exactlyOneViewModel = MeasureImmediately(exactlyOneGiB);

        Assert.Equal("Summed working set: 1024.0 MiB", underOneViewModel.WorkingSetText);
        Assert.Equal("Summed working set: 1 GiB", exactlyOneViewModel.WorkingSetText);
    }

    [Fact]
    public void DesktopProcessSnapshotPresenter_ReusesActiveDialogInsteadOfOpeningAnother()
    {
        var owner = new ImmediatePulseMeterWindow();
        FakeDesktopProcessSnapshotDialog? dialog = null;
        DesktopProcessSnapshotPresenter? presenter = null;
        presenter = new DesktopProcessSnapshotPresenter(
            new ImmediateDesktopProcessSnapshotService(CompleteDesktopSnapshot()),
            new RecordingClipboard(),
            owner,
            (_, _) => dialog = new FakeDesktopProcessSnapshotDialog(() => presenter!.ShowSnapshot()));

        presenter.ShowSnapshot();

        Assert.NotNull(dialog);
        Assert.Equal(1, dialog.ShowCount);
        Assert.Equal(1, dialog.ActivateCount);
    }

    [Fact]
    public void DesktopProcessSnapshotPresenter_ClearsSetupFailureAndAllowsRetry()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var owner = new TestOwnerWindow();
                var factoryCalls = 0;
                var failOwnerAssignment = true;
                var presenter = new DesktopProcessSnapshotPresenter(
                    new ImmediateDesktopProcessSnapshotService(CompleteDesktopSnapshot()),
                    new RecordingClipboard(),
                    owner,
                    (_, _) =>
                    {
                        factoryCalls++;
                        return new FakeDesktopProcessSnapshotDialog(() => { });
                    },
                    (dialog, ownerWindow) =>
                    {
                        if (failOwnerAssignment)
                        {
                            failOwnerAssignment = false;
                            throw new InvalidOperationException("owner setup failure");
                        }

                        dialog.Owner = ownerWindow;
                    });

                Assert.Throws<InvalidOperationException>(() => presenter.ShowSnapshot());
                presenter.ShowSnapshot();

                Assert.Equal(2, factoryCalls);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void DesktopProcessSnapshotWindow_ClosesMeasurementAndIgnoresStaleCompletion()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new DeferredDesktopProcessSnapshotService();
                var viewModel = new DesktopProcessSnapshotViewModel(service, new RecordingClipboard());
                var window = new DesktopProcessSnapshotWindow(viewModel);
                Assert.Equal(0, service.CaptureCount);

                viewModel.BeginMeasurement();
                window.Close();
                service.CompleteNext(CompleteDesktopSnapshot());

                Assert.True(service.WaitForCompletionObserved(TimeSpan.FromSeconds(10)));
                Assert.Equal(DesktopProcessSnapshotPresentationState.Cancelled, viewModel.State);
                Assert.False(viewModel.CanCopyPreview);
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (threadFailure is not null)
        {
            ExceptionDispatchInfo.Capture(threadFailure).Throw();
        }
    }

    [Fact]
    public void DesktopProcessSnapshotModal_DeclaresAccessibleKeyboardControlsAndNeutralCopy()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseMeter", "Slices", "SupportSnapshot", "UI", "DesktopProcessSnapshotWindow.xaml"));

        Assert.Contains("Title=\"Desktop process snapshot\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"Desktop process snapshot JSON preview\"", xaml);
        Assert.Contains("Command=\"{Binding MeasureNowCommand}\"", xaml);
        Assert.Contains("IsCancel=\"True\"", xaml);
        Assert.Contains("This cannot establish the cause of lag or a memory leak.", xaml);
        Assert.Contains("PulseMeter checks installed app package identity, executable paths, and signatures locally. It does not read chats, prompts, logs, command lines, window titles, or project files, and it does not upload measurements. Raw identity details are not included in the snapshot.", xaml);
        Assert.DoesNotContain("Desktop Health", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe to end", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("total RAM", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file contents", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static CodexDesktopProcessSnapshot CompleteDesktopSnapshot() => new(
        CodexDesktopProcessSnapshot.CurrentSchemaVersion,
        Now,
        CodexDesktopProcessSnapshotStatus.Complete,
        3,
        1_610_612_736,
        true,
        0,
        CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);

    private static CodexDesktopProcessSnapshot PartialDesktopSnapshot() => new(
        CodexDesktopProcessSnapshot.CurrentSchemaVersion,
        Now,
        CodexDesktopProcessSnapshotStatus.Partial,
        2,
        805_306_368,
        true,
        1,
        CodexDesktopProcessSnapshot.CurrentIdentityRuleVersion);

    private static DesktopProcessSnapshotViewModel MeasureImmediately(CodexDesktopProcessSnapshot snapshot)
    {
        var viewModel = new DesktopProcessSnapshotViewModel(new ImmediateDesktopProcessSnapshotService(snapshot), new RecordingClipboard());
        viewModel.BeginMeasurement();
        Assert.True(SpinWait.SpinUntil(() => viewModel.CanCopyPreview, TimeSpan.FromSeconds(2)));
        return viewModel;
    }

    private sealed class RecordingClipboard : IClipboardService { public string? Text { get; private set; } public void SetText(string text) => Text = text; }
    private sealed class ThrowingClipboard : IClipboardService { public void SetText(string text) => throw new InvalidOperationException("C:\\private\\clipboard"); }
    private sealed class ImmediateDesktopProcessSnapshotService(CodexDesktopProcessSnapshot snapshot) : ICodexDesktopProcessSnapshotService
    {
        public Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class DeferredDesktopProcessSnapshotService : ICodexDesktopProcessSnapshotService
    {
        private readonly Queue<TaskCompletionSource<CodexDesktopProcessSnapshot>> _completions = new();
        private readonly ManualResetEventSlim _completionObserved = new();
        public int CaptureCount { get; private set; }

        public async Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            var completion = new TaskCompletionSource<CodexDesktopProcessSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completions.Enqueue(completion);
            var snapshot = await completion.Task;
            _completionObserved.Set();
            return snapshot;
        }

        public void CompleteNext(CodexDesktopProcessSnapshot snapshot) => _completions.Dequeue().TrySetResult(snapshot);

        public bool WaitForCompletionObserved(TimeSpan timeout) => _completionObserved.Wait(timeout);
    }

    private sealed class RemeasureDesktopProcessSnapshotService(CodexDesktopProcessSnapshot firstSnapshot) : ICodexDesktopProcessSnapshotService
    {
        private readonly TaskCompletionSource<CodexDesktopProcessSnapshot> _secondCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondCaptureStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CaptureCount { get; private set; }

        public Task<CodexDesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            if (CaptureCount == 1)
            {
                return Task.FromResult(firstSnapshot);
            }

            SecondCaptureStarted.TrySetResult(true);
            return _secondCompletion.Task;
        }

        public void CompleteSecond(CodexDesktopProcessSnapshot snapshot) => _secondCompletion.TrySetResult(snapshot);
    }

    private sealed class FakeDesktopProcessSnapshotDialog : IDesktopProcessSnapshotDialog
    {
        private readonly Action _onShow;

        public FakeDesktopProcessSnapshotDialog(Action onShow) => _onShow = onShow;

        public event EventHandler? Closed;
        public System.Windows.Window? Owner { get; set; }
        public bool IsVisible { get; private set; }
        public int ShowCount { get; private set; }
        public int ActivateCount { get; private set; }

        public bool Activate()
        {
            ActivateCount++;
            return true;
        }

        public bool? ShowDialog()
        {
            ShowCount++;
            IsVisible = true;
            _onShow();
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    private sealed class ImmediatePulseMeterWindow : IPulseMeterWindow
    {
        public IntPtr Handle => IntPtr.Zero;
        public bool IsVisible => true;
        public System.Windows.WindowState WindowState { get; set; }
        public void Invoke(Action action) => action();
        public void Show() { }
        public void ShowWithoutActivation() { }
        public void ShowAndActivate() { }
        public void Hide() { }
        public void CloseForShutdown() { }
        public bool Activate() => true;
    }

    private sealed class TestOwnerWindow : System.Windows.Window, IPulseMeterWindow
    {
        IntPtr IPulseMeterWindow.Handle => IntPtr.Zero;
        bool IPulseMeterWindow.IsVisible => IsVisible;
        System.Windows.WindowState IPulseMeterWindow.WindowState
        {
            get => WindowState;
            set => WindowState = value;
        }

        void IPulseMeterWindow.Invoke(Action action) => action();
        void IPulseMeterWindow.Show() => Show();
        void IPulseMeterWindow.ShowWithoutActivation() => Show();
        void IPulseMeterWindow.ShowAndActivate() => Show();
        void IPulseMeterWindow.Hide() => Hide();
        void IPulseMeterWindow.CloseForShutdown() => Close();
        bool IPulseMeterWindow.Activate() => Activate();
    }
    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated { add { } remove { } }
        public bool UseMockMode { get; set; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new UsageSnapshot());
    }
}
