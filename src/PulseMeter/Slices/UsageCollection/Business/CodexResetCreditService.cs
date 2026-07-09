using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Slices.UsageCollection.Business;

public interface ICodexResetCreditService
{
    Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexResetCreditService : ICodexResetCreditService
{
    private const string ResetCreditsUrl = "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly string _authPath;
    private readonly HttpMessageHandler? _handler;

    public CodexResetCreditService(string? authPath = null, HttpMessageHandler? handler = null)
    {
        _authPath = authPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "auth.json");
        _handler = handler;
    }

    public async Task<ResetCreditFetchResult?> TryFetchAsync(CancellationToken cancellationToken = default)
    {
        CodexAuthSession auth;
        try
        {
            auth = LoadAuthSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }

        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ResetCreditsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("OpenAI-Beta", "codex-1");
        request.Headers.TryAddWithoutValidation("originator", "Codex Desktop");

        if (!string.IsNullOrWhiteSpace(auth.AccountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-ID", auth.AccountId);
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ParseResponse(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    public static ResetCreditFetchResult ParseResponse(JsonElement payload)
    {
        var credits = ReadArray(payload, "credits")
            .Select(ParseCredit)
            .Where(credit => IsAvailableStatus(credit.Status))
            .OrderBy(credit => credit.ExpiresAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(credit => credit.GrantedAtUtc ?? DateTimeOffset.MaxValue)
            .ToList();

        var availableCount = ReadInt(payload, "available_count")
            ?? ReadInt(payload, "availableCount")
            ?? credits.Count;

        return new ResetCreditFetchResult(Math.Max(0, availableCount), credits);
    }

    private HttpClient CreateHttpClient()
    {
        var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);

        client.Timeout = RequestTimeout;
        return client;
    }

    private CodexAuthSession LoadAuthSession()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(_authPath));
        var root = document.RootElement;
        var tokens = root.TryGetProperty("tokens", out var tokensProperty)
            && tokensProperty.ValueKind == JsonValueKind.Object
                ? tokensProperty
                : root;

        var accessToken = ReadString(tokens, "access_token") ?? ReadString(tokens, "accessToken");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Auth session does not contain an access token.");
        }

        return new CodexAuthSession(
            accessToken,
            ReadString(tokens, "account_id") ?? ReadString(tokens, "accountId"));
    }

    private static ResetCreditSnapshot ParseCredit(JsonElement credit)
    {
        return new ResetCreditSnapshot(
            ReadDateTimeOffset(credit, "granted_at") ?? ReadDateTimeOffset(credit, "grantedAt"),
            ReadDateTimeOffset(credit, "expires_at") ?? ReadDateTimeOffset(credit, "expiresAt"),
            ReadString(credit, "status"));
    }

    private static bool IsAvailableStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            || status.Equals("available", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                yield return item;
            }
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString();
        if (long.TryParse(text, out var parsedUnixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(parsedUnixSeconds);
        }

        return DateTimeOffset.TryParse(text, out var parsedDate) ? parsedDate.ToUniversalTime() : null;
    }

    private sealed record CodexAuthSession(string AccessToken, string? AccountId);
}

public sealed record ResetCreditFetchResult(
    int AvailableCount,
    IReadOnlyList<ResetCreditSnapshot> Credits);
