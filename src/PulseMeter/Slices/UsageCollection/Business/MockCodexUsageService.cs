using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

public interface IMockUsageService : IUsageService
{
}

public sealed class MockCodexUsageService : IMockUsageService
{
    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public bool UseMockMode { get; set; } = true;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var fiveHourReset = now.AddHours(2).AddMinutes(1);
        var weeklyReset = now.AddDays(5).AddHours(22);
        var sparkFiveHourReset = now.AddHours(4).AddMinutes(12);
        var sparkWeeklyReset = now.AddDays(6).AddHours(18);
        var recentThreadUpdated = now.AddSeconds(-12);

        var buckets = new[]
        {
            new RateLimitBucket
            {
                LimitId = "mock-general",
                LimitName = "5h window",
                UsedPercent = 87,
                WindowDurationMins = 300,
                ResetsAtUnixSeconds = fiveHourReset.ToUnixTimeSeconds(),
                ResetsAtUtc = fiveHourReset,
                Label = "5h",
                GroupLabel = "General",
                WindowLabel = "5h",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(fiveHourReset.ToUnixTimeSeconds(), now)
            },
            new RateLimitBucket
            {
                LimitId = "mock-general",
                LimitName = "7d window",
                UsedPercent = 50,
                WindowDurationMins = 10080,
                ResetsAtUnixSeconds = weeklyReset.ToUnixTimeSeconds(),
                ResetsAtUtc = weeklyReset,
                Label = "7d",
                GroupLabel = "General",
                WindowLabel = "7d",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(weeklyReset.ToUnixTimeSeconds(), now)
            },
            new RateLimitBucket
            {
                LimitId = "mock-spark",
                LimitName = "GPT-5.3-Spark",
                UsedPercent = 12,
                WindowDurationMins = 300,
                ResetsAtUnixSeconds = sparkFiveHourReset.ToUnixTimeSeconds(),
                ResetsAtUtc = sparkFiveHourReset,
                Label = "GPT-5.3-Spark",
                GroupLabel = "GPT-5.3-Spark",
                WindowLabel = "5h",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(sparkFiveHourReset.ToUnixTimeSeconds(), now)
            },
            new RateLimitBucket
            {
                LimitId = "mock-spark",
                LimitName = "GPT-5.3-Spark",
                UsedPercent = 3,
                WindowDurationMins = 10080,
                ResetsAtUnixSeconds = sparkWeeklyReset.ToUnixTimeSeconds(),
                ResetsAtUtc = sparkWeeklyReset,
                Label = "GPT-5.3-Spark",
                GroupLabel = "GPT-5.3-Spark",
                WindowLabel = "7d",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(sparkWeeklyReset.ToUnixTimeSeconds(), now)
            }
        };

        var snapshot = new UsageSnapshot
        {
            Buckets = buckets,
            LifetimeTokens = 12_450_000,
            PeakDailyTokens = 840_000,
            LongestRunningTurnSec = 1_420,
            CurrentStreakDays = 5,
            LongestStreakDays = 12,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = now.AddDays(-2).ToString("yyyy-MM-dd"), Tokens = 520_000 },
                new DailyUsageBucket { StartDate = now.AddDays(-1).ToString("yyyy-MM-dd"), Tokens = 840_000 },
                new DailyUsageBucket { StartDate = now.ToString("yyyy-MM-dd"), Tokens = 310_000 }
            ],
            ProjectUsageRows =
            [
                new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 190_000, 360_000, 4, 61.3),
                new ProjectUsageRow("L2Engine", @"C:\Projects\L2Engine", 75_000, 142_000, 2, 24.2)
            ],
            ResetCreditsAvailable = 3,
            ResetCreditsExpiresAtUtc = null,
            RecentActiveThread = new ThreadUsageSnapshot
            {
                ThreadId = "mock-thread-payment-refactor",
                ThreadName = "Payment refactor",
                ContextUsedPercent = 59,
                ContextLeftPercent = 41,
                InputTokens = 118_000,
                OutputTokens = 42_000,
                TotalTokens = 160_000,
                LastUpdatedUtc = recentThreadUpdated,
                IsExactCurrentDesktopThread = false
            },
            SyncStatus = SyncStatus.Mocked,
            LastUpdatedUtc = now,
            Source = "Mock"
        };

        SnapshotUpdated?.Invoke(this, snapshot);
        return Task.FromResult(snapshot);
    }
}
