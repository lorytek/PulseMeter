using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class UsageSummaryParserTests
{
    [Fact]
    public void MergeUsageSummary_PreservesRateLimitsAndAddsSummaryFields()
    {
        var snapshot = new UsageSnapshot
        {
            Buckets = [new RateLimitBucket { Label = "5h", UsedPercent = 20 }],
            SyncStatus = SyncStatus.Live,
            Source = "AppServer",
            LastUpdatedUtc = DateTimeOffset.FromUnixTimeSeconds(1_730_000_000)
        };
        using var document = JsonDocument.Parse("""
            {
              "summary": {
                "lifetimeTokens": 123456,
                "peakDailyTokens": 45000,
                "longestRunningTurnSec": 99,
                "currentStreakDays": 3,
                "longestStreakDays": 5
              },
              "dailyUsageBuckets": [
                { "startDate": "2026-07-01", "tokens": 1000 }
              ]
            }
            """);

        var merged = CodexUsageParser.MergeUsageSummary(snapshot, document.RootElement);

        Assert.Equal(snapshot.Buckets, merged.Buckets);
        Assert.Equal(123456, merged.LifetimeTokens);
        Assert.Equal(45000, merged.PeakDailyTokens);
        Assert.Equal(99, merged.LongestRunningTurnSec);
        Assert.Equal(3, merged.CurrentStreakDays);
        Assert.Equal(5, merged.LongestStreakDays);
        Assert.Equal("2026-07-01", Assert.Single(merged.DailyBuckets).StartDate);
    }
}
