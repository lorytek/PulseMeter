using System.Text.Json;
using System.IO;
using PulseMeter.Platform.Persistence;

namespace PulseMeter.Slices.UsageSignals.Business;

public interface IRunwayObservationStateStore
{
    RunwayObservationLoadResult Load();

    bool Save(RunwayObservationState state);
}

public enum RunwayObservationLoadStatus
{
    Loaded,
    Missing,
    Unavailable
}

public sealed record RunwayObservationLoadResult(RunwayObservationLoadStatus Status, RunwayObservationState? State = null);

public sealed record RunwayObservationState(int SchemaVersion, IReadOnlyList<RunwayObservationSample?>? Samples);

public sealed record RunwayObservationSample(
    string BucketId,
    string LimitKey,
    string TrackLabel,
    string WindowLabel,
    string ForecastWindowLabel,
    int? WindowDurationMins,
    double UsedPercent,
    DateTimeOffset ResetsAtUtc,
    DateTimeOffset ObservedAtUtc,
    bool StartsAfterMeasurementGap = false);

public sealed class RunwayObservationStateStore : IRunwayObservationStateStore
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _filePath;

    public RunwayObservationStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseMeter",
            "runway-observations.json");
    }

    public RunwayObservationLoadResult Load()
    {
        var state = AtomicJsonFileStore.Load<RunwayObservationState>(_filePath, JsonOptions);
        if (state is not null)
        {
            return new RunwayObservationLoadResult(RunwayObservationLoadStatus.Loaded, state);
        }

        return ProbeFile(_filePath) || ProbeFile(_filePath + ".bak")
            ? new RunwayObservationLoadResult(RunwayObservationLoadStatus.Unavailable)
            : new RunwayObservationLoadResult(RunwayObservationLoadStatus.Missing);
    }

    public bool Save(RunwayObservationState state)
    {
        return AtomicJsonFileStore.Save(_filePath, state, JsonOptions);
    }

    private static bool ProbeFile(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
