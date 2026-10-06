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

public sealed record ClaudeUsageFetchResult(
    ClaudeUsageFetchStatus Status,
    JsonElement Payload = default,
    string? Detail = null)
{
    public static ClaudeUsageFetchResult From(ClaudeUsageFetchStatus status, string? detail = null) =>
        new(status, default, detail);
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
    /// <summary>
    /// Every folder Claude Code may keep its sign-in in, most specific first:
    /// <c>CLAUDE_CONFIG_DIR</c>, then <c>.claude</c> under the Windows profile, the
    /// <c>HOME</c> folder, and the <c>HOMEDRIVE</c>/<c>HOMEPATH</c> profile.
    /// </summary>
    public static IReadOnlyList<string> CandidateHomes(Func<string, string?>? getEnvironmentVariable = null)
    {
        var env = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        var homes = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var normalized = path.Trim().Trim('"');
            if (!homes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                homes.Add(normalized);
            }
        }

        Add(env("CLAUDE_CONFIG_DIR"));
        AddProfile(env("USERPROFILE"));
        AddProfile(env("HOME"));
        var homeDrive = env("HOMEDRIVE");
        var homePath = env("HOMEPATH");
        if (!string.IsNullOrWhiteSpace(homeDrive) && !string.IsNullOrWhiteSpace(homePath))
        {
            AddProfile(homeDrive + homePath);
        }

        if (getEnvironmentVariable is null)
        {
            AddProfile(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        return homes;

        void AddProfile(string? profile)
        {
            if (!string.IsNullOrWhiteSpace(profile))
            {
                Add(Path.Combine(profile.Trim().Trim('"'), ".claude"));
            }
        }
    }
}

public enum ClaudeCredentialFileState
{
    Missing,
    Unreadable,
    NoAccessToken,
    Found
}

public sealed record ClaudeCredentialProbe(string Path, ClaudeCredentialFileState State);

public sealed record ClaudeAccessToken(string Value, DateTimeOffset? ExpiresAtUtc);

public sealed record ClaudeCredentialLookup(
    ClaudeAccessToken? Token,
    IReadOnlyList<ClaudeCredentialProbe> Probes)
{
    /// <summary>One line naming each path that was checked and what was found there.</summary>
    public string Describe()
    {
        return string.Join("; ", Probes.Select(probe => $"{probe.Path} ({probe.State switch
        {
            ClaudeCredentialFileState.Missing => "file not found",
            ClaudeCredentialFileState.Unreadable => "could not be read",
            ClaudeCredentialFileState.NoAccessToken => "no access token inside",
            _ => "ok"
        }})"));
    }
}

/// <summary>
/// Finds the Claude Code access token. It checks each candidate folder in turn and
/// accepts the wrapped (<c>claudeAiOauth</c>) and the flat token layouts. Only the
/// access token is read.
/// </summary>
public static class ClaudeCredentialLocator
{
    public const string FileName = ".credentials.json";

    public static ClaudeCredentialLookup Find(IEnumerable<string> homes)
    {
        var probes = new List<ClaudeCredentialProbe>();
        foreach (var home in homes)
        {
            var path = Path.Combine(home, FileName);
            if (!File.Exists(path))
            {
                probes.Add(new ClaudeCredentialProbe(path, ClaudeCredentialFileState.Missing));
                continue;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);
                var token = ParseAccessToken(document.RootElement);
                if (token is not null)
                {
                    probes.Add(new ClaudeCredentialProbe(path, ClaudeCredentialFileState.Found));
                    return new ClaudeCredentialLookup(token, probes);
                }

                probes.Add(new ClaudeCredentialProbe(path, ClaudeCredentialFileState.NoAccessToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                PrivacySafeDiagnostics.WriteFailure("claude credentials could not be read", ex);
                probes.Add(new ClaudeCredentialProbe(path, ClaudeCredentialFileState.Unreadable));
            }
        }

        return new ClaudeCredentialLookup(null, probes);
    }

    public static ClaudeAccessToken? ParseAccessToken(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Wrapped layouts first, then the token object being the document itself.
        foreach (var wrapper in new[] { "claudeAiOauth", "oauth", "claude_ai_oauth" })
        {
            if (root.TryGetProperty(wrapper, out var inner)
                && inner.ValueKind == JsonValueKind.Object
                && ReadToken(inner) is { } wrapped)
            {
                return wrapped;
            }
        }

        return ReadToken(root);
    }

    private static ClaudeAccessToken? ReadToken(JsonElement element)
    {
        string? value = null;
        foreach (var name in new[] { "accessToken", "access_token" })
        {
            if (element.TryGetProperty(name, out var property)
                && property.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.GetString()))
            {
                value = property.GetString();
                break;
            }
        }

        if (value is null)
        {
            return null;
        }

        DateTimeOffset? expiresAt = null;
        foreach (var name in new[] { "expiresAt", "expires_at" })
        {
            if (element.TryGetProperty(name, out var expires)
                && expires.ValueKind == JsonValueKind.Number
                && expires.TryGetInt64(out var raw)
                && raw > 0)
            {
                // Claude Code stores milliseconds; tolerate seconds too.
                expiresAt = raw > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
                    : DateTimeOffset.FromUnixTimeSeconds(raw);
                break;
            }
        }

        return new ClaudeAccessToken(value, expiresAt);
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

    private readonly IReadOnlyList<string> _homes;
    private readonly HttpMessageHandler? _handler;
    private readonly Func<DateTimeOffset> _clock;

    public ClaudeUsageApiClient(
        string? claudeHome = null,
        HttpMessageHandler? handler = null,
        Func<DateTimeOffset>? clock = null)
    {
        _homes = claudeHome is null ? ClaudeHomeLocator.CandidateHomes() : [claudeHome];
        _handler = handler;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<ClaudeUsageFetchResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var lookup = await Task.Run(() => ClaudeCredentialLocator.Find(_homes), cancellationToken).ConfigureAwait(false);
        var token = lookup.Token;
        if (token is null)
        {
            return ClaudeUsageFetchResult.From(ClaudeUsageFetchStatus.NotSignedIn, lookup.Describe());
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

    private HttpClient CreateHttpClient()
    {
        var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = RequestTimeout;
        return client;
    }
}
