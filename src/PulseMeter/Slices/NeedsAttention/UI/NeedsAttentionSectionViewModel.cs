using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;
using PulseMeter.Slices.UsageSignals;

namespace PulseMeter.Slices.NeedsAttention.UI;

public sealed class NeedsAttentionSectionViewModel : INotifyPropertyChanged
{
    private readonly INeedsAttentionPresenter _presenter;
    private readonly IUsageSignalsTracker? _usageSignalsTracker;
    private readonly IClipboardService? _clipboardService;
    private UsageSignalsSnapshot _signals = UsageSignalsSnapshot.Empty;

    public NeedsAttentionSectionViewModel(
        INeedsAttentionPresenter presenter,
        IUsageSignalsTracker? usageSignalsTracker = null,
        IClipboardService? clipboardService = null)
    {
        _presenter = presenter;
        _usageSignalsTracker = usageSignalsTracker;
        _clipboardService = clipboardService;
        DismissSignalCommand = new RelayCommand(DismissSignal);
        CopyDiagnosticCommand = new RelayCommand(CopyDiagnostic, parameter =>
            parameter is NeedsAttentionItem item && item.CanCopyDiagnostic);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NeedsAttentionItem> NeedsAttentionItems { get; } = new();

    public RelayCommand DismissSignalCommand { get; }

    public RelayCommand CopyDiagnosticCommand { get; }

    public bool HasNeedsAttention => NeedsAttentionItems.Count > 0;

    public string NeedsAttentionSummaryText => NeedsAttentionItems.Count == 1
        ? "1 signal"
        : $"{NeedsAttentionItems.Count} signals";

    public void ApplySignals(UsageSignalsSnapshot signals)
    {
        _signals = signals;
        Refresh();
    }

    public void Refresh(DateTimeOffset now)
    {
        Refresh();
    }

    private void Refresh()
    {
        NeedsAttentionItems.Clear();
        foreach (var item in _presenter.BuildItems(_signals))
        {
            NeedsAttentionItems.Add(item);
        }

        OnPropertyChanged(nameof(HasNeedsAttention));
        OnPropertyChanged(nameof(NeedsAttentionSummaryText));
    }

    private void DismissSignal(object? parameter)
    {
        if (parameter is not NeedsAttentionItem { DismissSignalId: "idle-drain" })
        {
            return;
        }

        _usageSignalsTracker?.DismissIdleDrain();
        _signals = new UsageSignalsSnapshot
        {
            RunwaySignals = _signals.RunwaySignals,
            ShowAllAttentionSignals = _signals.ShowAllAttentionSignals,
            AttentionSignals = _signals.AttentionSignals
                .Where(signal => signal.DismissSignalId != "idle-drain")
                .ToList()
        };
        Refresh();
    }

    private void CopyDiagnostic(object? parameter)
    {
        if (parameter is not NeedsAttentionItem item || string.IsNullOrWhiteSpace(item.DiagnosticText))
        {
            return;
        }

        _clipboardService?.SetText(item.DiagnosticText);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
