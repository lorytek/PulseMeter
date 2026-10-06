using System.Net;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class ClaudeUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private const string UsagePayload = """
        {
          "five_hour": { "utilization": 42.5, "resets_at": "2026-10-04T14:30:00.123+00:00" },
          "seven_day": { "utilization": 61, "resets_at": "2026-10-09T08:00:00+00:00" },
          "seven_day_opus": { "utilization": 100.0, "resets_at": "2026-10-09T08:00:00+00:00" },
          "seven_day_oauth_apps": null,
          "extra_usage": { "is_enabled": false }
        }
        """;

    [Fact]
    public void Parser_MapsFiveHourAndWeeklyWindowsToGeneralLimit()
    {
        using var document = JsonDocument.Parse(UsagePayload);

        var buckets = ClaudeUsageParser.ParseRateLimitBuckets(document.RootElement, Now);

        Assert.Equal(3, buckets.Count);
        var fiveHour = buckets[0];
        Assert.Equal(ClaudeUsageParser.GeneralLimitId, fiveHour.LimitId);
        Assert.Equal("General", fiveHour.GroupLabel);
        Assert.Equal(300, fiveHour.WindowDurationMins);
        Assert.Equal(42.5, fiveHour.UsedPercent);
        Assert.Equal("5h", fiveHour.WindowLabel);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 14, 30, 0, 123, TimeSpan.Zero), fiveHour.ResetsAtUtc);
        Assert.Equal("2h 30m", fiveHour.ResetCountdown);
        Assert.False(fiveHour.IsReached);

        var weekly = buckets[1];
        Assert.Equal(ClaudeUsageParser.GeneralLimitId, weekly.LimitId);
        Assert.Equal(10_080, weekly.WindowDurationMins);

        var opus = buckets[2];
        Assert.Equal("claude_opus", opus.LimitId);
        Assert.Equal("Opus", opus.GroupLabel);
        Assert.True(opus.IsReached);
    }

    [Fact]
    public void Parser_IgnoresUnknownOrMalformedWindows()
    {
        using var document = JsonDocument.Parse("""
            { "five_hour": { "resets_at": null }, "mystery": { "utilization": 5 }, "seven_day": "bad" }
            """);

        Assert.Empty(ClaudeUsageParser.ParseRateLimitBuckets(document.RootElement, Now));
    }

    [Fact]
    public void LocalSource_DeduplicatesStreamedMessagesAndBuildsDailyTotals()
    {
        var lines = new[]
        {
            """{"type":"summary","summary":"ignored"}""",
            AssistantLine("2026-10-04T09:00:00Z", "msg_1", "req_1", input: 10, output: 20, cacheCreate: 30, cacheRead: 40),
            // Same message repeated for a second content block must not be counted twice.
            AssistantLine("2026-10-04T09:00:01Z", "msg_1", "req_1", input: 10, output: 20, cacheCreate: 30, cacheRead: 40),
            AssistantLine("2026-10-03T09:00:00Z", "msg_2", "req_2", input: 5, output: 5, cacheCreate: 0, cacheRead: 0),
            """{"type":"assistant","message":{"usage":{"input_tokens":"""
        };

        var file = ClaudeLocalUsageSource.ParseSessionLines("fallback", lines);
        var usage = ClaudeLocalUsageSource.Aggregate([file], Now);

        Assert.Equal("session-a", file.SessionId);
        Assert.Equal(@"C:\work\pulse", file.Cwd);
        Assert.Equal(110, usage.LifetimeTokens);
        Assert.Equal(2, usage.DailyBuckets.Count);
        var expectedLatestDate = DateTimeOffset.Parse("2026-10-04T09:00:00Z").ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expectedLatestDate, usage.DailyBuckets[^1].StartDate);
        Assert.Equal(100, usage.DailyBuckets[^1].TotalTokens);
        Assert.Equal(80, usage.DailyBuckets[^1].InputTokens);
        Assert.Equal(100, usage.PeakDailyTokens);
        Assert.Equal(2, usage.CurrentStreakDays);
        Assert.Equal(2, usage.LongestStreakDays);
        Assert.Equal(ActivityEvidenceCoverage.Available, usage.ActivityEvidence.Coverage);
        Assert.Equal(2, usage.ActivityEvidence.UtcHours.Count);

        var session = Assert.Single(usage.SessionSummaries);
        Assert.Equal("session-a", session.ThreadId);
        Assert.Equal(2, session.TokenSummaries.Count);
        Assert.Equal(string.Empty, session.Title);
        Assert.Equal("session-a", usage.RecentSession?.ThreadId);
    }

    [Fact]
    public async Task LocalSource_ReadsProjectsFolderAndReturnsEmptyWhenMissing()
    {
        var home = CreateTempDirectory();
        try
        {
            var missing = await new ClaudeLocalUsageSource(home).ReadAsync(Now);
            Assert.Same(ClaudeLocalUsage.Empty, missing);

            var projectDir = Directory.CreateDirectory(Path.Combine(home, "projects", "C--work-pulse")).FullName;
            await File.WriteAllLinesAsync(
                Path.Combine(projectDir, "session-a.jsonl"),
                [AssistantLine("2026-10-04T09:00:00Z", "msg_1", "req_1", 1, 2, 3, 4)]);

            var source = new ClaudeLocalUsageSource(home);
            var usage = await source.ReadAsync(Now);

            Assert.Equal(10, usage.LifetimeTokens);
            Assert.Single(usage.SessionSummaries);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void ApiClient_ParsesAccessTokenAndExpiry()
    {
        using var document = JsonDocument.Parse("""
            { "claudeAiOauth": { "accessToken": "token-value", "expiresAt": 1791000000000 } }
            """);

        var token = ClaudeCredentialLocator.ParseAccessToken(document.RootElement);

        Assert.NotNull(token);
        Assert.Equal("token-value", token.Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1791000000000), token.ExpiresAtUtc);
        using var empty = JsonDocument.Parse("{}");
        Assert.Null(ClaudeCredentialLocator.ParseAccessToken(empty.RootElement));
    }

    [Fact]
    public async Task ApiClient_ReportsNotSignedInWithoutCredentials()
    {
        var home = CreateTempDirectory();
        try
        {
            var handler = new StubHandler(HttpStatusCode.OK, UsagePayload);
            var result = await new ClaudeUsageApiClient(home, handler).FetchAsync();

            Assert.Equal(ClaudeUsageFetchStatus.NotSignedIn, result.Status);
            Assert.Equal(0, handler.CallCount);
            Assert.Contains(Path.Combine(home, ".credentials.json"), result.Detail);
            Assert.Contains("file not found", result.Detail);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, ClaudeUsageFetchStatus.Success)]
    [InlineData(HttpStatusCode.Unauthorized, ClaudeUsageFetchStatus.SignInExpired)]
    [InlineData(HttpStatusCode.TooManyRequests, ClaudeUsageFetchStatus.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ClaudeUsageFetchStatus.Failed)]
    public async Task ApiClient_SendsBearerTokenAndMapsStatus(HttpStatusCode statusCode, ClaudeUsageFetchStatus expected)
    {
        var home = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(home, ".credentials.json"),
                """{ "claudeAiOauth": { "accessToken": "token-value", "expiresAt": 4102444800000 } }""");
            var handler = new StubHandler(statusCode, UsagePayload);

            var result = await new ClaudeUsageApiClient(home, handler, () => Now).FetchAsync();

            Assert.Equal(expected, result.Status);
            Assert.Equal("Bearer token-value", handler.LastAuthorization);
            Assert.Equal("oauth-2025-04-20", handler.LastBetaHeader);
            if (expected == ClaudeUsageFetchStatus.Success)
            {
                Assert.Equal(JsonValueKind.Object, result.Payload.ValueKind);
            }
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task ApiClient_DoesNotSendExpiredToken()
    {
        var home = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(home, ".credentials.json"),
                """{ "claudeAiOauth": { "accessToken": "token-value", "expiresAt": 1000 } }""");
            var handler = new StubHandler(HttpStatusCode.OK, UsagePayload);

            var result = await new ClaudeUsageApiClient(home, handler, () => Now).FetchAsync();

            Assert.Equal(ClaudeUsageFetchStatus.SignInExpired, result.Status);
            Assert.Equal(0, handler.CallCount);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task Service_ReturnsLiveSnapshotThenFallsBackToLastConfirmedLimits()
    {
        using var document = JsonDocument.Parse(UsagePayload);
        var api = new StubApiClient(new ClaudeUsageFetchResult(ClaudeUsageFetchStatus.Success, document.RootElement.Clone()));
        var service = CreateService(api);
        UsageSnapshot? published = null;
        service.SnapshotUpdated += (_, snapshot) => published = snapshot;

        var live = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Live, live.SyncStatus);
        Assert.Equal("Claude", live.Source);
        Assert.Equal(3, live.Buckets.Count);
        Assert.Null(live.StatusMessage);
        Assert.Same(live, published);

        api.Result = ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.SignInExpired);
        var stale = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Stale, stale.SyncStatus);
        Assert.Equal(3, stale.Buckets.Count);
        Assert.Contains("expired", stale.StatusMessage);
        Assert.Contains("last confirmed", stale.StatusMessage);
    }

    [Fact]
    public void CandidateHomes_ListsConfigDirThenProfileFoldersWithoutDuplicates()
    {
        var env = new Dictionary<string, string?>
        {
            ["CLAUDE_CONFIG_DIR"] = @"D:\cfg\claude",
            ["USERPROFILE"] = @"C:\Users\laur",
            ["HOME"] = @"C:\Users\laur",
            ["HOMEDRIVE"] = "C:",
            ["HOMEPATH"] = @"\Users\laur"
        };

        var homes = ClaudeHomeLocator.CandidateHomes(name => env.GetValueOrDefault(name));

        Assert.Equal(@"D:\cfg\claude", homes[0]);
        Assert.Equal(Path.Combine(@"C:\Users\laur", ".claude"), homes[1]);
        Assert.Equal(homes.Count, homes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(ClaudeHomeLocator.CandidateHomes(_ => null));
    }

    [Fact]
    public void CredentialLocator_FallsThroughToLaterFolderAndReportsEachProbe()
    {
        var empty = CreateTempDirectory();
        var wrongShape = CreateTempDirectory();
        var good = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(wrongShape, ".credentials.json"), """{ "mcpOAuth": {} }""");
            File.WriteAllText(
                Path.Combine(good, ".credentials.json"),
                """{ "claudeAiOauth": { "accessToken": "token-value" } }""");

            var lookup = ClaudeCredentialLocator.Find([empty, wrongShape, good]);

            Assert.Equal("token-value", lookup.Token?.Value);
            Assert.Equal(
                [ClaudeCredentialFileState.Missing, ClaudeCredentialFileState.NoAccessToken, ClaudeCredentialFileState.Found],
                lookup.Probes.Select(probe => probe.State).ToArray());

            var notFound = ClaudeCredentialLocator.Find([empty, wrongShape]);
            Assert.Null(notFound.Token);
            Assert.Contains("file not found", notFound.Describe());
            Assert.Contains("no access token inside", notFound.Describe());
            Assert.DoesNotContain("token-value", notFound.Describe());
        }
        finally
        {
            foreach (var directory in new[] { empty, wrongShape, good })
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void CredentialLocator_MarksMalformedJsonAsUnreadableAndKeepsLooking()
    {
        var broken = CreateTempDirectory();
        var good = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(broken, ".credentials.json"), "{ not json");
            File.WriteAllText(Path.Combine(good, ".credentials.json"), """{ "accessToken": "flat-token" }""");

            var lookup = ClaudeCredentialLocator.Find([broken, good]);

            Assert.Equal("flat-token", lookup.Token?.Value);
            Assert.Equal(ClaudeCredentialFileState.Unreadable, lookup.Probes[0].State);
        }
        finally
        {
            Directory.Delete(broken, recursive: true);
            Directory.Delete(good, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "a", "expiresAt": 4102444800000 } }""", "a")]
    [InlineData("""{ "accessToken": "b" }""", "b")]
    [InlineData("""{ "access_token": "c", "expires_at": 4102444800 }""", "c")]
    [InlineData("""{ "oauth": { "access_token": "d" } }""", "d")]
    [InlineData("""{ "claudeAiOauth": { "accessToken": "  " } }""", null)]
    [InlineData("""[]""", null)]
    public void CredentialLocator_ParsesWrappedAndFlatTokenShapes(string json, string? expected)
    {
        using var document = JsonDocument.Parse(json);

        var token = ClaudeCredentialLocator.ParseAccessToken(document.RootElement);

        Assert.Equal(expected, token?.Value);
    }

    [Fact]
    public async Task Service_StatusMessageNamesTheCheckedPaths()
    {
        var detail = @"C:\Users\laur\.claude\.credentials.json (file not found)";
        var service = CreateService(new StubApiClient(
            ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.NotSignedIn, detail)));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Contains(detail, snapshot.StatusMessage);
    }

    [Fact]
    public async Task Service_IsUnavailableWhenNeverSignedIn()
    {
        var service = CreateService(new StubApiClient(ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.NotSignedIn)));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(SyncStatus.Unavailable, snapshot.SyncStatus);
        Assert.Empty(snapshot.Buckets);
        Assert.Contains("sign-in was not found", snapshot.StatusMessage);
    }

    [Fact]
    public async Task Router_UsesSelectedProviderAndIgnoresUpdatesFromInactiveProvider()
    {
        var codex = new StubUsageService("Codex");
        var claude = new StubUsageService("Claude");
        var router = new UsageProviderRouter(codex, claude);
        var received = new List<string>();
        router.SnapshotUpdated += (_, snapshot) => received.Add(snapshot.Source);

        Assert.Equal("Codex", (await router.GetSnapshotAsync()).Source);

        router.Provider = UsageProvider.Claude;
        Assert.Equal("Claude", (await router.GetSnapshotAsync()).Source);

        codex.Raise();
        claude.Raise();
        Assert.Equal(new[] { "Claude" }, received);

        router.UseMockMode = true;
        Assert.True(codex.UseMockMode);
        Assert.True(claude.UseMockMode);
    }

    [Theory]
    [InlineData(null, UsageProvider.Codex)]
    [InlineData("claude", UsageProvider.Claude)]
    [InlineData("Codex", UsageProvider.Codex)]
    [InlineData("42", UsageProvider.Codex)]
    [InlineData("something-else", UsageProvider.Codex)]
    public void ProviderNames_ParseFallsBackToCodex(string? value, UsageProvider expected)
    {
        Assert.Equal(expected, UsageProviderNames.Parse(value));
    }

    private static ClaudeUsageService CreateService(IClaudeUsageApiClient api) =>
        new(
            new MockCodexUsageService(),
            api,
            new StubLocalSource(),
            new ProjectUsageService(CreateTempDirectory()),
            new UsageAttributionService(CreateTempDirectory()),
            () => Now);

    private static string AssistantLine(
        string timestamp,
        string messageId,
        string requestId,
        long input,
        long output,
        long cacheCreate,
        long cacheRead) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["cwd"] = @"C:\work\pulse",
            ["sessionId"] = "session-a",
            ["requestId"] = requestId,
            ["timestamp"] = timestamp,
            ["message"] = new Dictionary<string, object>
            {
                ["id"] = messageId,
                ["role"] = "assistant",
                ["content"] = new[] { new { type = "text", text = "not retained" } },
                ["usage"] = new Dictionary<string, long>
                {
                    ["input_tokens"] = input,
                    ["output_tokens"] = output,
                    ["cache_creation_input_tokens"] = cacheCreate,
                    ["cache_read_input_tokens"] = cacheRead
                }
            }
        });

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public string? LastAuthorization { get; private set; }

        public string? LastBetaHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastBetaHeader = request.Headers.TryGetValues("anthropic-beta", out var values) ? values.Single() : null;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class StubApiClient(ClaudeUsageFetchResult result) : IClaudeUsageApiClient
    {
        public ClaudeUsageFetchResult Result { get; set; } = result;

        public Task<ClaudeUsageFetchResult> FetchAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }

    private sealed class StubLocalSource : IClaudeLocalUsageSource
    {
        public Task<ClaudeLocalUsage> ReadAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(ClaudeLocalUsage.Empty);
    }

    private sealed class StubUsageService(string source) : IUsageService
    {
        public event EventHandler<UsageSnapshot>? SnapshotUpdated;

        public bool UseMockMode { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new UsageSnapshot { Source = source });

        public void Raise() => SnapshotUpdated?.Invoke(this, new UsageSnapshot { Source = source });
    }
}
