namespace PulseMeter.Slices.UsageCollection.Models;

public sealed record ProjectUsageRow(
    string DisplayName,
    string FullPath,
    long EstimatedTokens,
    long RawLocalTokens,
    int ThreadCount,
    double SharePercent);
