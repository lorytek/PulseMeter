namespace PulseMeter.Slices.ReturnNote.Models;

public sealed record ReturnNote(string For, string NextStep);

public sealed record ReturnNoteValidationResult(bool IsValid, ReturnNote? Note, string Error)
{
    public static ReturnNoteValidationResult Invalid(string error) => new(false, null, error);
}
