using System.ComponentModel;
using System.Runtime.CompilerServices;
using PulseMeter.Platform.Windows;
using PulseMeter.Shared.Commands;
using PulseMeter.Slices.ReturnNote.Business;
using ReturnNoteModel = PulseMeter.Slices.ReturnNote.Models.ReturnNote;

namespace PulseMeter.Slices.ReturnNote.UI;

public sealed class ReturnNoteSectionViewModel : INotifyPropertyChanged
{
    private readonly IClipboardService _clipboard;
    private ReturnNoteModel? _savedNote;
    private bool _isEditing;
    private bool _isConfirmingClear;
    private string _forText = string.Empty;
    private string _nextStep = string.Empty;
    private string _statusText = string.Empty;

    public ReturnNoteSectionViewModel(IClipboardService clipboard) { _clipboard = clipboard; AddNoteCommand = new RelayCommand(_ => BeginEdit()); EditCommand = new RelayCommand(_ => BeginEdit()); SaveCommand = new RelayCommand(_ => Save()); CancelCommand = new RelayCommand(_ => Cancel()); CopyNextStepCommand = new RelayCommand(_ => Copy()); ClearCommand = new RelayCommand(_ => IsConfirmingClear = true); ConfirmClearCommand = new RelayCommand(_ => Clear()); CancelClearCommand = new RelayCommand(_ => IsConfirmingClear = false); }
    public static ReturnNoteSectionViewModel CreateEmpty() => new(new EmptyClipboard());
    public event PropertyChangedEventHandler? PropertyChanged;
    public RelayCommand AddNoteCommand { get; } public RelayCommand EditCommand { get; } public RelayCommand SaveCommand { get; } public RelayCommand CancelCommand { get; } public RelayCommand CopyNextStepCommand { get; } public RelayCommand ClearCommand { get; } public RelayCommand ConfirmClearCommand { get; } public RelayCommand CancelClearCommand { get; }
    public bool HasSavedNote => _savedNote is not null;
    public bool IsEditing { get => _isEditing; private set { if (Set(ref _isEditing, value)) RefreshStates(); } }
    public bool IsConfirmingClear { get => _isConfirmingClear; private set { if (Set(ref _isConfirmingClear, value)) RefreshStates(); } }
    public bool IsEmpty => !HasSavedNote && !IsEditing;
    public bool IsViewing => HasSavedNote && !IsEditing && !IsConfirmingClear;
    public string ForText { get => _forText; set => Set(ref _forText, value); }
    public string NextStep { get => _nextStep; set => Set(ref _nextStep, value); }
    public string SavedFor => _savedNote?.For ?? string.Empty;
    public string SavedNextStep => _savedNote?.NextStep ?? string.Empty;
    public string StatusText { get => _statusText; private set { if (Set(ref _statusText, value)) OnChanged(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);
    private void BeginEdit() { IsConfirmingClear = false; if (_savedNote is not null) { ForText = _savedNote.For; NextStep = _savedNote.NextStep; } IsEditing = true; StatusText = string.Empty; }
    private void Save() { var validation = ReturnNoteValidator.Validate(ForText, NextStep); if (!validation.IsValid || validation.Note is null) { StatusText = validation.Error; return; } _savedNote = validation.Note; ForText = validation.Note.For; NextStep = validation.Note.NextStep; IsEditing = false; RefreshStates(); OnChanged(nameof(SavedFor)); OnChanged(nameof(SavedNextStep)); StatusText = "Note saved."; }
    private void Cancel() { if (_savedNote is null) { ForText = NextStep = string.Empty; } else { ForText = _savedNote.For; NextStep = _savedNote.NextStep; } IsEditing = false; StatusText = string.Empty; }
    private void Copy() { if (_savedNote is null) return; try { _clipboard.SetText(_savedNote.NextStep); StatusText = "Next step copied."; } catch (Exception) { StatusText = "Couldn't copy. Try again."; } }
    private void Clear() { _savedNote = null; ForText = NextStep = string.Empty; IsConfirmingClear = false; IsEditing = false; RefreshStates(); OnChanged(nameof(SavedFor)); OnChanged(nameof(SavedNextStep)); StatusText = "Note cleared."; }
    private void RefreshStates() { OnChanged(nameof(HasSavedNote)); OnChanged(nameof(IsEmpty)); OnChanged(nameof(IsViewing)); }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnChanged(name); return true; }
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private sealed class EmptyClipboard : IClipboardService { public void SetText(string text) { } }
}
