using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using PulseMeter.Platform.Persistence;
using ReturnNoteModel = PulseMeter.Slices.ReturnNote.Models.ReturnNote;

namespace PulseMeter.Slices.ReturnNote.Business;

public enum ReturnNoteLoadStatus
{
    Loaded,
    Missing,
    Corrupt,
    Unavailable
}

public sealed record ReturnNoteLoadResult(
    ReturnNoteLoadStatus Status,
    IReadOnlyList<ReturnNoteModel>? Notes = null);

public interface IReturnNoteStateStore
{
    ReturnNoteLoadResult Load();

    bool Save(IReadOnlyList<ReturnNoteModel> notes);
}

public sealed class ReturnNoteStateStore : IReturnNoteStateStore
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaximumPersistedNotes = 100;

    private static readonly byte[] OptionalEntropy = "PulseMeter.ReturnNotes.v1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;
    private readonly IReturnNoteDataProtector _protector;

    public ReturnNoteStateStore(string? filePath = null)
        : this(filePath, new CurrentUserReturnNoteDataProtector())
    {
    }

    internal ReturnNoteStateStore(string? filePath, IReturnNoteDataProtector protector)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseMeter",
            "return-notes.v1.dat");
        _protector = protector;
    }

    public ReturnNoteLoadResult Load()
    {
        var envelopeResult = AtomicJsonFileStore.LoadWithStatus<ReturnNoteEnvelope>(_filePath, JsonOptions);
        if (envelopeResult.Status != AtomicJsonLoadStatus.Loaded || envelopeResult.Value is not { } envelope)
        {
            return envelopeResult.Status switch
            {
                AtomicJsonLoadStatus.Invalid => new ReturnNoteLoadResult(ReturnNoteLoadStatus.Corrupt),
                AtomicJsonLoadStatus.Unavailable => new ReturnNoteLoadResult(ReturnNoteLoadStatus.Unavailable),
                _ => new ReturnNoteLoadResult(ReturnNoteLoadStatus.Missing)
            };
        }

        if (envelope.SchemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(envelope.ProtectedPayload))
        {
            return new ReturnNoteLoadResult(ReturnNoteLoadStatus.Corrupt);
        }

        byte[]? protectedBytes = null;
        byte[]? plaintextBytes = null;
        try
        {
            protectedBytes = Convert.FromBase64String(envelope.ProtectedPayload);
            plaintextBytes = _protector.Unprotect(protectedBytes, OptionalEntropy);
            var state = JsonSerializer.Deserialize<ReturnNoteState>(plaintextBytes, JsonOptions);
            return IsValidState(state)
                ? new ReturnNoteLoadResult(ReturnNoteLoadStatus.Loaded, state!.Notes!.Cast<ReturnNoteModel>().ToArray())
                : new ReturnNoteLoadResult(ReturnNoteLoadStatus.Corrupt);
        }
        catch (Exception exception) when (exception is FormatException
            or JsonException
            or CryptographicException
            or PlatformNotSupportedException)
        {
            return new ReturnNoteLoadResult(ReturnNoteLoadStatus.Corrupt);
        }
        finally
        {
            if (plaintextBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    public bool Save(IReadOnlyList<ReturnNoteModel> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var state = new ReturnNoteState(CurrentSchemaVersion, notes.Cast<ReturnNoteModel?>().ToArray());
        if (!IsValidState(state))
        {
            return false;
        }

        byte[]? plaintextBytes = null;
        byte[]? protectedBytes = null;
        try
        {
            plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
            protectedBytes = _protector.Protect(plaintextBytes, OptionalEntropy);
            var envelope = new ReturnNoteEnvelope(
                CurrentSchemaVersion,
                Convert.ToBase64String(protectedBytes));
            return AtomicJsonFileStore.Save(_filePath, envelope, JsonOptions);
        }
        catch (Exception exception) when (exception is CryptographicException
            or PlatformNotSupportedException)
        {
            return false;
        }
        finally
        {
            if (plaintextBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    private static bool IsValidState(ReturnNoteState? state)
    {
        if (state is not { SchemaVersion: CurrentSchemaVersion, Notes: { Count: <= MaximumPersistedNotes } notes })
        {
            return false;
        }

        foreach (var note in notes)
        {
            if (note is null)
            {
                return false;
            }

            var validation = ReturnNoteValidator.Validate(note.For, note.NextStep);
            if (!validation.IsValid || validation.Note != note)
            {
                return false;
            }
        }

        return true;
    }

    internal sealed record ReturnNoteEnvelope(int SchemaVersion, string ProtectedPayload);
    internal sealed record ReturnNoteState(int SchemaVersion, IReadOnlyList<ReturnNoteModel?>? Notes);
}

internal interface IReturnNoteDataProtector
{
    byte[] Protect(byte[] plaintext, byte[] optionalEntropy);

    byte[] Unprotect(byte[] protectedData, byte[] optionalEntropy);
}

internal sealed class CurrentUserReturnNoteDataProtector : IReturnNoteDataProtector
{
    public byte[] Protect(byte[] plaintext, byte[] optionalEntropy) =>
        ProtectedData.Protect(plaintext, optionalEntropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData, byte[] optionalEntropy) =>
        ProtectedData.Unprotect(protectedData, optionalEntropy, DataProtectionScope.CurrentUser);
}
