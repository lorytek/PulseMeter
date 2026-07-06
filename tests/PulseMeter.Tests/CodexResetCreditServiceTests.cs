using System.Text.Json;
using PulseMeter.Slices.UsageCollection;
using System.Net;

namespace PulseMeter.Tests;

public sealed class CodexResetCreditServiceTests
{
    [Fact]
    public void ParseResponse_ReadsAvailableCreditExpiriesAndFiltersUsedCredits()
    {
        using var document = JsonDocument.Parse("""
            {
              "available_count": 2,
              "total_earned_count": 4,
              "credits": [
                {
                  "status": "redeemed",
                  "granted_at": "2026-06-17T17:38:38Z",
                  "expires_at": "2026-07-17T17:38:38Z"
                },
                {
                  "status": "available",
                  "granted_at": "2026-06-20T08:00:00Z",
                  "expires_at": "2026-07-20T08:00:00Z"
                },
                {
                  "status": "available",
                  "granted_at": "2026-06-18T09:30:00Z",
                  "expires_at": "2026-07-18T09:30:00Z"
                },
                {
                  "status": "expired",
                  "granted_at": "2026-05-01T09:30:00Z",
                  "expires_at": "2026-06-01T09:30:00Z"
                }
              ]
            }
            """);

        var result = CodexResetCreditService.ParseResponse(document.RootElement);

        Assert.Equal(2, result.AvailableCount);
        Assert.Equal(2, result.Credits.Count);
        Assert.Equal(new DateTimeOffset(2026, 7, 18, 9, 30, 0, TimeSpan.Zero), result.Credits[0].ExpiresAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero), result.Credits[1].ExpiresAtUtc);
    }

    [Fact]
    public void ParseResponse_UsesCamelCaseFallbackAndEpochSeconds()
    {
        using var document = JsonDocument.Parse("""
            {
              "availableCount": "1",
              "credits": [
                {
                  "status": "available",
                  "grantedAt": 1782910800,
                  "expiresAt": 1785502800
                }
              ]
            }
            """);

        var result = CodexResetCreditService.ParseResponse(document.RootElement);

        Assert.Equal(1, result.AvailableCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1785502800), Assert.Single(result.Credits).ExpiresAtUtc);
    }

    [Fact]
    public async Task TryFetchAsync_UsesLocalAuthForOpenAiResetCreditRequest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var authPath = Path.Combine(directory, "auth.json");
        var accessTokenPropertyName = string.Concat("access_", "token");
        var testAccessToken = string.Concat("test-", "access-", "token");
        await File.WriteAllTextAsync(authPath, $$"""
            {
              "tokens": {
                "{{accessTokenPropertyName}}": "{{testAccessToken}}",
                "account_id": "acct-test"
              }
            }
            """);
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "available_count": 1,
                  "credits": [
                    {
                      "status": "available",
                      "granted_at": "2026-06-18T09:30:00Z",
                      "expires_at": "2026-07-18T09:30:00Z"
                    }
                  ]
                }
                """)
        });
        var service = new CodexResetCreditService(authPath, handler);

        var result = await service.TryFetchAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result.AvailableCount);
        Assert.Equal("Bearer", handler.Request?.Headers.Authorization?.Scheme);
        Assert.Equal(testAccessToken, handler.Request?.Headers.Authorization?.Parameter);
        Assert.NotNull(handler.Request);
        var hasAccountHeader = handler.Request.Headers.TryGetValues("ChatGPT-Account-ID", out var accountValues);
        Assert.True(hasAccountHeader);
        Assert.Equal("acct-test", Assert.Single(accountValues!));
        Assert.Equal("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits", handler.Request?.RequestUri?.ToString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_response);
        }
    }
}
