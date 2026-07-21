using PulseMeter.Platform.Codex;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class CodexUsageServiceTests
{
    [Fact]
    public void ShouldFallbackFromRefreshException_ReturnsTrueForInternalTimeout()
    {
        Assert.True(CodexUsageService.ShouldFallbackFromRefreshException(new TaskCanceledException(), CancellationToken.None));
    }

    [Fact]
    public void ShouldFallbackFromRefreshException_ReturnsFalseWhenCallerCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(CodexUsageService.ShouldFallbackFromRefreshException(new TaskCanceledException(), cts.Token));
    }

    [Fact]
    public async Task FallbackWithoutLastGoodLiveSnapshot_ShowsUnavailableInsteadOfMockData()
    {
        await using var service = CreateService();
        var fallback = await InvokeFallbackAsync(service, new InvalidOperationException("Codex is closed"));

        Assert.Equal(SyncStatus.Unavailable, fallback.SyncStatus);
        Assert.Equal("AppServer", fallback.Source);
        Assert.Empty(fallback.Buckets);
        Assert.Empty(fallback.DailyBuckets);
        Assert.Null(fallback.ResetCreditsAvailable);
        Assert.Contains("The live usage source is unavailable", fallback.StatusMessage);
        Assert.DoesNotContain("mock", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSnapshotAsync_RedactsWrappedAppServerLaunchFailureDetails()
    {
        const string sensitiveDetails = "RPC response {\"accountId\":\"acct_123\",\"token\":\"secret-token\"} from C:\\Users\\Alice\\private\\codex.exe";

        await using var service = CreateService(
            processFactory: new ThrowingProcessFactory(new AppServerLaunchException(new Win32Exception(sensitiveDetails))));
        var fallback = await service.GetSnapshotAsync();

        Assert.Contains("The monitored CLI is unavailable", fallback.StatusMessage);
        Assert.DoesNotContain("C:\\Users", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alice", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acct_123", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RPC response", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FallbackWithoutLastGoodLiveSnapshot_RedactsTimeoutAndGeneralFailureDetails()
    {
        const string sensitiveDetails = "RPC response {\"serverId\":\"srv_456\",\"token\":\"secret-token\"} from C:\\Users\\Alice\\private\\codex.exe";

        await using var service = CreateService();
        var timeoutFallback = await InvokeFallbackAsync(service, new TaskCanceledException(sensitiveDetails));
        var generalFailureFallback = await InvokeFallbackAsync(service, new InvalidOperationException(sensitiveDetails));

        Assert.Contains("timed out", timeoutFallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The live usage source is unavailable", generalFailureFallback.StatusMessage);

        foreach (var statusMessage in new[] { timeoutFallback.StatusMessage, generalFailureFallback.StatusMessage })
        {
            Assert.DoesNotContain("C:\\Users", statusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Alice", statusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("srv_456", statusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-token", statusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RPC response", statusMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetSnapshotAsync_ThrowsWhenCallerCancelsDuringFallbackWithLastGoodData()
    {
        using var cancellationSource = new CancellationTokenSource();
        var client = new CancelOnDisposeAfterRefreshFailureJsonRpcClient(cancellationSource);
        await using var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        var baseline = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, baseline.SyncStatus);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetSnapshotAsync(cancellationSource.Token));
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsLiveSnapshotBeforeAnalyticsCompletesAndPublishesEnrichment()
    {
        var projectUsage = new BlockingProjectUsageService();
        var attribution = new StubUsageAttributionService(new UsageAttributionSnapshot
        {
            Sessions =
            [
                new UsageAttributionSessionRow(
                    "Implement attribution",
                    "thread-1",
                    "PulseMeter",
                    @"C:\Projects\PulseMeter",
                    500,
                    1_000,
                    100,
                    300,
                    100,
                    50,
                    20,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ],
            RawLocalTokens = 500,
            EstimatedAttributedTokens = 1_000,
            AccountWindowTokens = 1_000
        });
        await using var service = CreateService(projectUsageService: projectUsage, usageAttributionService: attribution, jsonRpcClientFactory: new StubJsonRpcClientFactory(new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{}}"),
            ("thread/loaded/list", "[]"))));
        var enrichedSnapshot = new TaskCompletionSource<UsageSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.SnapshotUpdated += (_, updated) =>
        {
            if (updated.UsageAttribution.HasAttribution)
            {
                enrichedSnapshot.TrySetResult(updated);
            }
        };

        var liveSnapshot = await service.GetSnapshotAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(SyncStatus.Live, liveSnapshot.SyncStatus);
        Assert.False(liveSnapshot.UsageAttribution.HasAttribution);
        await projectUsage.Started.WaitAsync(TimeSpan.FromSeconds(2));
        projectUsage.Release();

        var snapshot = await enrichedSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(attribution.Snapshot, snapshot.UsageAttribution);
        Assert.Single(snapshot.UsageAttribution.Sessions);
        Assert.Equal(1, attribution.CallCount);
        Assert.Equal(1_000, Assert.Single(attribution.DailyBuckets).TotalTokens);
    }

    [Fact]
    public async Task AnalyticsRefresh_CoalescesPendingRequestsAndNeverRunsConcurrently()
    {
        var projectUsage = new CoalescingProjectUsageService();
        await using var service = CreateService(
            projectUsageService: projectUsage,
            jsonRpcClientFactory: new StubJsonRpcClientFactory(new StubJsonRpcClient(
                ("initialize", "{}"),
                ("account/rateLimits/read", "{}"),
                ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{}}"),
                ("thread/loaded/list", "[]"))));

        await service.GetSnapshotAsync();
        await projectUsage.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(2));

        await service.GetSnapshotAsync();
        await service.GetSnapshotAsync();
        projectUsage.ReleaseFirstCall();
        await projectUsage.SecondCallCompleted.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, projectUsage.CallCount);
        Assert.Equal(1, projectUsage.MaxConcurrentCalls);
    }

    [Fact]
    public async Task DisposeAsync_CancelsOwnedAnalyticsRefresh()
    {
        var projectUsage = new CancellationObservingProjectUsageService();
        var service = CreateService(
            projectUsageService: projectUsage,
            jsonRpcClientFactory: new StubJsonRpcClientFactory(new StubJsonRpcClient(
                ("initialize", "{}"),
                ("account/rateLimits/read", "{}"),
                ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{}}"),
                ("thread/loaded/list", "[]"))));

        await service.GetSnapshotAsync();
        await projectUsage.Started.WaitAsync(TimeSpan.FromSeconds(2));

        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        await projectUsage.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndWaitsForActiveRefreshAndIsSharedByConcurrentCallers()
    {
        var client = new CancellationBlockingJsonRpcClient();
        var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));
        var refresh = service.GetSnapshotAsync();
        await client.RefreshStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var firstDispose = service.DisposeAsync().AsTask();
        var secondDispose = service.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        await firstDispose.WaitAsync(TimeSpan.FromSeconds(2));
        await client.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, client.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetSnapshotAsync());
    }

    [Fact]
    public async Task DisposeAsync_WaitsForRefreshRegisteredBeforeRefreshLockAcquisition()
    {
        var service = CreateService();
        var refreshLockField = typeof(CodexUsageService).GetField(
            "_refreshLock",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var activeOperationCountField = typeof(CodexUsageService).GetField(
            "_activeOperationCount",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(refreshLockField);
        Assert.NotNull(activeOperationCountField);
        var refreshLock = Assert.IsType<SemaphoreSlim>(refreshLockField.GetValue(service));
        await refreshLock.WaitAsync();

        var refresh = service.GetSnapshotAsync();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (Assert.IsType<int>(activeOperationCountField.GetValue(service)) == 1)
            {
                break;
            }

            await Task.Delay(10);
        }

        Assert.Equal(1, Assert.IsType<int>(activeOperationCountField.GetValue(service)));
        var disposal = service.DisposeAsync().AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        await disposal.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetSnapshotAsync_SendsInitializedNotificationBeforeAwaitingInitializeResponse()
    {
        var client = new HandshakeBlockingJsonRpcClient();
        await using var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        await service.GetSnapshotAsync();

        Assert.True(client.Calls.Count >= 2);
        Assert.Equal("request:initialize", client.Calls[0]);
        Assert.Equal("notification:initialized", client.Calls[1]);
        Assert.True(client.InitializeWasPendingWhenInitialized);
        Assert.Contains("\"capabilities\"", client.InitializeParametersJson);
        Assert.Contains("\"experimentalApi\":false", client.InitializeParametersJson);
        Assert.Contains("\"requestAttestation\":false", client.InitializeParametersJson);
        var expectedVersion = typeof(CodexUsageService).Assembly.GetName().Version?.ToString(3);
        Assert.Contains($"\"version\":\"{expectedVersion}\"", client.InitializeParametersJson);
        Assert.Contains("request:account/rateLimits/read", client.Calls);
        Assert.Contains("request:account/usage/read", client.Calls);
    }

    [Fact]
    public async Task GetSnapshotAsync_StillReadsAccountUsageWhenResetCreditLookupTimesOut()
    {
        var client = new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{\"lifetimeTokens\":2500}}"),
            ("thread/loaded/list", "[]"));
        await using var service = CreateService(
            resetCreditService: new TimeoutResetCreditService(),
            jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, snapshot.SyncStatus);
        Assert.Equal(2_500, snapshot.LifetimeTokens);
        Assert.Single(snapshot.DailyBuckets);
    }

    [Fact]
    public async Task GetSnapshotAsync_KeepsLiveSnapshotWhenResetCreditDatesAreOutOfRange()
    {
        using var resetCreditDocument = JsonDocument.Parse("""
            {
              "available_count": 1,
              "credits": [
                {
                  "status": "available",
                  "granted_at": 9223372036854775807,
                  "expires_at": "9223372036854775807"
                }
              ]
            }
            """);
        var resetCredits = CodexResetCreditService.ParseResponse(resetCreditDocument.RootElement);
        var processFactory = new StubProcessFactory();
        var client = new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{\"lifetimeTokens\":2500}}"),
            ("thread/loaded/list", "[]"));
        await using var service = CreateService(
            resetCreditService: new StaticResetCreditService(resetCredits),
            processFactory: processFactory,
            jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, snapshot.SyncStatus);
        Assert.Equal(2_500, snapshot.LifetimeTokens);
        Assert.Equal(1, snapshot.ResetCreditsAvailable);
        Assert.Null(snapshot.ResetCreditsExpiresAtUtc);
        Assert.Equal(1, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_StillReturnsUsageWhenLoadedThreadProbeTimesOut()
    {
        var client = new ThreadListTimeoutJsonRpcClient();
        await using var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, snapshot.SyncStatus);
        Assert.Equal(2_500, snapshot.LifetimeTokens);
        Assert.Single(snapshot.DailyBuckets);
    }

    [Fact]
    public async Task AccountNotifications_CoalesceBurstIntoCurrentAndTrailingRefresh()
    {
        var client = new NotificationBurstJsonRpcClient();
        await using var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        await service.GetSnapshotAsync();
        client.RaiseAccountNotification();
        await client.FirstNotificationRefreshStarted;

        for (var i = 0; i < 20; i++)
        {
            client.RaiseAccountNotification();
        }

        client.ReleaseFirstNotificationRefresh();
        await client.SecondNotificationRefreshCompleted;
        await Task.Delay(100);

        Assert.Equal(3, client.RateLimitReadCount);
        Assert.False(client.ExtraNotificationRefreshStarted.IsCompleted);
    }

    [Fact]
    public async Task AccountNotification_AtPumpExitStartsAnotherRefresh()
    {
        var client = new NotificationBurstJsonRpcClient();
        await using var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        await service.GetSnapshotAsync();
        client.RaiseAccountNotification();
        await client.FirstNotificationRefreshStarted;
        client.RaiseAccountNotification();
        client.ReleaseFirstNotificationRefresh();
        await client.SecondNotificationRefreshCompleted;

        client.RaiseAccountNotification();
        await client.ExtraNotificationRefreshStarted;

        Assert.Equal(4, client.RateLimitReadCount);
    }

    [Fact]
    public async Task DisposeAsync_CancelsBlockedNotificationRefreshAndDropsTrailingRequest()
    {
        var client = new NotificationBurstJsonRpcClient();
        var service = CreateService(jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        await service.GetSnapshotAsync();
        client.RaiseAccountNotification();
        await client.FirstNotificationRefreshStarted;

        for (var i = 0; i < 5; i++)
        {
            client.RaiseAccountNotification();
        }

        await service.DisposeAsync();

        Assert.Equal(2, client.RateLimitReadCount);
        Assert.False(client.ExtraNotificationRefreshStarted.IsCompleted);
        Assert.True(client.WasDisposed);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReusesHealthyAppServerAcrossRefreshes()
    {
        var processFactory = new StubProcessFactory();
        var client = new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[],\"summary\":{}}"),
            ("thread/loaded/list", "[]"));
        await using var service = CreateService(
            processFactory: processFactory,
            jsonRpcClientFactory: new StubJsonRpcClientFactory(client));

        await service.GetSnapshotAsync();
        await service.GetSnapshotAsync();

        Assert.Equal(1, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReplacesConnectionAfterRequestFailure()
    {
        var failedClient = new StubJsonRpcClient(("initialize", "{}"));
        var healthyClient = new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[],\"summary\":{}}"),
            ("thread/loaded/list", "[]"));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(failedClient, healthyClient));

        var failed = await service.GetSnapshotAsync();
        var recovered = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Unavailable, failed.SyncStatus);
        Assert.Equal(SyncStatus.Live, recovered.SyncStatus);
        Assert.Equal(2, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_AcceptsConfirmedUsageDropInsideSameWindow()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var baselineClient = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset),
            RateLimitsJson(5, reset.AddMinutes(3)));
        var staleConfirmationClient = RateLimitsOnlyClient(RateLimitsJson(5, reset.AddMinutes(3)));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(
                baselineClient,
                staleConfirmationClient));

        var baseline = await service.GetSnapshotAsync();
        var refreshed = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, baseline.SyncStatus);
        Assert.Equal(SyncStatus.Live, refreshed.SyncStatus);
        Assert.Equal(5, Assert.Single(refreshed.Buckets).UsedPercent);
        Assert.Equal(2, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_KeepsLastConfirmedValuesWhenFreshReadsDisagree()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var baselineClient = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset),
            RateLimitsJson(5, reset.AddMinutes(3)));
        var conflictingConfirmationClient = RateLimitsOnlyClient(RateLimitsJson(58, reset.AddMinutes(4)));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(
                baselineClient,
                conflictingConfirmationClient));

        var baseline = await service.GetSnapshotAsync();
        var rejected = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Stale, rejected.SyncStatus);
        Assert.Equal(60, Assert.Single(rejected.Buckets).UsedPercent);
        Assert.Equal(baseline.LastUpdatedUtc, rejected.LastUpdatedUtc);
        Assert.Contains("last confirmed", rejected.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_RejectsOneOffShortWindowOmissionWhenResetCreditWasConsumed()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var client = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset, 25, weeklyReset),
            RateLimitsJson(null, null, 25, weeklyReset));
        var confirmationClient = RateLimitsOnlyClient(RateLimitsJson(60, reset, 25, weeklyReset));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            resetCreditService: new SequentialResetCreditService(2, 1),
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(client, confirmationClient));

        var baseline = await service.GetSnapshotAsync();
        var refreshed = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Stale, refreshed.SyncStatus);
        Assert.Equal(2, refreshed.Buckets.Count);
        Assert.Equal(2, processFactory.StartCount);
        Assert.Equal(2, baseline.ResetCreditsAvailable);
    }

    [Fact]
    public async Task GetSnapshotAsync_AcceptsConfirmedShortWindowRemovalFromEndpoint()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var baselineClient = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset, 25, weeklyReset),
            RateLimitsJson(null, null, 25, weeklyReset));
        var confirmationClient = RateLimitsOnlyClient(RateLimitsJson(null, null, 26, weeklyReset.AddMinutes(1)));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            resetCreditService: new SequentialResetCreditService(2, 1),
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(baselineClient, confirmationClient));

        await service.GetSnapshotAsync();
        var refreshed = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, refreshed.SyncStatus);
        var weekly = Assert.Single(refreshed.Buckets);
        Assert.Equal(10080, weekly.WindowDurationMins);
        Assert.Equal(26, weekly.UsedPercent);
        Assert.Equal(1, refreshed.ResetCreditsAvailable);
        Assert.Equal(2, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_RejectsOneOffAddedTopology()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var baselineClient = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset),
            RateLimitsJson(62, reset.AddMinutes(1), 25, weeklyReset));
        var confirmationClient = RateLimitsOnlyClient(RateLimitsJson(64, reset.AddMinutes(2)));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(baselineClient, confirmationClient));

        await service.GetSnapshotAsync();
        var refreshed = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Stale, refreshed.SyncStatus);
        Assert.Single(refreshed.Buckets);
        Assert.Equal(60, refreshed.Buckets[0].UsedPercent);
        Assert.Equal(2, processFactory.StartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_AcceptsConfirmedAddedTopologyAndPreservesCandidateResetCreditMetadata()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var weeklyReset = DateTimeOffset.UtcNow.AddDays(2);
        var credit = new ResetCreditSnapshot(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(5),
            "available");
        var baselineClient = new SequentialRateLimitJsonRpcClient(
            RateLimitsJson(60, reset),
            RateLimitsJson(62, reset.AddMinutes(1), 25, weeklyReset));
        var confirmationClient = RateLimitsOnlyClient(
            RateLimitsJson(64, reset.AddMinutes(2), 26, weeklyReset.AddMinutes(1)));
        var processFactory = new StubProcessFactory();
        await using var service = CreateService(
            resetCreditService: new StaticResetCreditService(new ResetCreditFetchResult(2, [credit])),
            processFactory: processFactory,
            jsonRpcClientFactory: new SequenceJsonRpcClientFactory(baselineClient, confirmationClient));

        await service.GetSnapshotAsync();
        var refreshed = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, refreshed.SyncStatus);
        Assert.Equal(2, refreshed.Buckets.Count);
        Assert.Equal(26, refreshed.Buckets.Single(bucket => bucket.WindowDurationMins == 10080).UsedPercent);
        Assert.Equal(2, refreshed.ResetCreditsAvailable);
        Assert.Equal(credit.ExpiresAtUtc, refreshed.ResetCreditsExpiresAtUtc);
        Assert.Equal([credit], refreshed.ResetCredits);
        Assert.Equal(2, processFactory.StartCount);
    }

    private static StubJsonRpcClient RateLimitsOnlyClient(string rateLimitsJson)
    {
        return new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", rateLimitsJson));
    }

    private static string RateLimitsJson(double usedPercent, DateTimeOffset reset)
    {
        return JsonSerializer.Serialize(new
        {
            rateLimitsByLimitId = new
            {
                codex = new
                {
                    primary = new
                    {
                        usedPercent,
                        windowDurationMins = 300,
                        resetsAt = reset.ToUnixTimeSeconds()
                    }
                }
            }
        });
    }

    private static string RateLimitsJson(
        double? primaryUsedPercent,
        DateTimeOffset? primaryReset,
        double weeklyUsedPercent,
        DateTimeOffset weeklyReset)
    {
        return JsonSerializer.Serialize(new
        {
            rateLimitsByLimitId = new
            {
                codex = new
                {
                    primary = primaryUsedPercent is null || primaryReset is null
                        ? null
                        : new
                        {
                            usedPercent = primaryUsedPercent.Value,
                            windowDurationMins = 300,
                            resetsAt = primaryReset.Value.ToUnixTimeSeconds()
                        },
                    secondary = new
                    {
                        usedPercent = weeklyUsedPercent,
                        windowDurationMins = 10080,
                        resetsAt = weeklyReset.ToUnixTimeSeconds()
                    }
                }
            }
        });
    }

    private static async Task<UsageSnapshot> InvokeFallbackAsync(CodexUsageService service, Exception exception)
    {
        var method = typeof(CodexUsageService).GetMethod(
            "BuildFallbackSnapshotAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<UsageSnapshot>?)method.Invoke(service, [exception, CancellationToken.None]);
        Assert.NotNull(task);

        return await task;
    }

    private static CodexUsageService CreateService(
        ICodexResetCreditService? resetCreditService = null,
        SharedRolloutAnalyticsSource? rolloutAnalyticsSource = null,
        IProjectUsageService? projectUsageService = null,
        IUsageAttributionService? usageAttributionService = null,
        IAppServerProcessFactory? processFactory = null,
        IJsonRpcClientFactory? jsonRpcClientFactory = null)
    {
        return new CodexUsageService(
            new StubMockUsageService(),
            resetCreditService ?? new StubResetCreditService(),
            rolloutAnalyticsSource ?? new SharedRolloutAnalyticsSource(
                Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"))),
            projectUsageService ?? new StubProjectUsageService(),
            usageAttributionService ?? new StubUsageAttributionService(UsageAttributionSnapshot.Empty),
            processFactory ?? new StubProcessFactory(),
            jsonRpcClientFactory ?? new StubJsonRpcClientFactory());
    }

    private sealed class StubMockUsageService : IMockUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new UsageSnapshot { SyncStatus = SyncStatus.Mocked, Source = "Mock" };
            SnapshotUpdated?.Invoke(this, snapshot);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubResetCreditService : ICodexResetCreditService
    {
        public Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ResetCreditFetchResult?>(null);
        }
    }

    private sealed class TimeoutResetCreditService : ICodexResetCreditService
    {
        public Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
        {
            throw new TaskCanceledException("Reset credit lookup timed out.");
        }
    }

    private sealed class SequentialResetCreditService(params int[] availableCounts) : ICodexResetCreditService
    {
        private readonly Queue<int> _availableCounts = new(availableCounts);

        public Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ResetCreditFetchResult?>(
                new ResetCreditFetchResult(_availableCounts.Dequeue(), []));
        }
    }

    private sealed class StaticResetCreditService(ResetCreditFetchResult result) : ICodexResetCreditService
    {
        public Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ResetCreditFetchResult?>(result);
        }
    }

    private sealed class StubProjectUsageService : IProjectUsageService
    {
        public Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProjectUsageRow>>(Array.Empty<ProjectUsageRow>());
        }
    }

    private sealed class BlockingProjectUsageService : IProjectUsageService
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public void Release()
        {
            _release.TrySetResult();
        }

        public async Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return Array.Empty<ProjectUsageRow>();
        }
    }

    private sealed class CoalescingProjectUsageService : IProjectUsageService
    {
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondCallCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;
        private int _concurrentCalls;
        private int _maxConcurrentCalls;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public Task SecondCallCompleted => _secondCallCompleted.Task;

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaxConcurrentCalls => Volatile.Read(ref _maxConcurrentCalls);

        public void ReleaseFirstCall()
        {
            _releaseFirstCall.TrySetResult();
        }

        public async Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            var concurrentCalls = Interlocked.Increment(ref _concurrentCalls);
            UpdateMaximum(ref _maxConcurrentCalls, concurrentCalls);

            try
            {
                if (call == 1)
                {
                    _firstCallStarted.TrySetResult();
                    await _releaseFirstCall.Task.WaitAsync(cancellationToken);
                }

                return Array.Empty<ProjectUsageRow>();
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCalls);
                if (call == 2)
                {
                    _secondCallCompleted.TrySetResult();
                }
            }
        }

        private static void UpdateMaximum(ref int maximum, int value)
        {
            var current = Volatile.Read(ref maximum);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref maximum, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }

    private sealed class CancellationObservingProjectUsageService : IProjectUsageService
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Task CancellationObserved => _cancellationObserved.Task;

        public async Task<IReadOnlyList<ProjectUsageRow>> GetProjectUsageAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Array.Empty<ProjectUsageRow>();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _cancellationObserved.TrySetResult();
                throw;
            }
        }
    }

    private sealed class StubUsageAttributionService : IUsageAttributionService
    {
        public StubUsageAttributionService(UsageAttributionSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public UsageAttributionSnapshot Snapshot { get; }

        public int CallCount { get; private set; }

        public IReadOnlyList<DailyUsageBucket> DailyBuckets { get; private set; } = Array.Empty<DailyUsageBucket>();

        public Task<UsageAttributionSnapshot> GetUsageAttributionAsync(
            IReadOnlyList<DailyUsageBucket> dailyBuckets,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            DailyBuckets = dailyBuckets;
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class StubProcessFactory : IAppServerProcessFactory
    {
        public int StartCount { get; private set; }

        public IAppServerProcess Start(string? executable = null)
        {
            StartCount++;
            return new StubAppServerProcess();
        }
    }

    private sealed class ThrowingProcessFactory(Exception exception) : IAppServerProcessFactory
    {
        public IAppServerProcess Start(string? executable = null)
        {
            throw exception;
        }
    }

    private sealed class StubJsonRpcClientFactory : IJsonRpcClientFactory
    {
        private readonly IJsonRpcClient? _client;

        public StubJsonRpcClientFactory(IJsonRpcClient? client = null)
        {
            _client = client;
        }

        public IJsonRpcClient Create(StreamReader reader, StreamWriter writer)
        {
            return _client ?? throw new InvalidOperationException("Not used by fallback test.");
        }
    }

    private sealed class SequenceJsonRpcClientFactory(params IJsonRpcClient[] clients) : IJsonRpcClientFactory
    {
        private readonly Queue<IJsonRpcClient> _clients = new(clients);

        public IJsonRpcClient Create(StreamReader reader, StreamWriter writer)
        {
            return _clients.Count > 0
                ? _clients.Dequeue()
                : throw new InvalidOperationException("No JSON-RPC client remains for this connection.");
        }
    }

    private sealed class StubAppServerProcess : IAppServerProcess
    {
        private readonly MemoryStream _output = new();
        private readonly MemoryStream _input = new();

        public StreamReader Output => new(_output);

        public StreamWriter Input => new(_input);

        public bool HasExited => false;

        public void Dispose()
        {
            _output.Dispose();
            _input.Dispose();
        }
    }

    private sealed class CancellationBlockingJsonRpcClient : IJsonRpcClient
    {
        private readonly TaskCompletionSource _refreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposeCount;

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public Task RefreshStarted => _refreshStarted.Task;

        public Task CancellationObserved => _cancellationObserved.Task;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void Start()
        {
        }

        public async Task<JsonElement> SendRequestAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (method == "initialize")
            {
                return ParseElement("{}");
            }

            if (method != "account/rateLimits/read")
            {
                throw new InvalidOperationException("Unexpected request: " + method);
            }

            _refreshStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ParseElement("{}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _cancellationObserved.TrySetResult();
                throw;
            }
        }

        public Task SendNotificationAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class StubJsonRpcClient : IJsonRpcClient
    {
        private readonly Dictionary<string, JsonElement> _responses;

        public StubJsonRpcClient(params (string Method, string Json)[] responses)
        {
            _responses = responses.ToDictionary(item => item.Method, item => ParseElement(item.Json), StringComparer.Ordinal);
        }

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public Task<JsonElement> SendRequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            if (!_responses.TryGetValue(method, out var response))
            {
                throw new InvalidOperationException("Unexpected request: " + method);
            }

            return Task.FromResult(response);
        }

        public Task SendNotificationAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class CancelOnDisposeAfterRefreshFailureJsonRpcClient(CancellationTokenSource cancellationSource) : IJsonRpcClient
    {
        private int _rateLimitReadCount;

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public Task<JsonElement> SendRequestAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return method switch
            {
                "initialize" => Task.FromResult(ParseElement("{}")),
                "account/rateLimits/read" when _rateLimitReadCount++ == 0 => Task.FromResult(ParseElement(RateLimitsJson(60, DateTimeOffset.UtcNow.AddHours(2)))),
                "account/rateLimits/read" => throw new InvalidOperationException("Live refresh failed."),
                "account/usage/read" => Task.FromResult(ParseElement("{\"dailyUsageBuckets\":[],\"summary\":{}}")),
                "thread/loaded/list" => Task.FromResult(ParseElement("[]")),
                _ => throw new InvalidOperationException("Unexpected request: " + method)
            };
        }

        public Task SendNotificationAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            cancellationSource.Cancel();
            return ValueTask.CompletedTask;
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class NotificationBurstJsonRpcClient : IJsonRpcClient
    {
        private readonly TaskCompletionSource _firstNotificationRefreshStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstNotificationRefresh =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondNotificationRefreshCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _extraNotificationRefreshStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _rateLimitReadCount;
        private int _loadedThreadReadCount;

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

        public Task FirstNotificationRefreshStarted => _firstNotificationRefreshStarted.Task;

        public Task SecondNotificationRefreshCompleted => _secondNotificationRefreshCompleted.Task;

        public Task ExtraNotificationRefreshStarted => _extraNotificationRefreshStarted.Task;

        public int RateLimitReadCount => Volatile.Read(ref _rateLimitReadCount);

        public bool WasDisposed { get; private set; }

        public void Start()
        {
        }

        public void RaiseAccountNotification()
        {
            NotificationReceived?.Invoke(
                this,
                new JsonRpcNotificationEventArgs("account/rateLimits/updated", ParseElement("{}")));
        }

        public void ReleaseFirstNotificationRefresh()
        {
            _releaseFirstNotificationRefresh.TrySetResult();
        }

        public Task<JsonElement> SendRequestAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return method switch
            {
                "initialize" => Task.FromResult(ParseElement("{}")),
                "account/rateLimits/read" => ReadRateLimitsAsync(cancellationToken),
                "account/usage/read" => Task.FromResult(ParseElement("{\"dailyUsageBuckets\":[],\"summary\":{}}")),
                "thread/loaded/list" => ReadLoadedThreadsAsync(),
                _ => throw new InvalidOperationException("Unexpected request: " + method)
            };
        }

        public Task SendNotificationAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return ValueTask.CompletedTask;
        }

        private async Task<JsonElement> ReadRateLimitsAsync(CancellationToken cancellationToken)
        {
            var readCount = Interlocked.Increment(ref _rateLimitReadCount);
            if (readCount == 2)
            {
                _firstNotificationRefreshStarted.TrySetResult();
                await _releaseFirstNotificationRefresh.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (readCount >= 4)
            {
                _extraNotificationRefreshStarted.TrySetResult();
            }

            return ParseElement(RateLimitsJson(60, DateTimeOffset.UtcNow.AddHours(2)));
        }

        private Task<JsonElement> ReadLoadedThreadsAsync()
        {
            if (Interlocked.Increment(ref _loadedThreadReadCount) == 3)
            {
                _secondNotificationRefreshCompleted.TrySetResult();
            }

            return Task.FromResult(ParseElement("[]"));
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class SequentialRateLimitJsonRpcClient(params string[] rateLimitResponses) : IJsonRpcClient
    {
        private readonly Queue<JsonElement> _rateLimitResponses = new(rateLimitResponses.Select(ParseElement));

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public Task<JsonElement> SendRequestAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return method switch
            {
                "initialize" => Task.FromResult(ParseElement("{}")),
                "account/rateLimits/read" when _rateLimitResponses.Count > 0 => Task.FromResult(_rateLimitResponses.Dequeue()),
                "account/usage/read" => Task.FromResult(ParseElement("{\"dailyUsageBuckets\":[],\"summary\":{}}")),
                "thread/loaded/list" => Task.FromResult(ParseElement("[]")),
                _ => throw new InvalidOperationException("Unexpected request: " + method)
            };
        }

        public Task SendNotificationAsync(
            string method,
            object? parameters = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class HandshakeBlockingJsonRpcClient : IJsonRpcClient
    {
        private readonly TaskCompletionSource<JsonElement> _initializeResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _initializeRequested;

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public List<string> Calls { get; } = [];

        public string InitializeParametersJson { get; private set; } = string.Empty;

        public bool InitializeWasPendingWhenInitialized { get; private set; }

        public void Start()
        {
        }

        public Task<JsonElement> SendRequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("request:" + method);

            return method switch
            {
                "initialize" => WaitForInitializeAsync(parameters, cancellationToken),
                "account/rateLimits/read" => Task.FromResult(ParseElement("{}")),
                "account/usage/read" => Task.FromResult(ParseElement("{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{}}")),
                "thread/loaded/list" => Task.FromResult(ParseElement("[]")),
                _ => throw new InvalidOperationException("Unexpected request: " + method)
            };
        }

        public Task SendNotificationAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("notification:" + method);

            if (method == "initialized")
            {
                InitializeWasPendingWhenInitialized = _initializeRequested && !_initializeResponse.Task.IsCompleted;
                _initializeResponse.TrySetResult(ParseElement("{}"));
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private async Task<JsonElement> WaitForInitializeAsync(object? parameters, CancellationToken cancellationToken)
        {
            _initializeRequested = true;
            InitializeParametersJson = JsonSerializer.Serialize(parameters);

            using var registration = cancellationToken.Register(() => _initializeResponse.TrySetCanceled(cancellationToken));
            return await _initializeResponse.Task.ConfigureAwait(false);
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class ThreadListTimeoutJsonRpcClient : IJsonRpcClient
    {
        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public Task<JsonElement> SendRequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return method switch
            {
                "initialize" => Task.FromResult(ParseElement("{}")),
                "account/rateLimits/read" => Task.FromResult(ParseElement("{}")),
                "account/usage/read" => Task.FromResult(ParseElement("{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{\"lifetimeTokens\":2500}}")),
                "thread/loaded/list" => throw new TaskCanceledException("Loaded thread probe timed out."),
                _ => throw new InvalidOperationException("Unexpected request: " + method)
            };
        }

        public Task SendNotificationAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static JsonElement ParseElement(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
