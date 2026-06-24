using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Amplify.Core.Auth;

/// <summary>
/// Talks to Spotify's Accounts service for the PKCE token exchange and refresh, and reads the
/// current user's profile from the Web API. UI-free and built by hand (no third-party SDK) so the
/// request/response mapping and the rate-limit backoff are unit-testable with a fake message
/// handler. Uses absolute URLs so a single <see cref="HttpClient"/> serves both the Accounts host
/// and the Web API host.
/// </summary>
public sealed class SpotifyTokenClient
{
    // One initial attempt plus up to three retries when Spotify replies 429 (rate limited).
    private const int _maxAttempts = 4;

    // Ceiling for the exponential fallback used when a 429 carries no Retry-After header.
    private static readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(8);

    private readonly HttpClient _http;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>Creates a client that waits real time between rate-limit retries.</summary>
    public SpotifyTokenClient(HttpClient http) : this(http, Task.Delay) { }

    // Test seam: lets unit tests substitute an instant, recordable delay so the backoff path runs
    // deterministically without real waiting.
    internal SpotifyTokenClient(HttpClient http, Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _http = http;
        _delayAsync = delayAsync;
    }

    /// <summary>Exchanges an authorization code for the initial token set.</summary>
    public Task<TokenSet> ExchangeCodeAsync(
        string clientId, string redirectUri, string code, string verifier, CancellationToken ct = default) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
        }, ct);

    /// <summary>
    /// Exchanges a refresh token for a fresh access token. Spotify may rotate the refresh token; when
    /// it does the new value is surfaced on <see cref="TokenSet.RefreshToken"/> for the caller to
    /// persist, otherwise that property is <c>null</c> and the existing token stays valid.
    /// </summary>
    public Task<TokenSet> RefreshAsync(string clientId, string refreshToken, CancellationToken ct = default) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        }, ct);

    /// <summary>
    /// Reads the connected account's display name from <c>GET /v1/me</c> for the account card.
    /// Subscription level is no longer exposed by the Web API, so none is read.
    /// </summary>
    public async Task<Account> GetAccountAsync(string accessToken, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await SendWithBackoffAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, SpotifyOAuthConstants.CurrentUserEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        UserProfile? profile = await response.Content.ReadFromJsonAsync<UserProfile>(ct).ConfigureAwait(false);
        string displayName = string.IsNullOrWhiteSpace(profile?.DisplayName) ? "Spotify user" : profile.DisplayName;
        return new Account(displayName, Initials(displayName));
    }

    private async Task<TokenSet> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendWithBackoffAsync(
            () => new HttpRequestMessage(HttpMethod.Post, SpotifyOAuthConstants.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            }, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        TokenResponse? tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ct).ConfigureAwait(false);
        if (tokens?.AccessToken is null)
        {
            throw new HttpRequestException("Spotify's token response did not include an access token.");
        }

        return new TokenSet(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
    }

    // Sends a freshly built request, retrying on 429 with the server's Retry-After (or an
    // exponential fallback) until it succeeds, fails for another reason, or exhausts the attempts.
    private async Task<HttpResponseMessage> SendWithBackoffAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            using HttpRequestMessage request = requestFactory();
            HttpResponseMessage response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _maxAttempts)
            {
                return response;
            }

            TimeSpan delay = RetryDelay(response.Headers.RetryAfter, attempt);
            response.Dispose();
            await _delayAsync(delay, ct).ConfigureAwait(false);
        }
    }

    private static TimeSpan RetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan until = date - DateTimeOffset.UtcNow;
            return until > TimeSpan.Zero ? until : TimeSpan.Zero;
        }

        // Exponential fallback: 1s, 2s, 4s … capped.
        double seconds = Math.Min(_maxBackoff.TotalSeconds, Math.Pow(2, attempt - 1));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Initials(string displayName)
    {
        string[] parts = displayName.Split(
            ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
        };
    }

    // Minimal projections of the Accounts/Web API JSON — only the fields Amplify reads.
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record UserProfile(
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
