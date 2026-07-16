namespace PulseMeter.Slices.UsageCollection.Models;

public sealed record ProjectUsageRow(
    string DisplayName,
    string FullPath,
    long EstimatedTokens,
    long RawLocalTokens,
    int ThreadCount,
    double SharePercent,
    long EstimatedLast7Days = 0,
    long EstimatedPrevious7Days = 0,
    int ActiveDaysLast7 = 0,
    int SpikeDays = 0,
    string LeadingChatDisplayName = "",
    long LeadingChatEstimatedTokens = 0,
    string SecondLeadingChatDisplayName = "",
    long SecondLeadingChatEstimatedTokens = 0,
    string LargestBurnMomentChatDisplayName = "",
    long LargestBurnMomentEstimatedTokens = 0,
    DateTimeOffset? LargestBurnMomentAtUtc = null);
