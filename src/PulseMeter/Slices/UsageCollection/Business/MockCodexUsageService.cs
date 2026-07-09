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
        var fiveHourReset = now.AddHours(1).AddMinutes(12);
        var weeklyReset = now.AddHours(19);
        var sparkFiveHourReset = now.AddHours(3).AddMinutes(20);
        var sparkWeeklyReset = now.AddDays(5).AddHours(7);
        var backgroundReset = now.AddMinutes(27);
        var resetCreditExpiry = now.AddHours(18);
        var recentThreadUpdated = now.AddSeconds(-12);
        var today = DateOnly.FromDateTime(now.LocalDateTime);

        var buckets = new[]
        {
            new RateLimitBucket
            {
                LimitId = "mock-general",
                LimitName = "5h window",
                UsedPercent = 96,
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
                UsedPercent = 92,
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
                UsedPercent = 78,
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
                UsedPercent = 34,
                WindowDurationMins = 10080,
                ResetsAtUnixSeconds = sparkWeeklyReset.ToUnixTimeSeconds(),
                ResetsAtUtc = sparkWeeklyReset,
                Label = "GPT-5.3-Spark",
                GroupLabel = "GPT-5.3-Spark",
                WindowLabel = "7d",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(sparkWeeklyReset.ToUnixTimeSeconds(), now)
            },
            new RateLimitBucket
            {
                LimitId = "mock-background-agent",
                LimitName = "Background agent",
                UsedPercent = 100,
                WindowDurationMins = 60,
                ResetsAtUnixSeconds = backgroundReset.ToUnixTimeSeconds(),
                ResetsAtUtc = backgroundReset,
                RateLimitReachedType = "hard_limit",
                Label = "1h",
                GroupLabel = "Background agent",
                WindowLabel = "1h",
                ResetCountdown = CountdownFormatter.FormatResetCountdown(backgroundReset.ToUnixTimeSeconds(), now)
            }
        };

        var snapshot = new UsageSnapshot
        {
            Buckets = buckets,
            LifetimeTokens = 48_620_000,
            PeakDailyTokens = 1_120_000,
            LongestRunningTurnSec = 2_940,
            CurrentStreakDays = 8,
            LongestStreakDays = 21,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = today.AddDays(-6).ToString("yyyy-MM-dd"), Tokens = 260_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-5).ToString("yyyy-MM-dd"), Tokens = 315_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-4).ToString("yyyy-MM-dd"), Tokens = 280_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-3).ToString("yyyy-MM-dd"), Tokens = 340_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-2).ToString("yyyy-MM-dd"), Tokens = 295_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-1).ToString("yyyy-MM-dd"), Tokens = 310_000 },
                new DailyUsageBucket { StartDate = today.ToString("yyyy-MM-dd"), Tokens = 940_000 }
            ],
            ProjectUsageRows =
            [
                new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 1_820_000, 3_640_000, 9, 62.4),
                new ProjectUsageRow("Searchability Audit", @"C:\Projects\Searchability", 510_000, 1_020_000, 4, 17.5),
                new ProjectUsageRow("L2Engine", @"C:\Projects\L2Engine", 390_000, 780_000, 3, 13.4),
                new ProjectUsageRow("Docs Polish", @"C:\Projects\Docs", 195_000, 390_000, 2, 6.7)
            ],
            UsageAttribution = new UsageAttributionSnapshot
            {
                AccountWindowTokens = 2_740_000,
                RawLocalTokens = 1_880_000,
                EstimatedAttributedTokens = 2_420_000,
                LastUpdatedUtc = now,
                Sessions =
                [
                    new UsageAttributionSessionRow(
                        "Burn analysis implementation",
                        "mock-thread-burn-analysis",
                        "PulseMeter",
                        @"C:\Projects\PulseMeter",
                        840_000,
                        1_080_000,
                        39.4,
                        510_000,
                        180_000,
                        96_000,
                        54_000,
                        now.AddMinutes(-44),
                        now.AddMinutes(-6)),
                    new UsageAttributionSessionRow(
                        "Searchability benchmark sweep",
                        "mock-thread-searchability",
                        "Searchability Audit",
                        @"C:\Projects\Searchability",
                        410_000,
                        528_000,
                        19.3,
                        260_000,
                        95_000,
                        34_000,
                        21_000,
                        now.AddHours(-5),
                        now.AddHours(-4).AddMinutes(-12)),
                    new UsageAttributionSessionRow(
                        "L2Engine routing review",
                        "mock-thread-l2-routing",
                        "L2Engine",
                        @"C:\Projects\L2Engine",
                        325_000,
                        418_000,
                        15.3,
                        198_000,
                        80_000,
                        31_000,
                        16_000,
                        now.AddDays(-1),
                        now.AddHours(-23)),
                    new UsageAttributionSessionRow(
                        "Docs polish pass",
                        "mock-thread-docs-polish",
                        "Docs Polish",
                        @"C:\Projects\Docs",
                        180_000,
                        232_000,
                        8.5,
                        112_000,
                        44_000,
                        15_000,
                        9_000,
                        now.AddDays(-2),
                        now.AddDays(-2).AddMinutes(35))
                ],
                BurnEvents =
                [
                    new UsageAttributionBurnEvent(
                        "Burn analysis implementation",
                        "mock-thread-burn-analysis",
                        "PulseMeter",
                        @"C:\Projects\PulseMeter",
                        now.AddMinutes(-6),
                        540_000,
                        695_000,
                        310_000,
                        122_000,
                        71_000,
                        37_000),
                    new UsageAttributionBurnEvent(
                        "Searchability benchmark sweep",
                        "mock-thread-searchability",
                        "Searchability Audit",
                        @"C:\Projects\Searchability",
                        now.AddHours(-4).AddMinutes(-12),
                        278_000,
                        358_000,
                        172_000,
                        67_000,
                        24_000,
                        15_000),
                    new UsageAttributionBurnEvent(
                        "L2Engine routing review",
                        "mock-thread-l2-routing",
                        "L2Engine",
                        @"C:\Projects\L2Engine",
                        now.AddHours(-23),
                        210_000,
                        270_000,
                        126_000,
                        54_000,
                        19_000,
                        11_000)
                ]
            },
            ResetCreditsAvailable = 3,
            ResetCreditsExpiresAtUtc = resetCreditExpiry,
            ResetCredits =
            [
                new ResetCreditSnapshot(now.AddDays(-25), resetCreditExpiry, "available"),
                new ResetCreditSnapshot(now.AddDays(-18), now.AddDays(7), "available"),
                new ResetCreditSnapshot(now.AddDays(-3), now.AddDays(27), "available")
            ],
            RecentActiveThread = new ThreadUsageSnapshot
            {
                ThreadId = "mock-thread-budget-alerts",
                ThreadName = "Attention signals demo",
                ContextUsedPercent = 86,
                ContextLeftPercent = 14,
                InputTokens = 236_000,
                OutputTokens = 78_000,
                TotalTokens = 314_000,
                LastUpdatedUtc = recentThreadUpdated,
                IsExactCurrentDesktopThread = false
            },
            SyncStatus = SyncStatus.Mocked,
            LastUpdatedUtc = now,
            Source = "Mock",
            StatusMessage = "Mock showcase data: includes demo limits, reset credits, project usage, automatic alert signals, runway, and idle-drain signals."
        };

        SnapshotUpdated?.Invoke(this, snapshot);
        return Task.FromResult(snapshot);
    }
}
