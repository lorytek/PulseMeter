using System.Text;
using ReturnNoteModel = PulseMeter.Slices.ReturnNote.Models.ReturnNote;
using ReturnNoteValidationResult = PulseMeter.Slices.ReturnNote.Models.ReturnNoteValidationResult;

namespace PulseMeter.Slices.ReturnNote.Business;

public static class ReturnNoteValidator
{
    public static ReturnNoteValidationResult Validate(string? forText, string? nextStep)
    {
        var normalizedFor = Normalize(forText, 1, 80, "For");
        if (normalizedFor.Error is not null) return ReturnNoteValidationResult.Invalid(normalizedFor.Error);
        var normalizedNext = Normalize(nextStep, 1, 240, "Next step");
        return normalizedNext.Error is null
            ? new ReturnNoteValidationResult(true, new ReturnNoteModel(normalizedFor.Value!, normalizedNext.Value!), string.Empty)
            : ReturnNoteValidationResult.Invalid(normalizedNext.Error);
    }

    private static (string? Value, string? Error) Normalize(string? value, int minimumScalars, int maximumScalars, string field)
    {
        var input = value ?? string.Empty;
        foreach (var rune in input.EnumerateRunes())
        {
            if (rune.Value == 0xFFFD || IsUnsafeControl(rune.Value)) return (null, $"{field} contains unsupported characters.");
        }
        try
        {
            if (!input.IsNormalized(NormalizationForm.FormC)) input = input.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return (null, $"{field} contains unsupported characters.");
        }
        var normalized = input.Trim();
        var scalarCount = normalized.EnumerateRunes().Count();
        return scalarCount < minimumScalars || scalarCount > maximumScalars
            ? (null, $"{field} must be {minimumScalars}-{maximumScalars} characters.")
            : (normalized, null);
    }

    private static bool IsUnsafeControl(int value) =>
        value is >= 0x0000 and <= 0x001F
        || value is >= 0x007F and <= 0x009F
        || value is 0x061C or 0x200E or 0x200F
        || value is >= 0x202A and <= 0x202E
        || value is >= 0x2066 and <= 0x2069;
}
