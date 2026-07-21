using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Timing;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;
using PulseMeter.Slices.UsageSignals;

namespace PulseMeter.Slices.NeedsAttention.UI;

public sealed class NeedsAttentionSectionViewModel : INotifyPropertyChanged
{
    private const int PreviewItemCount = 3;
    private readonly INeedsAttentionPresenter _presenter;
    private readonly IUsageSignalsTracker? _usageSignalsTracker;
    private readonly IClipboardService? _clipboardService;
    private readonly IPulseMeterTimer? _copyFeedbackTimer;
    private readonly IPulseMeterTimer? _dismissUndoTimer;
    private UsageSignalsSnapshot _signals = UsageSignalsSnapshot.Empty;
    private UsageAttentionSignal? _pendingDismissedSignal;
    private int _pendingDismissedSignalIndex;
    private string _copyFeedbackText = string.Empty;
    private string _copyFeedbackBrush = "#15803D";
    private bool _isShowingAll;

    public NeedsAttentionSectionViewModel(
        INeedsAttentionPresenter presenter,
        IUsageSignalsTracker? usageSignalsTracker = null,
        IClipboardService? clipboardService = null,
        IPulseMeterTimerFactory? timerFactory = null)
    {
        _presenter = presenter;
        _usageSignalsTracker = usageSignalsTracker;
        _clipboardService = clipboardService;
        DismissSignalCommand = new RelayCommand(DismissSignal);
        CopyDiagnosticCommand = new RelayCommand(CopyDiagnostic, parameter =>
            parameter is NeedsAttentionItem item && item.CanCopyDiagnostic);
        ToggleAttentionItemsCommand = new RelayCommand(ToggleAttentionItems);
        UndoDismissCommand = new RelayCommand(UndoDismiss, _ => HasPendingDismissal);

        if (timerFactory is not null)
        {
            _copyFeedbackTimer = timerFactory.Create(TimeSpan.FromSeconds(3));
            _copyFeedbackTimer.Tick += CopyFeedbackTimer_OnTick;
            _dismissUndoTimer = timerFactory.Create(TimeSpan.FromSeconds(8));
            _dismissUndoTimer.Tick += DismissUndoTimer_OnTick;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NeedsAttentionItem> NeedsAttentionItems { get; } = new();

    public ObservableCollection<NeedsAttentionItem> VisibleNeedsAttentionItems { get; } = new();

    public RelayCommand DismissSignalCommand { get; }

    public RelayCommand CopyDiagnosticCommand { get; }

    public RelayCommand ToggleAttentionItemsCommand { get; }

    public RelayCommand UndoDismissCommand { get; }

    public bool HasNeedsAttention => NeedsAttentionItems.Count > 0;

    public bool HasHiddenAttentionItems => NeedsAttentionItems.Count > PreviewItemCount;

    public bool IsShowingAll
    {
        get => _isShowingAll;
        private set
        {
            if (_isShowingAll == value)
            {
                return;
            }

            _isShowingAll = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToggleAttentionItemsText));
            OnPropertyChanged(nameof(ToggleAttentionItemsAccessibleLabel));
        }
    }

    public string ToggleAttentionItemsText => IsShowingAll
        ? "Show top 3"
        : $"Show all {NeedsAttentionItems.Count}";

    public string ToggleAttentionItemsAccessibleLabel => IsShowingAll
        ? "Show top 3 attention signals"
        : $"Show all {NeedsAttentionItems.Count} attention signals";

    public bool HasCopyFeedback => !string.IsNullOrWhiteSpace(CopyFeedbackText);

    public bool HasPendingDismissal => _pendingDismissedSignal is not null;

    public string DismissUndoText => "Idle alert dismissed.";

    public string CopyFeedbackText
    {
        get => _copyFeedbackText;
        private set
        {
            if (_copyFeedbackText == value)
            {
                return;
            }

            _copyFeedbackText = value;
            OnPropertyChanged(nameof(HasCopyFeedback));
            OnPropertyChanged();
        }
    }

    public string CopyFeedbackBrush
    {
        get => _copyFeedbackBrush;
        private set
        {
            if (_copyFeedbackBrush == value)
            {
                return;
            }

            _copyFeedbackBrush = value;
            OnPropertyChanged();
        }
    }

    public string NeedsAttentionSummaryText => NeedsAttentionItems.Count switch
    {
        0 => "All clear",
        1 => "1 signal",
        _ => $"{NeedsAttentionItems.Count} signals"
    };

