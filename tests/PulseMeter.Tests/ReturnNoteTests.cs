using Microsoft.Extensions.DependencyInjection;
using PulseMeter.Platform.Windows;
using PulseMeter.Slices.ReturnNote.Business;
using PulseMeter.Slices.ReturnNote.UI;
using PulseMeter.VisualHarness;

namespace PulseMeter.Tests;

public sealed class ReturnNoteTests
{
    [Fact]
    public void Validator_NormalizesScalarsAndRejectsMalformedControlsAndBidi()
    {
        var valid = ReturnNoteValidator.Validate(" e\u0301🙂 ", "next");
        Assert.True(valid.IsValid); Assert.Equal("é🙂", valid.Note!.For);
        Assert.False(ReturnNoteValidator.Validate("for\t", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u202E", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u061C", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u200E", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("for\u200F", "next").IsValid);
        Assert.False(ReturnNoteValidator.Validate("\uD800", "next").IsValid);
        Assert.True(ReturnNoteValidator.Validate(string.Concat(Enumerable.Repeat("🙂", 80)), string.Concat(Enumerable.Repeat("🙂", 240))).IsValid);
        Assert.False(ReturnNoteValidator.Validate(string.Concat(Enumerable.Repeat("🙂", 81)), "next").IsValid);
    }

    [Fact]
    public void ViewModel_TransitionsBetweenCompactEmptyEditViewAndClearConfirmation()
    {
        var clipboard = new RecordingClipboard(); var vm = new ReturnNoteSectionViewModel(clipboard);
        Assert.True(vm.IsEmpty); Assert.False(vm.IsEditing);
        vm.AddNoteCommand.Execute(null); Assert.True(vm.IsEditing); Assert.False(vm.IsEmpty);
        vm.ForText = "release"; vm.NextStep = "run tests"; vm.SaveCommand.Execute(null);
        Assert.True(vm.IsViewing); Assert.Equal("release", vm.SavedFor); Assert.Equal("run tests", vm.SavedNextStep);
        vm.EditCommand.Execute(null); Assert.True(vm.IsEditing); Assert.False(vm.IsViewing); vm.CancelCommand.Execute(null); Assert.True(vm.IsViewing);
        vm.ClearCommand.Execute(null); Assert.True(vm.IsConfirmingClear); Assert.False(vm.IsViewing); Assert.False(vm.IsEditing);
        vm.CancelClearCommand.Execute(null); Assert.True(vm.IsViewing);
        vm.ClearCommand.Execute(null); vm.ConfirmClearCommand.Execute(null); Assert.True(vm.IsEmpty); Assert.False(vm.HasSavedNote);
    }

    [Fact]
    public void ViewModel_CopiesOnlySavedNextStepAndPreservesNoteOnClipboardFailure()
    {
        var clipboard = new RecordingClipboard(); var vm = new ReturnNoteSectionViewModel(clipboard); vm.AddNoteCommand.Execute(null); vm.ForText = "For value"; vm.NextStep = "Only next"; vm.SaveCommand.Execute(null);
        vm.CopyNextStepCommand.Execute(null); Assert.Equal("Only next", clipboard.Text);
        var failing = new ReturnNoteSectionViewModel(new ThrowingClipboard()); failing.AddNoteCommand.Execute(null); failing.ForText = "For value"; failing.NextStep = "Only next"; failing.SaveCommand.Execute(null); failing.CopyNextStepCommand.Execute(null);
        Assert.Equal("Only next", failing.SavedNextStep); Assert.Equal("Couldn't copy. Try again.", failing.StatusText);
    }

    [Fact]
    public void XamlAndHarness_ShowSavedFieldsAfterNeedsAttentionAndResetWithNewProvider()
    {
        var root = TestWorkspace.FindRoot(); var window = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml")); var note = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Slices", "ReturnNote", "UI", "ReturnNoteSection.xaml")); var platform = File.ReadAllText(Path.Combine(root, "src", "PulseMeter", "Bootstrap", "Composition", "PlatformRegistration.cs")); var harness = File.ReadAllText(Path.Combine(root, "tools", "PulseMeter.VisualHarness", "VisualHarnessComposition.cs"));
        Assert.True(window.IndexOf("<needsAttention:NeedsAttentionSection", StringComparison.Ordinal) < window.IndexOf("<returnNote:ReturnNoteSection", StringComparison.Ordinal));
        Assert.Contains("Text=\"{Binding SavedFor}\"", note); Assert.Contains("Text=\"{Binding SavedNextStep}\"", note); Assert.Contains("Content=\"_Project or task\"", note); Assert.Contains("Target=\"{Binding ElementName=ForTextBox}\"", note); Assert.Contains("AutomationProperties.Name=\"Add note\"", note); Assert.Contains("Do not enter secrets or customer data.", note); Assert.Contains("Copy uses the Windows clipboard, which may retain or sync text.", note); Assert.Contains("clipboard history or sync may retain it", note); Assert.DoesNotContain("ResumeCard", platform); Assert.DoesNotContain("ResumeCard", harness);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PulseMeter.ReturnNote", Guid.NewGuid().ToString("N"))).FullName; Directory.CreateDirectory(Path.Combine(workspace, ".git")); File.WriteAllText(Path.Combine(workspace, "PulseMeter.slnx"), "<Solution />");
        var paths = VisualHarnessWorkspace.ValidateRoot(workspace);
        using (var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { }))
        {
            var saved = provider.GetRequiredService<ReturnNoteSectionViewModel>();
            saved.AddNoteCommand.Execute(null); saved.ForText = "PulseMeter"; saved.NextStep = "Run tests"; saved.SaveCommand.Execute(null);
            Assert.True(saved.IsViewing);
        }
        using (var provider = VisualHarnessComposition.BuildServiceProvider(paths, () => { }))
        {
            Assert.True(provider.GetRequiredService<ReturnNoteSectionViewModel>().IsEmpty);
        }
        Assert.False(File.Exists(Path.Combine(paths.StateRoot, "resume-card.v1.dat")));
        Directory.Delete(workspace, true);
    }
    private sealed class RecordingClipboard : IClipboardService { public string? Text; public void SetText(string text) => Text = text; }
    private sealed class ThrowingClipboard : IClipboardService { public void SetText(string text) => throw new InvalidOperationException(); }
}
