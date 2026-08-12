using System.ComponentModel;
using System.Globalization;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;
using PulseMeter.Slices.SupportSnapshot.Business;
using PulseMeter.Slices.SupportSnapshot.Models;

namespace PulseMeter.Slices.SupportSnapshot.UI;

public enum DesktopProcessSnapshotPresentationState
{
    Idle,
    Measuring,
    Complete,
    Partial,
    NoVerifiedProcesses,
    Unavailable,
    Cancelled
}

public sealed class DesktopProcessSnapshotViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ICodexDesktopProcessSnapshotService _snapshotService;
    private readonly IClipboardService _clipboardService;
    private CancellationTokenSource? _measurementCancellation;
    private int _measurementGeneration;
    private bool _isClosed;
    private DesktopProcessSnapshotPresentationState _state = DesktopProcessSnapshotPresentationState.Idle;
    private string _statusText = "Select Measure now to take a point-in-time measurement.";
    private string _verifiedProcessCountText = "Verified helper processes: —";
    private string _workingSetText = string.Empty;
    private string _previewText = string.Empty;
    private string _copyFeedback = string.Empty;

    public DesktopProcessSnapshotViewModel(
        ICodexDesktopProcessSnapshotService snapshotService,
        IClipboardService clipboardService)
    {
        _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        MeasureNowCommand = new RelayCommand(_ => BeginMeasurement(), _ => State != DesktopProcessSnapshotPresentationState.Measuring && !_isClosed);
        CopyPreviewCommand = new RelayCommand(_ => CopyPreview(), _ => CanCopyPreview);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand MeasureNowCommand { get; }

    public RelayCommand CopyPreviewCommand { get; }

    public DesktopProcessSnapshotPresentationState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string VerifiedProcessCountText
    {
        get => _verifiedProcessCountText;
        private set => SetProperty(ref _verifiedProcessCountText, value);
    }

    public string WorkingSetText
    {
        get => _workingSetText;
        private set => SetProperty(ref _workingSetText, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string CopyFeedback
    {
        get => _copyFeedback;
        private set => SetProperty(ref _copyFeedback, value);
    }

    public bool CanCopyPreview => HasTerminalSnapshot;

    public bool IsMeasuring => State == DesktopProcessSnapshotPresentationState.Measuring;

    public async void BeginMeasurement()
    {
        if (_isClosed || State == DesktopProcessSnapshotPresentationState.Measuring)
        {
            return;
        }

        var generation = ++_measurementGeneration;
        _measurementCancellation?.Dispose();
        _measurementCancellation = new CancellationTokenSource();
        State = DesktopProcessSnapshotPresentationState.Measuring;
        StatusText = "Measuring…";
        VerifiedProcessCountText = "Verified helper processes: —";
        WorkingSetText = string.Empty;
        PreviewText = string.Empty;
        CopyFeedback = string.Empty;
        OnMeasurementStateChanged();

        try
        {
            var snapshot = await _snapshotService.CaptureAsync(_measurementCancellation.Token);
            if (!CanApply(generation))
            {
                return;
            }

            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException) when (_isClosed || generation != _measurementGeneration || _measurementCancellation.IsCancellationRequested)
        {
            if (CanApply(generation))
            {
                State = DesktopProcessSnapshotPresentationState.Cancelled;
                StatusText = "Measurement cancelled.";
                OnMeasurementStateChanged();
            }
        }
        catch (Exception)
        {
            if (CanApply(generation))
            {
                State = DesktopProcessSnapshotPresentationState.Unavailable;
                StatusText = "Measurement unavailable";
                VerifiedProcessCountText = "Verified helper processes: —";
                WorkingSetText = string.Empty;
                PreviewText = string.Empty;
                OnMeasurementStateChanged();
            }
        }
    }

    public void CancelMeasurement()
    {
        _isClosed = true;
        _measurementGeneration++;
        _measurementCancellation?.Cancel();
        if (State == DesktopProcessSnapshotPresentationState.Measuring)
        {
            State = DesktopProcessSnapshotPresentationState.Cancelled;
            StatusText = "Measurement cancelled.";
        }
        OnMeasurementStateChanged();
    }

    public void Dispose()
    {
        CancelMeasurement();
        _measurementCancellation?.Dispose();
        _measurementCancellation = null;
    }

    private void ApplySnapshot(CodexDesktopProcessSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        State = snapshot.Status switch
        {
            CodexDesktopProcessSnapshotStatus.Complete => DesktopProcessSnapshotPresentationState.Complete,
            CodexDesktopProcessSnapshotStatus.Partial => DesktopProcessSnapshotPresentationState.Partial,
            CodexDesktopProcessSnapshotStatus.NoVerified => DesktopProcessSnapshotPresentationState.NoVerifiedProcesses,
            CodexDesktopProcessSnapshotStatus.Unavailable => DesktopProcessSnapshotPresentationState.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot))
        };
        StatusText = State switch
        {
            DesktopProcessSnapshotPresentationState.Complete => "Measurement complete",
            DesktopProcessSnapshotPresentationState.Partial => "Partial measurement",
            DesktopProcessSnapshotPresentationState.NoVerifiedProcesses => "No verified helper processes found",
            DesktopProcessSnapshotPresentationState.Unavailable => "Measurement unavailable",
            _ => throw new InvalidOperationException("A terminal measurement state was expected.")
        };
        VerifiedProcessCountText = $"Verified helper processes: {snapshot.VerifiedProcessCount}";
        WorkingSetText = snapshot.VerifiedProcessCount > 0 && snapshot.SummedWorkingSetBytes is long bytes
            ? $"Summed working set: {FormatMemory(bytes)}"
            : string.Empty;
        PreviewText = CodexDesktopProcessSnapshotFormatter.Format(snapshot);
        OnMeasurementStateChanged();
    }

    public void CopyPreview()
    {
        if (!CanCopyPreview)
        {
            return;
        }

        try
        {
            _clipboardService.SetText(PreviewText);
            CopyFeedback = "Preview copied.";
        }
        catch (Exception)
        {
            CopyFeedback = "Could not copy the preview. Try again.";
        }
    }

    private bool HasTerminalSnapshot => State is DesktopProcessSnapshotPresentationState.Complete
        or DesktopProcessSnapshotPresentationState.Partial
        or DesktopProcessSnapshotPresentationState.NoVerifiedProcesses
        or DesktopProcessSnapshotPresentationState.Unavailable
        && !string.IsNullOrEmpty(PreviewText);

    private bool CanApply(int generation) => !_isClosed && generation == _measurementGeneration;

    private void OnMeasurementStateChanged()
    {
        OnPropertyChanged(nameof(IsMeasuring));
        OnPropertyChanged(nameof(CanCopyPreview));
        MeasureNowCommand.RaiseCanExecuteChanged();
        CopyPreviewCommand.RaiseCanExecuteChanged();
    }

    private static string FormatMemory(long bytes)
    {
        const double bytesPerMiB = 1024d * 1024d;
        const double bytesPerGiB = 1024d * 1024d * 1024d;
        var value = bytes >= bytesPerGiB ? bytes / bytesPerGiB : bytes / bytesPerMiB;
        var unit = bytes >= bytesPerGiB ? "GiB" : "MiB";
        return $"{value.ToString(value % 1d == 0d ? "0" : "0.0", CultureInfo.InvariantCulture)} {unit}";
    }

    private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