    public void ApplySignals(UsageSignalsSnapshot signals)
    {
        if (_pendingDismissedSignal is not null)
        {
            var incomingSignals = signals.AttentionSignals.ToList();
            var incomingIndex = incomingSignals.FindIndex(signal => signal.DismissSignalId == _pendingDismissedSignal.DismissSignalId);
            if (incomingIndex >= 0)
            {
                _pendingDismissedSignal = incomingSignals[incomingIndex];
                _pendingDismissedSignalIndex = incomingIndex;
                incomingSignals.RemoveAt(incomingIndex);
                signals = WithAttentionSignals(signals, incomingSignals);
            }
            else
            {
                _dismissUndoTimer?.Stop();
                ClearPendingDismissal();
            }
        }

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

        if (!HasHiddenAttentionItems)
        {
            IsShowingAll = false;
        }

        RefreshVisibleItems();

        OnPropertyChanged(nameof(HasNeedsAttention));
        OnPropertyChanged(nameof(HasHiddenAttentionItems));
        OnPropertyChanged(nameof(NeedsAttentionSummaryText));
        OnPropertyChanged(nameof(ToggleAttentionItemsText));
        OnPropertyChanged(nameof(ToggleAttentionItemsAccessibleLabel));
    }

    private void ToggleAttentionItems(object? parameter)
    {
        if (!HasHiddenAttentionItems)
        {
            return;
        }

        IsShowingAll = !IsShowingAll;
        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        VisibleNeedsAttentionItems.Clear();
        var items = IsShowingAll
            ? NeedsAttentionItems
            : NeedsAttentionItems.Take(PreviewItemCount);

        foreach (var item in items)
        {
            VisibleNeedsAttentionItems.Add(item);
        }
    }

    private void DismissSignal(object? parameter)
    {
        if (parameter is not NeedsAttentionItem { DismissSignalId: "idle-drain" } item)
        {
            return;
        }

        var attentionSignals = _signals.AttentionSignals.ToList();
        var signalIndex = attentionSignals.FindIndex(signal => signal.DismissSignalId == item.DismissSignalId);
        if (signalIndex < 0)
        {
            return;
        }

        var originalSignal = attentionSignals[signalIndex];
        attentionSignals.RemoveAt(signalIndex);
        _signals = WithAttentionSignals(_signals, attentionSignals);

        if (_dismissUndoTimer is null)
        {
            _usageSignalsTracker?.DismissIdleDrain();
            Refresh();
            return;
        }

        _dismissUndoTimer.Stop();
        _pendingDismissedSignal = originalSignal;
        _pendingDismissedSignalIndex = signalIndex;
        OnPropertyChanged(nameof(HasPendingDismissal));
        UndoDismissCommand.RaiseCanExecuteChanged();
        _dismissUndoTimer.Start();
        Refresh();
    }

    private void UndoDismiss(object? parameter)
    {
        if (_pendingDismissedSignal is null)
        {
            return;
        }

        _dismissUndoTimer?.Stop();
        var attentionSignals = _signals.AttentionSignals.ToList();
        if (attentionSignals.All(signal => signal.DismissSignalId != _pendingDismissedSignal.DismissSignalId))
        {
            attentionSignals.Insert(Math.Min(_pendingDismissedSignalIndex, attentionSignals.Count), _pendingDismissedSignal);
            _signals = WithAttentionSignals(_signals, attentionSignals);
        }

        ClearPendingDismissal();
        Refresh();
    }

    private void DismissUndoTimer_OnTick(object? sender, EventArgs e)
    {
        _dismissUndoTimer?.Stop();
        if (_pendingDismissedSignal is null)
        {
            return;
        }

        _usageSignalsTracker?.DismissIdleDrain();
        ClearPendingDismissal();
    }

    private void ClearPendingDismissal()
    {
        _pendingDismissedSignal = null;
        _pendingDismissedSignalIndex = 0;
        OnPropertyChanged(nameof(HasPendingDismissal));
        UndoDismissCommand.RaiseCanExecuteChanged();
    }

    private static UsageSignalsSnapshot WithAttentionSignals(
        UsageSignalsSnapshot source,
        IReadOnlyList<UsageAttentionSignal> attentionSignals)
    {
        return new UsageSignalsSnapshot
        {
            RunwaySignals = source.RunwaySignals,
            RunwayForecasts = source.RunwayForecasts,
            IdleDrainIncident = source.IdleDrainIncident,
            ShowAllAttentionSignals = source.ShowAllAttentionSignals,
            AttentionSignals = attentionSignals
        };
    }

    private void CopyDiagnostic(object? parameter)
    {
        if (parameter is not NeedsAttentionItem item || string.IsNullOrWhiteSpace(item.DiagnosticText))
        {
            return;
        }

        if (_clipboardService is null)
        {
            return;
        }

        try
        {
            _clipboardService.SetText(item.DiagnosticText);
            ShowCopyFeedback("Diagnostic copied", "#15803D");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            ShowCopyFeedback("Couldn't copy. Try again.", "#DC2626");
        }
    }

    private void ShowCopyFeedback(string message, string brush)
    {
        _copyFeedbackTimer?.Stop();

        // Clearing first ensures repeating the same action is announced again.
        CopyFeedbackText = string.Empty;
        CopyFeedbackBrush = brush;
        CopyFeedbackText = message;
        _copyFeedbackTimer?.Start();
    }

    private void CopyFeedbackTimer_OnTick(object? sender, EventArgs e)
    {
        _copyFeedbackTimer?.Stop();
        CopyFeedbackText = string.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
