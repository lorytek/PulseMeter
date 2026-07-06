using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Tests;

public sealed class RateLimitParserTests
{
    [Fact]
    public void ParseRateLimits_UsesRateLimitsByLimitIdWhenPresent()
    {
        using var document = JsonDocument.Parse("""
            {
              "rateLimits": {
                "limitId": "legacy",
                "primary": { "usedPercent": 10, "windowDurationMins": 15, "resetsAt": 1730000100 }
              },
              "rateLimitsByLimitId": {
                "codex": {
                  "limitId": "codex",
                  "limitName": "Codex",
                  "primary": { "usedPercent": 25, "windowDurationMins": 300, "resetsAt": 1730007200 },
                  "rateLimitReachedType": null
                },
                "weekly": {
                  "limitId": "weekly",
                  "limitName": null,
                  "primary": { "usedPercent": 50, "windowDurationMins": 10080, "resetsAt": 1730520000 }
                }
              },
              "rateLimitResetCredits": { "availableCount": 2 }
            }
            """);

        var snapshot = CodexUsageParser.ParseRateLimits(document.RootElement, DateTimeOffset.FromUnixTimeSeconds(1_730_000_000), "AppServer");

        Assert.Equal(2, snapshot.Buckets.Count);
        Assert.Equal("Codex", snapshot.Buckets[0].Label);
        Assert.Equal(25, snapshot.Buckets[0].UsedPercent);
        Assert.Equal("2h 00m", snapshot.Buckets[0].ResetCountdown);
        Assert.Equal("7d", snapshot.Buckets[1].Label);
        Assert.Equal(2, snapshot.ResetCreditsAvailable);
    }

    [Fact]
    public void ParseRateLimits_ReadsResetCreditExpiryWhenPresent()
    {
        using var document = JsonDocument.Parse("""
            {
              "rateLimits": {
                "limitId": "codex",
                "primary": { "usedPercent": 10, "windowDurationMins": 300 }
              },
              "rateLimitResetCredits": {
                "availableCount": 2,
                "expiresAt": 1730520000
              }
            }
            """);

        var snapshot = CodexUsageParser.ParseRateLimits(document.RootElement, DateTimeOffset.FromUnixTimeSeconds(1_730_000_000), "AppServer");

        Assert.Equal(2, snapshot.ResetCreditsAvailable);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_730_520_000), snapshot.ResetCreditsExpiresAtUtc);
    }

    [Fact]
    public void ParseRateLimits_FallsBackToSingleRateLimitsObject()
    {
        using var document = JsonDocument.Parse("""
            {
              "rateLimits": {
                "limitId": "codex",
                "primary": { "usedPercent": 61, "windowDurationMins": 60 }
              }
            }
            """);

        var snapshot = CodexUsageParser.ParseRateLimits(document.RootElement, DateTimeOffset.FromUnixTimeSeconds(1_730_000_000), "AppServer");

        var bucket = Assert.Single(snapshot.Buckets);
        Assert.Equal("1h", bucket.Label);
        Assert.Equal("reset unknown", bucket.ResetCountdown);
        Assert.Equal(61, bucket.UsedPercent);
    }

    [Fact]
    public void ParseRateLimits_ExpandsPrimaryAndSecondaryWindowsForEachLimitGroup()
    {
        using var document = JsonDocument.Parse("""
            {
              "rateLimitsByLimitId": {
                "codex": {
                  "limitId": "codex",
                  "limitName": null,
                  "primary": { "usedPercent": 4, "windowDurationMins": 300, "resetsAt": 1730007200 },
                  "secondary": { "usedPercent": 49, "windowDurationMins": 10080, "resetsAt": 1730520000 }
                },
                "codex_bengalfox": {
                  "limitId": "codex_bengalfox",
                  "limitName": "GPT-5.3-Codex-Spark",
                  "primary": { "usedPercent": 0, "windowDurationMins": 300, "resetsAt": 1730007200 },
                  "secondary": { "usedPercent": 0, "windowDurationMins": 10080, "resetsAt": 1730520000 }
                }
              }
            }
            """);

        var snapshot = CodexUsageParser.ParseRateLimits(document.RootElement, DateTimeOffset.FromUnixTimeSeconds(1_730_000_000), "AppServer");

        Assert.Collection(
            snapshot.Buckets,
            bucket =>
            {
                Assert.Equal("codex", bucket.LimitId);
                Assert.Equal("General", bucket.GroupLabel);
                Assert.Equal("5h", bucket.WindowLabel);
                Assert.Equal(4, bucket.UsedPercent);
            },
            bucket =>
            {
                Assert.Equal("codex", bucket.LimitId);
                Assert.Equal("General", bucket.GroupLabel);
                Assert.Equal("7d", bucket.WindowLabel);
                Assert.Equal(49, bucket.UsedPercent);
            },
            bucket =>
            {
                Assert.Equal("codex_bengalfox", bucket.LimitId);
                Assert.Equal("GPT-5.3-Codex-Spark", bucket.GroupLabel);
                Assert.Equal("5h", bucket.WindowLabel);
                Assert.Equal(0, bucket.UsedPercent);
            },
            bucket =>
            {
                Assert.Equal("codex_bengalfox", bucket.LimitId);
                Assert.Equal("GPT-5.3-Codex-Spark", bucket.GroupLabel);
                Assert.Equal("7d", bucket.WindowLabel);
                Assert.Equal(0, bucket.UsedPercent);
            });
    }
}
