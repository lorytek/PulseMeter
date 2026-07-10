using PulseMeter.Slices.UsageCollection;
using PulseMeter.Platform.Persistence;
using PulseMeter.Slices.PulseMeterWindow;

namespace PulseMeter.Tests;

public sealed class PulseMeterWindowViewModelSelectionTests
{
    [Fact]
    public void ApplySnapshot_CreatesLimitOptionsAndUsesSelectedGroupForQuotaRows()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 4),
                Bucket("codex", "General", "7d", 49),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "5h", 0),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "7d", 0)
            ],
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(["General", "GPT-5.3-Spark"], viewModel.LimitOptions.Select(option => option.DisplayName));
        Assert.Equal("General", viewModel.SelectedLimitOption?.DisplayName);
        Assert.Equal(["5h", "7d"], viewModel.SelectedBuckets.Select(bucket => bucket.WindowLabel));
        Assert.Equal(["5h", "Weekly"], viewModel.CompactQuotaRows.Select(row => row.Label));
        Assert.Equal(["96% left", "51% left"], viewModel.CompactQuotaRows.Select(row => row.RemainingPercentText));

        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.DisplayName == "GPT-5.3-Spark");

        Assert.Equal(["5h", "7d"], viewModel.SelectedBuckets.Select(bucket => bucket.WindowLabel));
        Assert.Equal(["100% left", "100% left"], viewModel.SelectedQuotaRows.Select(row => row.RemainingPercentText));
    }

    [Fact]
    public void QuotaRows_ShowRemainingQuotaAndResetForEachWindow()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var fiveHourResetLocal = new DateTimeOffset(
            2026,
            7,
            2,
            19,
            28,
            0,
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 2, 19, 28, 0)));

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 0, "4h 59m", fiveHourResetLocal.ToUniversalTime(), 300),
                Bucket("codex", "General", "7d", 0, "6d 23h 15m", null, 10_080)
            ],
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(["5h", "Weekly"], viewModel.CompactQuotaRows.Select(row => row.Label));
        Assert.Equal(["100% left", "100% left"], viewModel.CompactQuotaRows.Select(row => row.RemainingPercentText));
        Assert.Equal(["7:28 PM", "6d 23h 15m"], viewModel.CompactQuotaRows.Select(row => row.ResetDisplayText));
        Assert.Equal([false, true], viewModel.CompactQuotaRows.Select(row => row.IsWeekly));
    }

    [Fact]
    public void QuotaRows_ShowResetHourForFiveHourAndDayForWeeklyWindow()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var fiveHourResetLocal = new DateTimeOffset(
            2026,
            7,
            7,
            19,
            28,
            0,
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 7, 19, 28, 0)));
        var weeklyResetLocal = new DateTimeOffset(
            2026,
            7,
            7,
            9,
            15,
            0,
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 7, 9, 15, 0)));

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 45, resetsAtUtc: fiveHourResetLocal.ToUniversalTime(), windowDurationMins: 300),
                Bucket("codex", "General", "7d", 24, resetsAtUtc: weeklyResetLocal.ToUniversalTime(), windowDurationMins: 10_080)
            ],
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(
            ["7:28 PM", "Tue 9:15 AM"],
            viewModel.CompactQuotaRows.Select(row => row.ResetDisplayText));
    }

    [Fact]
    public void CompactHeader_UsesPulseMeterTitleWithRows()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var fiveHourResetLocal = new DateTimeOffset(
            2026,
            7,
            2,
            19,
            28,
            0,
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 2, 19, 28, 0)));

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 1, "4h 43m", fiveHourResetLocal.ToUniversalTime(), 300),
                Bucket("codex", "General", "7d", 50, "4d 18h 48m", null, 10_080)
            ],
            Source = "AppServer",
            SyncStatus = SyncStatus.Live,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal("PulseMeter - General", viewModel.CompactTitleText);
        Assert.Equal(["5h", "Weekly"], viewModel.CompactQuotaRows.Select(row => row.Label));
        Assert.Equal(["99% left", "50% left"], viewModel.CompactQuotaRows.Select(row => row.RemainingPercentText));
        Assert.Equal(["99%", "50% left"], viewModel.CompactQuotaRows.Select(row => row.CompactRemainingPercentText));
        Assert.Equal(["7:28 PM", "4d 18h 48m"], viewModel.CompactQuotaRows.Select(row => row.ResetDisplayText));
        Assert.Equal([false, true], viewModel.CompactQuotaRows.Select(row => row.ShowCompactSeparator));
        Assert.Equal(
            "5h \u2022 99% | Weekly \u2022 50% left",
            viewModel.CompactQuotaSummaryText);
    }

    [Fact]
    public void ApplySnapshot_PreservesSelectedLimitGroupWhenItStillExists()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 4),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "5h", 0)
            ]
        });
        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.Key == "codex_bengalfox");

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 8),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "5h", 1)
            ]
        });

        Assert.Equal("codex_bengalfox", viewModel.SelectedLimitOption?.Key);
        Assert.Equal("99% left", Assert.Single(viewModel.CompactQuotaRows).RemainingPercentText);
    }

    [Fact]
    public void RateLimitsDailyRows_ShowEmptyAllowanceChunksWhenWeeklyUsageIsZero()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 0, windowDurationMins: 300),
                Bucket("codex", "General", "7d", 0, windowDurationMins: 10_080)
            ]
        });

        Assert.True(viewModel.HasDailyRateLimitRows);
        Assert.Equal("Daily allowance to stay within your weekly limit.", viewModel.RateLimitsDailySummaryText);
        Assert.False(viewModel.HasRateLimitsDailyWarning);
        Assert.Equal(string.Empty, viewModel.RateLimitsDailyWarningText);
        Assert.Equal(["Day 1", "Day 2", "Day 3", "Day 4", "Day 5", "Day 6", "Day 7"], viewModel.DailyRateLimitRows.Select(row => row.Label));
        Assert.Equal("#1F73FF", viewModel.DailyRateLimitRows[0].LabelBrush);
        Assert.All(viewModel.DailyRateLimitRows.Skip(1), row => Assert.Equal("#6B7280", row.LabelBrush));
        Assert.All(viewModel.DailyRateLimitRows, row =>
        {
            Assert.Equal(100, row.RemainingPercentValue);
            Assert.Equal("100%", row.RemainingPercentText);
            Assert.Equal("#1F73FF", row.RingBrush);
            Assert.NotEqual(string.Empty, row.RingArcData);
        });
    }

    [Fact]
    public void RateLimitsDailyRows_ShowRemainingAllowanceAndAdvanceIntoNextChunk()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 2, windowDurationMins: 300),
                Bucket("codex", "General", "7d", 37, windowDurationMins: 10_080)
            ]
        });

        Assert.Equal(7, viewModel.DailyRateLimitRows.Count);
        Assert.Equal("0%", viewModel.DailyRateLimitRows[0].RemainingPercentText);
        Assert.Equal("0%", viewModel.DailyRateLimitRows[1].RemainingPercentText);
        Assert.Equal("41%", viewModel.DailyRateLimitRows[2].RemainingPercentText);
        Assert.Equal(["100%", "100%", "100%", "100%"], viewModel.DailyRateLimitRows.Skip(3).Select(row => row.RemainingPercentText));
        Assert.InRange(viewModel.DailyRateLimitRows[2].RemainingPercentValue, 40.9, 41.1);
        Assert.Equal("#EF4444", viewModel.DailyRateLimitRows[0].RingBrush);
        Assert.Equal("#EF4444", viewModel.DailyRateLimitRows[1].RingBrush);
        Assert.Equal("#9A5791", viewModel.DailyRateLimitRows[2].RingBrush);
        Assert.All(viewModel.DailyRateLimitRows.Skip(3), row => Assert.Equal("#1F73FF", row.RingBrush));
        Assert.Equal("#1F73FF", viewModel.DailyRateLimitRows[2].LabelBrush);
        Assert.All(viewModel.DailyRateLimitRows.Where((_, index) => index != 2), row => Assert.Equal("#6B7280", row.LabelBrush));
        Assert.True(viewModel.HasRateLimitsDailyWarning);
        Assert.Equal("Daily allowance exceeded; now consuming Day 3.", viewModel.RateLimitsDailyWarningText);
        Assert.All(viewModel.DailyRateLimitRows, row => Assert.NotEqual(string.Empty, row.RingArcData));
    }

    [Fact]
    public void RateLimitsDailyRows_HighlightCalendarDayAndWarnWhenFutureAllowanceIsConsumed()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());
        var resetAtUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(12);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 2, windowDurationMins: 300),
                Bucket("codex", "General", "7d", 63, resetsAtUtc: resetAtUtc, windowDurationMins: 10_080)
            ]
        });

        Assert.Equal(["0%", "0%", "0%", "0%", "59%", "100%", "100%"], viewModel.DailyRateLimitRows.Select(row => row.RemainingPercentText));
        Assert.Equal("#1F73FF", viewModel.DailyRateLimitRows[3].LabelBrush);
        Assert.All(viewModel.DailyRateLimitRows.Where((_, index) => index != 3), row => Assert.Equal("#6B7280", row.LabelBrush));
        Assert.True(viewModel.HasRateLimitsDailyWarning);
        Assert.Contains("using Day 5 allowance early", viewModel.RateLimitsDailyWarningText);
        Assert.Matches(@"Wait \d+h \d{2}m to get back on pace\.", viewModel.RateLimitsDailyWarningText);
    }

    [Fact]
    public void RateLimitsDailyRows_ClampFullyUsedWeeklyUsageToSevenFullChunks()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 80, windowDurationMins: 300),
                Bucket("codex", "General", "7d", 100, windowDurationMins: 10_080)
            ]
        });

        Assert.Equal(7, viewModel.DailyRateLimitRows.Count);
        Assert.All(viewModel.DailyRateLimitRows, row =>
        {
            Assert.Equal(0, row.RemainingPercentValue);
            Assert.Equal("0%", row.RemainingPercentText);
            Assert.Equal("#EF4444", row.RingBrush);
        });
        Assert.True(viewModel.HasRateLimitsDailyWarning);
        Assert.Equal("Daily allowance exceeded; weekly allowance is fully consumed.", viewModel.RateLimitsDailyWarningText);
        Assert.Equal("#1F73FF", viewModel.DailyRateLimitRows[6].LabelBrush);
    }

    [Fact]
    public void RateLimitsDailyRows_RebuildWhenSelectedTrackChanges()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 1, windowDurationMins: 300),
                Bucket("codex", "General", "7d", 37, windowDurationMins: 10_080),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "5h", 0, windowDurationMins: 300),
                Bucket("codex_bengalfox", "GPT-5.3-Codex-Spark", "7d", 100, windowDurationMins: 10_080)
            ]
        });

        Assert.Equal("41%", viewModel.DailyRateLimitRows[2].RemainingPercentText);

        viewModel.SelectedLimitOption = viewModel.LimitOptions.Single(option => option.Key == "codex_bengalfox");

        Assert.All(viewModel.DailyRateLimitRows, row => Assert.Equal("0%", row.RemainingPercentText));
        Assert.Equal("Daily allowance exceeded; weekly allowance is fully consumed.", viewModel.RateLimitsDailyWarningText);
        Assert.Equal("#1F73FF", viewModel.DailyRateLimitRows[6].LabelBrush);
    }

    [Fact]
    public void RateLimitsDailyRows_AreUnavailableWithoutWeeklyBucket()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            Buckets =
            [
                Bucket("codex", "General", "5h", 2, windowDurationMins: 300)
            ]
        });

        Assert.False(viewModel.HasDailyRateLimitRows);
        Assert.False(viewModel.HasRateLimitsDailyWarning);
        Assert.Empty(viewModel.DailyRateLimitRows);
        Assert.Equal("Weekly usage unavailable for this track.", viewModel.RateLimitsDailySummaryText);
    }

    [Fact]
    public void Collapse_HidesExpandedDetails()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ToggleExpanded();
        viewModel.Collapse();

        Assert.False(viewModel.IsExpanded);
    }

    [Fact]
    public void ExpandCollapseGlyph_TracksExpandedState()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.Equal("Ã¢â€“Â¼", viewModel.ExpandCollapseGlyph);
        Assert.Equal("Expand details", viewModel.ExpandCollapseTooltip);

        viewModel.ToggleExpanded();

        Assert.Equal("Ã¢â€“Â²", viewModel.ExpandCollapseGlyph);
        Assert.Equal("Collapse details", viewModel.ExpandCollapseTooltip);

        viewModel.Collapse();

        Assert.Equal("Ã¢â€“Â¼", viewModel.ExpandCollapseGlyph);
        Assert.Equal("Expand details", viewModel.ExpandCollapseTooltip);
    }

    [Fact]
    public void DailyUsagePanelCollapseGlyph_TracksExpandedState()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.True(viewModel.IsDailyUsageExpanded);
        Assert.Equal("-", viewModel.DailyUsageExpandCollapseGlyph);
        Assert.Equal("Collapse daily usage", viewModel.DailyUsageExpandCollapseTooltip);

        viewModel.ToggleDailyUsageExpanded();

        Assert.False(viewModel.IsDailyUsageExpanded);
        Assert.Equal("+", viewModel.DailyUsageExpandCollapseGlyph);
        Assert.Equal("Expand daily usage", viewModel.DailyUsageExpandCollapseTooltip);
    }

    [Fact]
    public void ToggleExpanded_KeepsDailyUsagePanelExpandedByDefault()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.ToggleExpanded();

        Assert.True(viewModel.IsExpanded);
        Assert.True(viewModel.IsDailyUsageExpanded);
        Assert.Equal("-", viewModel.DailyUsageExpandCollapseGlyph);
    }

    [Fact]
    public void NavigationPanelToggle_CollapsesAndExpandsTheSidebar()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.True(viewModel.IsNavigationPanelExpanded);
        Assert.Equal(205, viewModel.NavigationPanelWidth);
        Assert.Equal("\u00E2\u20AC\u00B9", viewModel.NavigationPanelToggleGlyph);
        Assert.Equal("Collapse navigation", viewModel.NavigationPanelToggleTooltip);

        viewModel.ToggleNavigationPanel();

        Assert.False(viewModel.IsNavigationPanelExpanded);
        Assert.Equal(64, viewModel.NavigationPanelWidth);
        Assert.Equal("\u00E2\u20AC\u00BA", viewModel.NavigationPanelToggleGlyph);
        Assert.Equal("Expand navigation", viewModel.NavigationPanelToggleTooltip);

        viewModel.ToggleNavigationPanel();

        Assert.True(viewModel.IsNavigationPanelExpanded);
        Assert.Equal(205, viewModel.NavigationPanelWidth);
        Assert.Equal("\u00E2\u20AC\u00B9", viewModel.NavigationPanelToggleGlyph);
    }

    [Fact]
    public void HasThreadContext_IsTrueOnlyWhenSnapshotContainsThreadData()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        Assert.False(viewModel.HasThreadContext);

        viewModel.ApplySnapshot(new UsageSnapshot
        {
            RecentActiveThread = new ThreadUsageSnapshot
            {
                ThreadName = "PulseMeter test thread",
                TotalTokens = 12_000,
                ContextLeftPercent = 44
            }
        });

        Assert.True(viewModel.HasThreadContext);

        viewModel.ApplySnapshot(new UsageSnapshot());

        Assert.False(viewModel.HasThreadContext);
    }

    [Fact]
    public void UserHiddenState_CanBeClearedWhenPulseMeterIsShownAgain()
    {
        var viewModel = new PulseMeterWindowViewModel(new StubUsageService());

        viewModel.MarkHiddenByUser();
        Assert.True(viewModel.IsHiddenByUser);

        viewModel.MarkShownByUser();
        Assert.False(viewModel.IsHiddenByUser);
    }

    private static RateLimitBucket Bucket(
        string limitId,
        string groupLabel,
        string windowLabel,
        double usedPercent,
        string resetCountdown = "2h 00m",
        DateTimeOffset? resetsAtUtc = null,
        int? windowDurationMins = null)
    {
        return new RateLimitBucket
        {
            LimitId = limitId,
            GroupLabel = groupLabel,
            WindowLabel = windowLabel,
            Label = groupLabel,
            UsedPercent = usedPercent,
            WindowDurationMins = windowDurationMins,
            ResetsAtUtc = resetsAtUtc,
            ResetCountdown = resetCountdown
        };
    }

    private sealed class StubUsageService : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new UsageSnapshot();
            SnapshotUpdated?.Invoke(this, snapshot);
            return Task.FromResult(snapshot);
        }
    }
}
