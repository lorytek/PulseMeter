using System.Globalization;
using PulseMeter.Slices.UsageCollection;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowViewModelSyncTests
{
    [Fact]
    public void MergeSignals_RemovesDuplicateRateLimitAlertForTheSameWindow()
    {
        var usageSignals = new UsageSignalsSnapshot
        {
            AttentionSignals =
            [
                new UsageAttentionSignal(
                    3,
                    "LIMIT",
                    "Weekly window is low",
                    "25% left",
                    "#F97316",
                    Kind: UsageAttentionSignalKind.RateLimit,
                    ScopeId: "codex|10080")
            ]
        };
        UsageAttentionSignal[] budgetSignals =
        [
            new UsageAttentionSignal(
                3,
                "BUDGET",
                "7d budget warning",
                "7d is 75% used",
                "#F97316",
                Kind: UsageAttentionSignalKind.RateLimit,
                ScopeId: "codex|10080"),
            new UsageAttentionSignal(
                3,
                "BUDGET",
                "5h budget warning",
                "5h is 75% used",
                "#F97316",
                Kind: UsageAttentionSignalKind.RateLimit,
                ScopeId: "codex|300")
        ];

        var merged = PulseMeterWindowViewModel.MergeSignals(usageSignals, budgetSignals);

        Assert.Equal(2, merged.AttentionSignals.Count);
        Assert.Contains(merged.AttentionSignals, signal => signal.BadgeText == "LIMIT" && signal.ScopeId == "codex|10080");
        Assert.DoesNotContain(merged.AttentionSignals, signal => signal.BadgeText == "BUDGET" && signal.ScopeId == "codex|10080");
        Assert.Contains(merged.AttentionSignals, signal => signal.BadgeText == "BUDGET" && signal.ScopeId == "codex|300");
    }

    [Fact]
    public void AutoSyncText_DescribesConfiguredInterval()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), TimeSpan.FromSeconds(90));

        Assert.Equal(90, viewModel.AutoSyncSeconds);
        Assert.Equal(TimeSpan.FromSeconds(90), viewModel.AutoSyncInterval);
    }

    [Fact]
    public void AutoSyncSeconds_CanBeEdited()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), TimeSpan.FromSeconds(90));

        viewModel.AutoSyncSeconds = 45;

        Assert.Equal(45, viewModel.AutoSyncSeconds);
        Assert.Equal(TimeSpan.FromSeconds(45), viewModel.AutoSyncInterval);
    }

    [Fact]
    public void RefreshClock_MarksOverdueLiveSnapshotAsStale()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService(), TimeSpan.FromSeconds(90));
        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        viewModel.RefreshClock();

        Assert.Equal("STALE", viewModel.StatusBadgeText);
        Assert.Equal("Stale", viewModel.SyncStatusText);
        Assert.Equal("#D97706", viewModel.StatusBadgeBrush);
        Assert.Equal(viewModel.StatusBadgeBrush, viewModel.DataBar.StatusBadgeBrush);
        Assert.Equal(viewModel.CompactStatusSummaryText, viewModel.DataBar.StatusSummaryText);
        Assert.Equal(viewModel.StatusBadgeBrush, viewModel.ExpandedHeader.StatusBadgeBrush);
        Assert.Equal(viewModel.LastUpdatedText, viewModel.ExpandedHeader.LastUpdatedText);
        Assert.Equal(viewModel.LastUpdatedDetailText, viewModel.ExpandedHeader.LastUpdatedDetailText);
        Assert.Equal($"{viewModel.StatusBadgeText}. {viewModel.LastUpdatedDetailText}", viewModel.ExpandedHeader.StatusSummaryText);
    }

    [Theory]
    [InlineData(SyncStatus.Live, "#16A34A")]
    [InlineData(SyncStatus.Mocked, "#16A34A")]
    [InlineData(SyncStatus.Stale, "#D97706")]
    [InlineData(SyncStatus.Unavailable, "#DC2626")]
    public void StatusBadgeBrush_ReflectsTheCurrentDataState(SyncStatus status, string expectedBrush)
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Source = status == SyncStatus.Mocked ? "Mock" : "AppServer",
            SyncStatus = status,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(expectedBrush, viewModel.StatusBadgeBrush);
        Assert.Equal(expectedBrush, viewModel.DataBar.StatusBadgeBrush);
        Assert.Equal(expectedBrush, viewModel.ExpandedHeader.StatusBadgeBrush);
    }

    [Fact]
    public async Task SyncNowCommand_RefreshesImmediately()
    {
        var service = new StubUsageService();
        var viewModel = new PulseMeterWindowViewModel(service, TimeSpan.FromSeconds(90));

        await viewModel.SyncNowCommand.ExecuteAsync();

        Assert.Equal(1, service.GetSnapshotCallCount);
        Assert.Equal("Source: Live source", viewModel.SourceText);
    }

    [Fact]
    public async Task RefreshAsync_AppliesServicePublishedSnapshotOnlyOnce()
    {
        var service = new StubUsageService();
        var usageSignalsTracker = new CountingUsageSignalsTracker();
        var viewModel = new PulseMeterWindowViewModel(service, usageSignalsTracker: usageSignalsTracker);
        service.SnapshotUpdated += (_, snapshot) => viewModel.ApplySnapshot(snapshot);

        await viewModel.RefreshAsync();

        Assert.Equal(1, usageSignalsTracker.ObserveCallCount);
    }

    [Fact]
    public async Task SyncNowCommand_ShowsInProgressAndSuccessFeedback()
    {
        var pendingSnapshot = new TaskCompletionSource<UsageSnapshot>();
        var service = new StubUsageService
        {
            SnapshotTask = pendingSnapshot.Task
        };
        var viewModel = new PulseMeterWindowViewModel(service, TimeSpan.FromSeconds(90));

        var syncTask = viewModel.SyncNowCommand.ExecuteAsync();

        Assert.True(viewModel.IsRefreshing);
        Assert.False(viewModel.SyncNowCommand.CanExecute(null));
        Assert.Equal("Syncing...", viewModel.SyncButtonText);
        Assert.Equal("Syncing now...", viewModel.SyncFeedbackText);
        Assert.Equal("#1F73FF", viewModel.StatusBadgeBrush);
        Assert.Equal("#1F73FF", viewModel.DataBar.StatusBadgeBrush);
        Assert.Equal("#1F73FF", viewModel.ExpandedHeader.StatusBadgeBrush);
        Assert.True(viewModel.ExpandedHeader.IsRefreshing);
        Assert.Equal("Syncing...", viewModel.ExpandedHeader.SyncButtonText);
        Assert.Equal("Syncing usage", viewModel.ExpandedHeader.SyncButtonTooltip);
        Assert.Equal("Syncing usage", viewModel.ExpandedHeader.SyncButtonAccessibleLabel);

        var updatedLocal = DateTimeOffset.Now;
        pendingSnapshot.SetResult(new UsageSnapshot
        {
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = updatedLocal.ToUniversalTime()
        });
        await syncTask;

        Assert.False(viewModel.IsRefreshing);
        Assert.Equal("Sync now", viewModel.SyncButtonText);
        Assert.Equal($"Synced at {updatedLocal:HH:mm}", viewModel.SyncFeedbackText);
        Assert.False(viewModel.HasActionableSyncIssue);
        Assert.False(viewModel.ExpandedHeader.IsRefreshing);
        Assert.Equal("Sync now", viewModel.ExpandedHeader.SyncButtonText);
        Assert.Equal("Sync usage (F5)", viewModel.ExpandedHeader.SyncButtonTooltip);
        Assert.Equal("Sync usage now", viewModel.ExpandedHeader.SyncButtonAccessibleLabel);
    }

    [Fact]
    public async Task SyncNowCommand_HidesSensitiveFailureDetailsWhenRefreshFails()
    {
        const string sensitiveDetails = @"C:\Users\Alice\.codex\sessions\private.json token=secret-token";
        var service = new StubUsageService
        {
            ExceptionToThrow = new InvalidOperationException(sensitiveDetails)
        };
        var viewModel = new PulseMeterWindowViewModel(service, TimeSpan.FromSeconds(90));

        await viewModel.SyncNowCommand.ExecuteAsync();

        Assert.False(viewModel.IsRefreshing);
        Assert.Equal("Sync now", viewModel.SyncButtonText);
        Assert.Equal("Sync failed. Try again.", viewModel.SyncFeedbackText);
        Assert.True(viewModel.HasActionableSyncIssue);
        Assert.Equal("Sync failed", viewModel.SyncIssueTitle);
        Assert.Equal("PulseMeter could not refresh usage. Check the monitored app, then try again.", viewModel.SyncIssueText);
        Assert.Equal("#FEF2F2", viewModel.SyncIssueBackground);
        Assert.Equal("#FCA5A5", viewModel.SyncIssueBorderBrush);
        Assert.DoesNotContain("C:\\Users", viewModel.SyncFeedbackText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alice", viewModel.SyncFeedbackText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", viewModel.SyncFeedbackText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncNowCommand_ShowsUnavailableFeedbackWhenMonitoredAppIsNotRunning()
    {
        var service = new StubUsageService
        {
            SnapshotTask = Task.FromResult(new UsageSnapshot
            {
                Source = "AppServer",
                SyncStatus = SyncStatus.Unavailable,
                StatusMessage = "The monitored app is not running. Start it, then sync again."
            })
        };
        var viewModel = new PulseMeterWindowViewModel(service, TimeSpan.FromSeconds(90));

        await viewModel.SyncNowCommand.ExecuteAsync();

        Assert.Equal("UNAVAILABLE", viewModel.StatusBadgeText);
        Assert.Equal("The monitored app is not running. Start it, then sync again.", viewModel.StatusMessage);
        Assert.Equal("#DC2626", viewModel.StatusMessageBrush);
        Assert.True(viewModel.HasActionableSyncIssue);
        Assert.Equal("Live source unavailable", viewModel.SyncIssueTitle);
        Assert.Equal("The monitored app is not running. Start it, then sync again.", viewModel.SyncIssueText);
        Assert.Contains("Live source unavailable", viewModel.DataBar.StatusSummaryText);
        Assert.Contains("Start it, then sync again", viewModel.DataBar.StatusSummaryText);
        Assert.False(viewModel.HasSectionStatusMessage);
        Assert.Equal(
            "Source unavailable: The monitored app is not running. Start it, then sync again.",
            viewModel.SyncFeedbackText);
    }

    [Fact]
    public void StaleSnapshot_ShowsGlobalRetryIssueWithoutDuplicatingSectionStatus()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var updated = DateTimeOffset.UtcNow.AddMinutes(-10);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Source = "AppServer",
            SyncStatus = SyncStatus.Stale,
            LastUpdatedUtc = updated,
            StatusMessage = "Cached usage is older than expected."
        });

        Assert.True(viewModel.HasActionableSyncIssue);
        Assert.Equal("Usage data is stale", viewModel.SyncIssueTitle);
        Assert.Contains("Retry to refresh usage.", viewModel.SyncIssueText);
        Assert.Equal("#FFF7ED", viewModel.SyncIssueBackground);
        Assert.Equal("#FDBA74", viewModel.SyncIssueBorderBrush);
        Assert.Equal("#C2410C", viewModel.SyncIssueForeground);
        Assert.False(viewModel.HasSectionStatusMessage);
    }

    [Fact]
    public void MockStatusMessage_RemainsInRateLimitsWithoutGlobalIssueBanner()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Source = "Mock",
            SyncStatus = SyncStatus.Mocked,
            StatusMessage = "Representative mock data."
        });

        Assert.False(viewModel.HasActionableSyncIssue);
        Assert.True(viewModel.HasSectionStatusMessage);
        Assert.Equal(string.Empty, viewModel.SyncIssueTitle);
    }

    [Fact]
    public void InitialStartingSnapshot_DoesNotFlashUnavailableBanner()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.False(viewModel.HasActionableSyncIssue);
        Assert.True(viewModel.HasSectionStatusMessage);
    }

    [Theory]
    [InlineData(SyncStatus.Mocked, "#64748B")]
    [InlineData(SyncStatus.Live, "#64748B")]
    [InlineData(SyncStatus.Stale, "#D97706")]
    [InlineData(SyncStatus.Unavailable, "#DC2626")]
    public void StatusMessageBrush_ReflectsSyncSeverity(SyncStatus status, string expectedBrush)
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = status,
            StatusMessage = "Status detail"
        });

        Assert.Equal(expectedBrush, viewModel.StatusMessageBrush);
    }

    [Fact]
    public void TodayUsageText_ShowsTodayUsedTokenCopy()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = yesterday, Tokens = 9_999 },
                new DailyUsageBucket { StartDate = today, InputTokens = 1_200, OutputTokens = 450 }
            ]
        });

        Assert.Equal("1.7K tokens", viewModel.TodayUsageValueText);
        Assert.Equal("Today used tokens: 1.7K tokens", viewModel.TodayUsageText);
    }

    [Fact]
    public void TodayUsageText_ShowsUnavailableAndWarningWhenLiveTodayBucketIsMissing()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [UsageBucket(8)],
            LifetimeTokens = 9_100_000_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 20,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = yesterday, Tokens = 9_999 }
            ]
        });

        Assert.Equal("Unavailable", viewModel.TodayUsageValueText);
        Assert.Equal("Today used tokens: unavailable", viewModel.TodayUsageText);
        Assert.Equal("--", viewModel.TodayUsageMetricValueText);
        Assert.True(viewModel.HasAccountSummaryFreshnessWarning);
        Assert.Equal("Today's usage is not available yet.", viewModel.AccountSummaryFreshnessWarningText);
    }

    [Fact]
    public void TodayUsageText_ShowsRealZeroWhenLiveTodayBucketIsPresent()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [UsageBucket(8)],
            LifetimeTokens = 9_100_000_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 20,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = today, Tokens = 0 }
            ]
        });

        Assert.Equal("0 tokens", viewModel.TodayUsageValueText);
        Assert.Equal("Today used tokens: 0 tokens", viewModel.TodayUsageText);
        Assert.Equal("0", viewModel.TodayUsageMetricValueText);
        Assert.False(viewModel.HasAccountSummaryFreshnessWarning);
    }

    [Fact]
    public void TodayUsageText_ShowsUnavailableWhenDailyUsageIsMissing()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.Equal("Unavailable", viewModel.TodayUsageValueText);
        Assert.Equal("Today used tokens: unavailable", viewModel.TodayUsageText);
    }

    [Fact]
    public void AccountSummaryWarning_StaysHiddenWhenSummaryValuesArePresentButUnchanged()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [UsageBucket(6)],
            LifetimeTokens = 9_100_000_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 20,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = today, Tokens = 1 }
            ]
        });

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [UsageBucket(7)],
            LifetimeTokens = 9_100_000_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 20,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = today, Tokens = 1 }
            ]
        });

        Assert.False(viewModel.HasAccountSummaryFreshnessWarning);
    }

    [Fact]
    public void AccountSummaryWarning_ShowsUnavailableWhenLiveSummaryIsMissing()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Buckets = [UsageBucket(6)]
        });

        Assert.True(viewModel.HasAccountSummaryFreshnessWarning);
        Assert.Equal("Account summary unavailable.", viewModel.AccountSummaryFreshnessWarningText);
        Assert.DoesNotContain("delayed", viewModel.AccountSummaryFreshnessWarningText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccountDashboardMetrics_FormatSummaryValuesAndDailyRows()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
        var peakDay = DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            LifetimeTokens = 894_600_000,
            PeakDailyTokens = 940_800_000,
            CurrentStreakDays = 19,
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = peakDay, Tokens = 940_800_000 },
                new DailyUsageBucket { StartDate = yesterday, Tokens = 396_064_699 },
                new DailyUsageBucket { StartDate = today, Tokens = 83_614_740 }
            ]
        });

        Assert.Equal("83.6M", viewModel.TodayUsageMetricValueText);
        Assert.Equal("894.6M", viewModel.LifetimeUsageValueText);
        Assert.Equal("940.8M", viewModel.PeakUsageValueText);
        Assert.Equal("19", viewModel.StreakDaysValueText);
        Assert.Equal("12.5% of 30-day median", viewModel.TodayMedianDailyPercentText);
        Assert.Equal($"{DateTime.Today.AddDays(-2):dd MMM yyyy}", viewModel.PeakUsageCaptionText);

        Assert.Equal(7, viewModel.DailyUsageRows.Count);
        Assert.Equal("7 days", viewModel.DailyUsageWindowText);
        Assert.Equal("30-day median per day: 668.4M", viewModel.DailyUsageMedianSummaryText);
        Assert.True(viewModel.HasDailyUsageMedianSummary);
        Assert.Equal("Today", viewModel.DailyUsageRows[0].DateText);
        Assert.Equal("83.6M", viewModel.DailyUsageRows[0].TokenText);
        Assert.Equal("-87% vs median", viewModel.DailyUsageRows[0].MedianComparisonText);
        Assert.True(viewModel.DailyUsageRows[0].HasMedianComparison);
        Assert.InRange(viewModel.DailyUsageRows[0].BarPercentValue, 8.8, 9.0);
        Assert.Equal(DateTime.Today.AddDays(-1).ToString("dddd", CultureInfo.InvariantCulture), viewModel.DailyUsageRows[1].DateText);
        Assert.Equal("-41% vs median", viewModel.DailyUsageRows[1].MedianComparisonText);
        Assert.Equal(DateTime.Today.AddDays(-2).ToString("dddd", CultureInfo.InvariantCulture), viewModel.DailyUsageRows[2].DateText);
        Assert.Equal("940.8M", viewModel.DailyUsageRows[2].TokenText);
        Assert.Equal("+41% vs median", viewModel.DailyUsageRows[2].MedianComparisonText);
        Assert.Equal(100, viewModel.DailyUsageRows[2].BarPercentValue);
    }

    [Fact]
    public void DailyUsageMedian_UsesAvailableActiveDaysFromLastThirtyDays()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var today = DateTime.Today;

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            DailyBuckets =
            [
                new DailyUsageBucket { StartDate = today.AddDays(-31).ToString("yyyy-MM-dd"), Tokens = 900_000_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-3).ToString("yyyy-MM-dd"), Tokens = 10_000_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-2).ToString("yyyy-MM-dd"), Tokens = 20_000_000 },
                new DailyUsageBucket { StartDate = today.AddDays(-1).ToString("yyyy-MM-dd"), Tokens = 30_000_000 },
                new DailyUsageBucket { StartDate = today.ToString("yyyy-MM-dd"), Tokens = 40_000_000 }
            ]
        });

        Assert.Equal("30-day median per day: 20.0M", viewModel.DailyUsageMedianSummaryText);
        Assert.Equal("+100% vs median", viewModel.DailyUsageRows[0].MedianComparisonText);
    }

    [Fact]
    public void AccountDashboardMetrics_ShowUnavailableFallbacksWhenSummaryIsMissing()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.Equal("--", viewModel.TodayUsageMetricValueText);
        Assert.Equal("--", viewModel.LifetimeUsageValueText);
        Assert.Equal("--", viewModel.PeakUsageValueText);
        Assert.Equal("--", viewModel.StreakDaysValueText);
        Assert.Equal("Peak unavailable", viewModel.PeakUsageCaptionText);
        Assert.Equal("Waiting for daily usage", viewModel.TodayMedianDailyPercentText);
        Assert.Empty(viewModel.DailyUsageRows);
        Assert.False(viewModel.HasDailyUsageMedianSummary);
    }

    [Fact]
    public void ProjectUsageRows_ShowEstimatedProjectUsage()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ProjectUsageRows =
            [
                new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 3_000_000_000, 6_000_000_000, 12, 60)
            ]
        });

        Assert.True(viewModel.HasProjectUsage);
        Assert.Equal("Estimated from local sessions, scaled to account usage", viewModel.ProjectUsageEstimateText);
        var row = Assert.Single(viewModel.ProjectUsageRows);
        Assert.Equal("PulseMeter", row.DisplayName);
        Assert.Equal(@"C:\Projects\PulseMeter", row.FullPath);
        Assert.Equal("3.0B", row.EstimatedTokensText);
        Assert.Equal("60%", row.ShareText);
        Assert.Equal("12", row.ThreadCountText);
    }

    [Fact]
    public void ProjectUsageVisibility_FollowsSidebarToggleAndAvailableRows()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.False(viewModel.HasProjectUsage);
        Assert.False(viewModel.ShouldShowProjectUsage);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            ProjectUsageRows =
            [
                new ProjectUsageRow("PulseMeter", @"C:\Projects\PulseMeter", 3_000_000_000, 6_000_000_000, 12, 60)
            ]
        });

        Assert.True(viewModel.HasProjectUsage);
        Assert.True(viewModel.IsProjectUsageVisible);
        Assert.True(viewModel.ShouldShowProjectUsage);

        viewModel.IsProjectUsageVisible = false;

        Assert.False(viewModel.ShouldShowProjectUsage);
    }

    [Fact]
    public void UsageAttributionVisibility_FollowsSidebarToggleAndAvailableRows()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var now = DateTimeOffset.UtcNow;

        Assert.False(viewModel.HasUsageAttribution);
        Assert.True(viewModel.IsUsageAttributionVisible);
        Assert.True(viewModel.ShouldShowUsageAttribution);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            UsageAttribution = new UsageAttributionSnapshot
            {
                AccountWindowTokens = 2_950_000,
                RawLocalTokens = 900_000,
                EstimatedAttributedTokens = 1_250_000,
                LastUpdatedUtc = now,
                Sessions =
                [
                    new UsageAttributionSessionRow(
                        "PulseMeter budget polish",
                        "thread-123",
                        "PulseMeter",
                        @"C:\Projects\PulseMeter",
                        900_000,
                        1_250_000,
                        42.3,
                        500_000,
                        200_000,
                        100_000,
                        50_000,
                        now.AddMinutes(-20),
                        now.AddMinutes(-12))
                ]
            }
        });

        Assert.True(viewModel.HasUsageAttribution);
        Assert.True(viewModel.IsUsageAttributionVisible);
        Assert.True(viewModel.ShouldShowUsageAttribution);
        Assert.Equal("1.3M attributed across 1 local chat", viewModel.UsageAttribution.SummaryText);
        Assert.Equal("Estimated from local project activity, scaled to account usage", viewModel.UsageAttribution.EvidenceText);
        var project = Assert.Single(viewModel.UsageAttribution.ProjectRows);
        Assert.Equal("PulseMeter", project.DisplayName);
        Assert.Equal("1.3M", project.EstimatedTokensText);
        Assert.Equal("42.3%", project.ShareText);
        viewModel.IsUsageAttributionVisible = false;

        Assert.False(viewModel.ShouldShowUsageAttribution);
    }

    [Fact]
    public void ResetCreditsVisibility_FollowsSidebarToggle()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.True(viewModel.IsResetCreditsVisible);

        viewModel.IsResetCreditsVisible = false;

        Assert.False(viewModel.IsResetCreditsVisible);
    }

    [Fact]
    public void ExpandedPulseMeter_UsesComfortableDashboardSize()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.Equal(410, viewModel.WindowWidth);
        Assert.Equal(410, viewModel.WindowMinWidth);
        Assert.Equal(66, viewModel.WindowHeight);
        Assert.Equal(66, viewModel.WindowMinHeight);

        viewModel.ToggleExpanded();

        Assert.Equal(1024, viewModel.WindowWidth);
        Assert.Equal(720, viewModel.WindowMinWidth);
        Assert.Equal(712, viewModel.WindowHeight);
        Assert.Equal(460, viewModel.WindowMinHeight);
    }

    [Fact]
    public void WindowState_LoadsPersistedExpandedSizeButStartsCompact()
    {
        var viewModel = new PulseMeterWindowViewModel(
            new StubUsageService(),
            windowState: new PulseMeterWindowState(IsExpanded: true, Width: 1040, Height: 1200));

        Assert.False(viewModel.IsExpanded);
        Assert.Equal(410, viewModel.WindowWidth);
        Assert.Equal(66, viewModel.WindowHeight);

        viewModel.ToggleExpanded();

        Assert.Equal(1040, viewModel.WindowWidth);
        Assert.Equal(900, viewModel.WindowHeight);
    }

    [Fact]
    public void WindowState_UpgradesLegacyCompressedReferenceHeightToComfortableDefault()
    {
        var viewModel = new PulseMeterWindowViewModel(
            new StubUsageService(),
            windowState: new PulseMeterWindowState(IsExpanded: true, Width: 1024, Height: 1040));

        Assert.False(viewModel.IsExpanded);
        Assert.Equal(410, viewModel.WindowWidth);
        Assert.Equal(66, viewModel.WindowHeight);

        viewModel.ToggleExpanded();

        Assert.Equal(1024, viewModel.WindowWidth);
        Assert.Equal(712, viewModel.WindowHeight);
    }

    [Fact]
    public void WindowState_LoadsPersistedWindowPosition()
    {
        var viewModel = new PulseMeterWindowViewModel(
            new StubUsageService(),
            windowState: new PulseMeterWindowState(IsExpanded: true, Width: 1040, Height: 1200, Left: 42, Top: 84));

        Assert.True(viewModel.HasWindowPosition);
        Assert.Equal(42, viewModel.WindowLeft);
        Assert.Equal(84, viewModel.WindowTop);
        Assert.Equal(new PulseMeterWindowState(IsExpanded: false, Width: 1040, Height: 900, Left: 42, Top: 84), viewModel.CaptureWindowState());
    }

    [Fact]
    public void RememberWindowSize_PersistsExpandedUserResizeOnly()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.RememberWindowSize(1100, 1200);

        Assert.Equal(410, viewModel.WindowWidth);
        Assert.Equal(66, viewModel.WindowHeight);

        viewModel.ToggleExpanded();
        viewModel.RememberWindowSize(1100, 1200);

        Assert.Equal(1100, viewModel.WindowWidth);
        Assert.Equal(900, viewModel.WindowHeight);
        Assert.Equal(new PulseMeterWindowState(IsExpanded: true, Width: 1100, Height: 900), viewModel.CaptureWindowState());
    }

    [Fact]
    public void RememberWindowPosition_PersistsFiniteWindowCoordinates()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.RememberWindowPosition(120, 80);

        Assert.True(viewModel.HasWindowPosition);
        Assert.Equal(120, viewModel.WindowLeft);
        Assert.Equal(80, viewModel.WindowTop);
        Assert.Equal(new PulseMeterWindowState(IsExpanded: false, Width: 1024, Height: 712, Left: 120, Top: 80), viewModel.CaptureWindowState());

        viewModel.RememberWindowPosition(double.NaN, 40);

        Assert.Equal(120, viewModel.WindowLeft);
        Assert.Equal(80, viewModel.WindowTop);
    }

    [Fact]
    public void ApplySnapshot_UpdatesNeedsAttentionSection()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Stale,
            StatusMessage = "Using cached usage."
        });

        Assert.True(viewModel.NeedsAttention.HasNeedsAttention);
        var item = Assert.Single(viewModel.NeedsAttention.NeedsAttentionItems);
        Assert.Equal("Live data is stale", item.Title);
    }

    [Fact]
    public void ApplySnapshot_AddsAutomaticBudgetSignalsToNeedsAttention()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            SyncStatus = SyncStatus.Live,
            Source = "AppServer",
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Buckets =
            [
                new RateLimitBucket
                {
                    LimitId = "codex",
                    GroupLabel = "General",
                    Label = "5h Window",
                    WindowLabel = "5h",
                    WindowDurationMins = 300,
                    UsedPercent = 96,
                    ResetsAtUtc = DateTimeOffset.UtcNow.AddHours(2),
                    ResetsAtUnixSeconds = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds()
                }
            ]
        });

        Assert.Contains(viewModel.NeedsAttention.NeedsAttentionItems, item => item.Title == "5h budget is critical");
    }

    [Fact]
    public async Task MockSnapshot_ShowcasesAlertsWithoutChangingMockBadge()
    {
        var service = new MockCodexUsageService();
        var viewModel = new PulseMeterWindowViewModel(service);

        viewModel.ApplySnapshot(await service.GetSnapshotAsync());

        Assert.Equal("MOCK DATA", viewModel.StatusBadgeText);
        Assert.True(viewModel.RateLimits.HasRunwayHint);
        Assert.True(viewModel.NeedsAttention.HasNeedsAttention);
        var badges = viewModel.NeedsAttention.NeedsAttentionItems
            .Select(item => item.BadgeText)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("BUDGET", badges);
        Assert.Contains("IDLE", badges);
        Assert.Contains("RUNWAY", badges);
        Assert.Contains("LIMIT", badges);
        Assert.Contains("CREDIT", badges);
        Assert.Contains("TODAY", badges);
        Assert.Contains("PROJECT", badges);
        Assert.True(viewModel.NeedsAttention.NeedsAttentionItems.Count >= 7);
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public int GetSnapshotCallCount { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<UsageSnapshot>? SnapshotTask { get; init; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            GetSnapshotCallCount++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (SnapshotTask is not null)
            {
                return SnapshotTask;
            }

            var snapshot = new UsageSnapshot
            {
                Source = "AppServer",
                SyncStatus = SyncStatus.Live,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            SnapshotUpdated?.Invoke(this, snapshot);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class CountingUsageSignalsTracker : IUsageSignalsTracker
    {
        public int ObserveCallCount { get; private set; }

        public UsageSignalsSnapshot Observe(UsageSnapshot snapshot, DateTimeOffset nowUtc)
        {
            ObserveCallCount++;
            return UsageSignalsSnapshot.Empty;
        }

        public void DismissIdleDrain()
        {
        }
    }

    private static RateLimitBucket UsageBucket(double usedPercent)
    {
        return new RateLimitBucket
        {
            LimitId = "general",
            Label = "General",
            WindowDurationMins = 300,
            WindowLabel = "5h",
            UsedPercent = usedPercent
        };
    }
}
