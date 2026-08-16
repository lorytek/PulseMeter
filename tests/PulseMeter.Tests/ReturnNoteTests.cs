using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ReturnNote.Business;
using PulseMeter.Slices.ReturnNote.Models;
using PulseMeter.Slices.ReturnNote.UI;
using PulseMeter.VisualHarness;

namespace PulseMeter.Tests;

public sealed class ReturnNoteTests
{
    [Fact]
    public void Validator_NormalizesScalars_AllowsNextStepLines_AndRejectsUnsafeCharacters()
    {
        var valid = ReturnNoteValidator.Validate(" e\u0301🙂 ", "first\r\nsecond");
        Assert.True(valid.IsValid);
        Assert.Equal("é🙂", valid.Note!.For);
        Assert.Equal("first\nsecond", valid.Note.NextStep);
        Assert.False(ReturnNoteValidator.Validate("for\nline", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\t", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for", "next\tstep").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u202E", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u061C", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u200E", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u200F", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("\uD800", "next").IsValid);
        Assert.True(ReturnNoteValidator.Validate(string.Concat(Enumerable.Repeat("🙂", 80)), string.Concat(Enumerable.Repeat("🙂", 240))).IsValid);
        Assert.False(ReturnNoteValidator.Validate(string.Concat(Enumerable.Repeat("🙂", 81)), "next").IsValid);
    }

    [Fact]
    public void ViewModel_AddsEditsAndRemovesMultipleIndependentNotes()
    {
        var viewModel = new ReturnNoteSectionViewModel(new RecordingClipboard());

        AddNote(viewModel, "release", "run tests");
        AddNote(viewModel, "docs", "update README\ncheck links");

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal("2 notes", viewModel.NoteCountText);
        Assert.Equal("release", viewModel.Notes[0].For);
        Assert.Equal("update README\ncheck links", viewModel.Notes[1].NextStep);

        var first = viewModel.Notes[0];
        viewModel.EditNoteCommand.Execute(first);
        viewModel.NextStep = "run all tests";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("run all tests", first.NextStep);
        Assert.Equal("update README\ncheck links", viewModel.Notes[1].NextStep);

        viewModel.RemoveNoteCommand.Execute(first);
        Assert.True(first.IsPendingRemoval);
        viewModel.CancelRemoveCommand.Execute(first);
        Assert.False(first.IsPendingRemoval);
        viewModel.RemoveNoteCommand.Execute(first);
        viewModel.ConfirmRemoveCommand.Execute(first);

        Assert.Single(viewModel.Notes);
        Assert.Equal("1 note", viewModel.NoteCountText);
        Assert.Equal("docs", viewModel.Notes[0].For);
    }

    [Fact]
    public void ViewModel_CopiesOnlySelectedNextStepAndPreservesNotesOnClipboardFailure()
    {
        var clipboard = new RecordingClipboard();
        var viewModel = new ReturnNoteSectionViewModel(clipboard);
        AddNote(viewModel, "one", "First next");
        AddNote(viewModel, "two", "Second next");

        viewModel.CopyNextStepCommand.Execute(viewModel.Notes[1]);
        Assert.Equal("Second next", clipboard.Text);

        var failing = new ReturnNoteSectionViewModel(new ThrowingClipboard());
        AddNote(failing, "for", "Only next");
        failing.CopyNextStepCommand.Execute(failing.Notes[0]);
        Assert.Single(failing.Notes);
        Assert.Equal("Only next", failing.Notes[0].NextStep);
        Assert.Equal("Couldn't copy. Try again.", failing.StatusText);
    }

    [Fact]
    public void ViewModel_DoesNotClaimSuccessWhenPersistentSaveFails()
    {
        var viewModel = new ReturnNoteSectionViewModel(new RecordingClipboard(), new FailingStateStore());

        viewModel.AddNoteCommand.Execute(null);
        viewModel.ForText = "release";
        viewModel.NextStep = "run tests";
        viewModel.SaveCommand.Execute(null);

        Assert.Empty(viewModel.Notes);
        Assert.True(viewModel.IsEditing);
        Assert.Equal("Couldn't save the note. Try again.", viewModel.StatusText);
    }

    [Fact]
    public void StateStore_ProtectsPayloadAndRoundTripsSeveralNotes()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PulseMeter.ReturnNotes", Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "return-notes.v1.dat");
        var store = new ReturnNoteStateStore(path);
        var notes = new[]
        {
            new ReturnNote("private project label", "private next action"),
            new ReturnNote("release", "publish package")
        };

        Assert.True(store.Save(notes));
        var persisted = File.ReadAllText(path);
        Assert.DoesNotContain("private project label", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("private next action", persisted, StringComparison.Ordinal);

        var loaded = new ReturnNoteStateStore(path).Load();
        Assert.Equal(ReturnNoteLoadStatus.Loaded, loaded.Status);
        Assert.Equal(notes, loaded.Notes);

        Directory.Delete(directory, true);
    }

