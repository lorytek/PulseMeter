using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Slices.UsageCollection.Business;

public enum ClaudeUsageFetchStatus
{
    Success,
    NotSignedIn,
    SignInExpired,
    RateLimited,
    Failed
}

public sealed record ClaudeUsageFetchResult(ClaudeUsageFetchStatus Status, JsonElement Payload = default)
{
    public static ClaudeUsageFetchResult From(ClaudeUsageFetchStatus status) => new(status);
}

public interface IClaudeUsageApiClient
{
    Task<ClaudeUsageFetchResult> FetchAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Locates the local Claude Code configuration folder.
/// </summary>
public static class ClaudeHomeLocator
{
    public static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude");
    }
}

/// <summary>
/// Reads the Claude Code subscription usage (5-hour and weekly utilization) using the
/// sign-in that Claude Code already stored locally. PulseMeter only reads the access
/// token; it never refreshes, writes, or uploads credentials anywhere else.
/// </summary>
public sealed class ClaudeUsageApiClient : IClaudeUsageApiClient
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    private const string OAuthBetaHeader = "oauth-2025-04-20";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly string ClientVersion =
        typeof(ClaudeUsageApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly string _credentialsPath;
    private readonly HttpMessageHandler? _handler;
    private readonly Func<DateTimeOffset> _clock;

    public ClaudeUsageApiClient(
        string? claudeHome = null,
        HttpMessageHandler? handler = null,
        Func<DateTimeOffset>? clock = null)
    {
        _credentialsPath = Path.Combine(claudeHome ?? ClaudeHomeLocator.Resolve(), ".credentials.json");
        _handler = handler;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<ClaudeUsageFetchResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        ClaudeAccessToken? token;
        try
        {
            token = await LoadAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            PrivacySafeDiagnostics.WriteFailure("claude credentials could not be read", ex);
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.NotSignedIn);
        }

        if (token is null)
        {
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.NotSignedIn);
        }

        if (token.ExpiresAtUtc is DateTimeOffset expiresAt && expiresAt <= _clock())
        {
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.SignInExpired);
        }

        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("anthropic-beta", OAuthBetaHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", $"PulseMeter/{ClientVersion}");

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.SignInExpired);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.RateLimited);
            }

            if (!response.IsSuccessStatusCode)
            {
                PrivacySafeDiagnostics.WriteInfo($"claude usage request returned HTTP {(int)response.StatusCode}");
                return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.Failed);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            PrivacySafeDiagnostics.WriteInfo("claude usage response received");
            return new ClaudeUsageFetchResult(ClaudeUsageFetchStatus.Success, document.RootElement.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            PrivacySafeDiagnostics.WriteFailure("claude usage request failed", ex);
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.Failed);
        }
    }

    private async Task<ClaudeAccessToken?> LoadAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_credentialsPath))
        {
            return null;
        }

        await using var stream = new FileStream(
            _credentialsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseAccessToken(document.RootElement);
    }

    internal static ClaudeAccessToken? ParseAccessToken(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("claudeAiOauth", out var oauth)
            || oauth.ValueKind != JsonValueKind.Object
            || !oauth.TryGetProperty("accessToken", out var accessToken)
            || accessToken.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(accessToken.GetString()))
        {
            return null;
        }

        DateTimeOffset? expiresAt = null;
        if (oauth.TryGetProperty("expiresAt", out var expires)
            && expires.ValueKind == JsonValueKind.Number
            && expires.TryGetInt64(out var expiresMs)
            && expiresMs > 0)
        {
            expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs);
        }

        return new ClaudeAccessToken(accessToken.GetString()!, expiresAt);
    }

    private HttpClient CreateHttpClient()
    {
        var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = RequestTimeout;
        return client;
    }

    internal sealed record ClaudeAccessToken(string Value, DateTimeOffset? ExpiresAtUtc);
}
