using System.Collections.ObjectModel;
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
    private readonly IReturnNoteStateStore _stateStore;
    private ReturnNoteListItem? _editingNote;
    private bool _isEditing;
    private string _forText = string.Empty;
    private string _nextStep = string.Empty;
    private string _statusText = string.Empty;

    public ReturnNoteSectionViewModel(IClipboardService clipboard, IReturnNoteStateStore stateStore)
    {
        _clipboard = clipboard;
        _stateStore = stateStore;
        AddNoteCommand = new RelayCommand(_ => BeginAdd());
        EditNoteCommand = new RelayCommand(BeginEdit);
        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => Cancel());
        CopyNextStepCommand = new RelayCommand(Copy);
        RemoveNoteCommand = new RelayCommand(RequestRemove);
        ConfirmRemoveCommand = new RelayCommand(ConfirmRemove);
        CancelRemoveCommand = new RelayCommand(CancelRemove);
        RestoreSavedNotes();
    }

    internal ReturnNoteSectionViewModel(IClipboardService clipboard)
        : this(clipboard, new VolatileReturnNoteStateStore())
    {
    }

    public static ReturnNoteSectionViewModel CreateEmpty() =>
        new(new EmptyClipboard(), new VolatileReturnNoteStateStore());

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ReturnNoteListItem> Notes { get; } = [];
    public RelayCommand AddNoteCommand { get; }
    public RelayCommand EditNoteCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopyNextStepCommand { get; }
    public RelayCommand RemoveNoteCommand { get; }
    public RelayCommand ConfirmRemoveCommand { get; }
    public RelayCommand CancelRemoveCommand { get; }

    public bool HasNotes => Notes.Count > 0;
    public bool IsEmpty => !HasNotes && !IsEditing;
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (Set(ref _isEditing, value))
            {
                OnChanged(nameof(IsNotEditing));
                OnChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsNotEditing => !IsEditing;
    public string EditorTitle => _editingNote is null ? "Add a return note" : "Edit return note";
    public string NoteCountText => Notes.Count == 1 ? "1 note" : $"{Notes.Count} notes";
    public string ForText { get => _forText; set => Set(ref _forText, value); }
    public string NextStep { get => _nextStep; set => Set(ref _nextStep, value); }
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (Set(ref _statusText, value))
            {
                OnChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    private void BeginAdd()
    {
        ClearRemovalConfirmations();
        _editingNote = null;
        ForText = string.Empty;
        NextStep = string.Empty;
        OnChanged(nameof(EditorTitle));
        IsEditing = true;
        StatusText = string.Empty;
    }

    private void BeginEdit(object? parameter)
    {
        if (parameter is not ReturnNoteListItem note)
        {
            return;
        }

        ClearRemovalConfirmations();
        _editingNote = note;
        ForText = note.For;
        NextStep = note.NextStep;
        OnChanged(nameof(EditorTitle));
        IsEditing = true;
        StatusText = string.Empty;
    }

    private void Save()
    {
        var validation = ReturnNoteValidator.Validate(ForText, NextStep);
        if (!validation.IsValid || validation.Note is null)
        {
            StatusText = validation.Error;
            return;
        }

        var wasEditing = _editingNote is not null;
        if (!wasEditing && Notes.Count >= ReturnNoteStateStore.MaximumPersistedNotes)
        {
            StatusText = $"Keep up to {ReturnNoteStateStore.MaximumPersistedNotes} saved notes.";
            return;
        }

        var proposedNotes = Notes
            .Select(note => new ReturnNoteModel(note.For, note.NextStep))
            .ToList();
        if (_editingNote is null)
        {
            proposedNotes.Add(validation.Note);
        }
        else
        {
            var editingIndex = Notes.IndexOf(_editingNote);
            if (editingIndex < 0)
            {
                Cancel();
                return;
            }

            proposedNotes[editingIndex] = validation.Note;
        }

        if (!_stateStore.Save(proposedNotes))
        {
            StatusText = "Couldn't save the note. Try again.";
            return;
        }

        if (_editingNote is null)
        {
            Notes.Add(new ReturnNoteListItem(validation.Note.For, validation.Note.NextStep));
        }
        else
        {
            _editingNote.Update(validation.Note.For, validation.Note.NextStep);
        }

        FinishEditing();
        NotifyNoteCollectionChanged();
        StatusText = wasEditing ? "Note updated." : "Note added.";
    }

    private void Cancel()
    {
        FinishEditing();
        StatusText = string.Empty;
    }

    private void Copy(object? parameter)
    {
        if (parameter is not ReturnNoteListItem note)
        {
            return;
        }

        try
        {
            _clipboard.SetText(note.NextStep);
            StatusText = "Next step copied.";
        }
        catch (Exception)
        {
            StatusText = "Couldn't copy. Try again.";
        }
    }

    private void RequestRemove(object? parameter)
    {
        if (parameter is not ReturnNoteListItem note)
        {
            return;
        }

        ClearRemovalConfirmations();
        note.IsPendingRemoval = true;
        StatusText = string.Empty;
    }

    private void ConfirmRemove(object? parameter)
    {
        if (parameter is not ReturnNoteListItem note || !Notes.Contains(note))
        {
            return;
        }

        var proposedNotes = Notes
            .Where(candidate => !ReferenceEquals(candidate, note))
            .Select(candidate => new ReturnNoteModel(candidate.For, candidate.NextStep))
            .ToArray();
        if (!_stateStore.Save(proposedNotes))
        {
            StatusText = "Couldn't remove the note. Try again.";
            return;
        }

        Notes.Remove(note);

        NotifyNoteCollectionChanged();
        StatusText = "Note removed.";
    }

    private static void CancelRemove(object? parameter)
    {
        if (parameter is ReturnNoteListItem note)
        {
            note.IsPendingRemoval = false;
        }
    }

    private void FinishEditing()
    {
        _editingNote = null;
        ForText = string.Empty;
        NextStep = string.Empty;
        OnChanged(nameof(EditorTitle));
        IsEditing = false;
    }

    private void ClearRemovalConfirmations()
    {
        foreach (var note in Notes)
        {
            note.IsPendingRemoval = false;
        }
    }

    private void NotifyNoteCollectionChanged()
    {
        OnChanged(nameof(HasNotes));
        OnChanged(nameof(IsEmpty));
        OnChanged(nameof(NoteCountText));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnChanged(name);
        return true;
    }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RestoreSavedNotes()
    {
        var result = _stateStore.Load();
        if (result.Status == ReturnNoteLoadStatus.Loaded && result.Notes is not null)
        {
            foreach (var note in result.Notes)
            {
                Notes.Add(new ReturnNoteListItem(note.For, note.NextStep));
            }

            return;
        }

        StatusText = result.Status switch
        {
            ReturnNoteLoadStatus.Corrupt => "Saved notes couldn't be read. New notes can still be saved.",
            ReturnNoteLoadStatus.Unavailable => "Saved notes are temporarily unavailable.",
            _ => string.Empty
        };
    }

    private sealed class EmptyClipboard : IClipboardService
    {
        public void SetText(string text) { }
    }

    private sealed class VolatileReturnNoteStateStore : IReturnNoteStateStore
    {
        public ReturnNoteLoadResult Load() => new(ReturnNoteLoadStatus.Missing);

        public bool Save(IReadOnlyList<ReturnNoteModel> notes) => true;
    }
}

public sealed class ReturnNoteListItem : INotifyPropertyChanged
{
    private string _for;
    private string _nextStep;
    private bool _isPendingRemoval;

    internal ReturnNoteListItem(string forText, string nextStep)
    {
        _for = forText;
        _nextStep = nextStep;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string For => _for;
    public string NextStep => _nextStep;
    public bool IsPendingRemoval
    {
        get => _isPendingRemoval;
        set
        {
            if (_isPendingRemoval == value)
            {
                return;
            }

            _isPendingRemoval = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPendingRemoval)));
        }
    }

    internal void Update(string forText, string nextStep)
    {
        _for = forText;
        _nextStep = nextStep;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(For)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextStep)));
    }
}