    [Fact]
    public void StateStore_RejectsUnknownOrCorruptEnvelopes()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PulseMeter.ReturnNotes", Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(directory, "return-notes.v1.dat");
        File.WriteAllText(path, JsonSerializer.Serialize(new ReturnNoteStateStore.ReturnNoteEnvelope(99, "not-base64")));

        Assert.Equal(ReturnNoteLoadStatus.Corrupt, new ReturnNoteStateStore(path).Load().Status);

        File.WriteAllText(path, "{broken");
        Assert.Equal(ReturnNoteLoadStatus.Corrupt, new ReturnNoteStateStore(path).Load().Status);

        Directory.Delete(directory, true);
    }

    [Fact]
    public void Xaml_UsesRoundedInputsMultilineEditorAndPerNoteActions()
    {
        var root = TestWorkspace.FindRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml"));
        var note = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "ReturnNote", "UI", "ReturnNoteSection.xaml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Shared", "Styles", "PulseMeterControls.xaml"));

        Assert.True(window.IndexOf("<needsAttention:NeedsAttentionSection", StringComparison.Ordinal) < window.IndexOf("<returnNote:ReturnNoteSection", StringComparison.Ordinal));
        Assert.Contains("Text=\"RETURN NOTES\"", note);
        Assert.Contains("Text=\"SAVED LOCALLY\"", note);
        Assert.Contains("Style=\"{DynamicResource LightTextBoxStyle}\"", note);
        Assert.Contains("Style=\"{DynamicResource LightMultilineTextBoxStyle}\"", note);
        Assert.Contains("ItemsSource=\"{Binding Notes}\"", note);
        Assert.Contains("DataContext.EditNoteCommand", note);
        Assert.Contains("DataContext.CopyNextStepCommand", note);
        Assert.Contains("DataContext.RemoveNoteCommand", note);
        Assert.Contains("Clipboard history or sync may retain it.", note);
        Assert.Contains("Protected for this Windows user and stored locally.", note);
        Assert.Contains("x:Key=\"LightMultilineTextBoxStyle\"", styles);
    }

    [Fact]
    public void Harness_PersistsSeveralNotesAcrossProviderRestarts()
    {
        var root = TestWorkspace.FindRoot();
        var platform = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Bootstrap", "Composition", "PlatformRegistration.cs"));
        var harness = File.ReadAllText(Path.Combine(root, "tools", "PulseMeter.VisualHarness", "VisualHarnessComposition.cs"));
        Assert.DoesNotContain("ResumeCard", platform);
        Assert.DoesNotContain("ResumeCard", harness);

        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PulseMeter.ReturnNote", Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(workspace, ".git"));
        File.WriteAllText(Path.Combine(workspace, "PulseMeter.slnx"), "<Solution />");
        var paths = VisualHarnessWorkspace.ValidateRoot(workspace);

        using (var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { }))
        {
            var notes = provider.GetRequiredService<ReturnNoteSectionViewModel>();
            AddNote(notes, "PulseMeter", "Run tests");
            AddNote(notes, "Release", "Publish package");
            Assert.Equal(2, notes.Notes.Count);
        }

        using (var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { }))
        {
            var notes = provider.GetRequiredService<ReturnNoteSectionViewModel>();
            Assert.Equal(2, notes.Notes.Count);
            Assert.Equal("PulseMeter", notes.Notes[0].For);
            Assert.Equal("Publish package", notes.Notes[1].NextStep);

            notes.RemoveNoteCommand.Execute(notes.Notes[0]);
            notes.ConfirmRemoveCommand.Execute(notes.Notes[0]);
        }

        using (var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { }))
        {
            var notes = provider.GetRequiredService<ReturnNoteSectionViewModel>();
            Assert.Single(notes.Notes);
            Assert.Equal("Release", notes.Notes[0].For);
        }

        Assert.True(File.Exists(paths.ReturnNotesPath));
        Directory.Delete(workspace, true);
    }

    private static void AddNote(ReturnNoteSectionViewModel viewModel, string forText, string nextStep)
    {
        viewModel.AddNoteCommand.Execute(null);
        viewModel.ForText = forText;
        viewModel.NextStep = nextStep;
        viewModel.SaveCommand.Execute(null);
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }

    private sealed class ThrowingClipboard : IClipboardService
    {
        public void SetText(string text) => throw new InvalidOperationException();
    }

    private sealed class FailingStateStore : IReturnNoteStateStore
    {
        public ReturnNoteLoadResult Load() => new(ReturnNoteLoadStatus.Missing);

        public bool Save(IReadOnlyList<ReturnNote> notes) => false;
    }
}
