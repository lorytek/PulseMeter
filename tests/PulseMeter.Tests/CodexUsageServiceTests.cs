using PulseMeter.Platform.Codex;
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
        Assert.Contains("The monitored app is not running", fallback.StatusMessage);
        Assert.DoesNotContain("mock", fallback.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSnapshotAsync_MergesUsageAttributionAfterAccountUsage()
    {
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
        await using var service = CreateService(usageAttributionService: attribution, jsonRpcClientFactory: new StubJsonRpcClientFactory(new StubJsonRpcClient(
            ("initialize", "{}"),
            ("account/rateLimits/read", "{}"),
            ("account/usage/read", "{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-07\",\"tokens\":1000}],\"summary\":{}}"),
            ("thread/loaded/list", "[]"))));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Same(attribution.Snapshot, snapshot.UsageAttribution);
        Assert.Single(snapshot.UsageAttribution.Sessions);
        Assert.Equal(1, attribution.CallCount);
        Assert.Equal(1_000, Assert.Single(attribution.DailyBuckets).TotalTokens);
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
        IUsageAttributionService? usageAttributionService = null,
        IAppServerProcessFactory? processFactory = null,
        IJsonRpcClientFactory? jsonRpcClientFactory = null)
    {
        return new CodexUsageService(
            new StubMockUsageService(),
            resetCreditService ?? new StubResetCreditService(),
            new StubProjectUsageService(),
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

    private sealed class StubJsonRpcClient : IJsonRpcClient
    {
        private readonly Dictionary<string, JsonElement> _responses;

        public StubJsonRpcClient(params (string Method, string Json)[] responses)
        {
            _responses = responses.ToDictionary(item => item.Method, item => ParseElement(item.Json), StringComparer.Ordinal);
        }

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

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

    private sealed class SequentialRateLimitJsonRpcClient(params string[] rateLimitResponses) : IJsonRpcClient
    {
        private readonly Queue<JsonElement> _rateLimitResponses = new(rateLimitResponses.Select(ParseElement));

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

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

        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

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
        public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

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
