using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.System;

namespace Amplify.App.Auth;

/// <summary>
/// Connects a Spotify account with Authorization Code + PKCE: generates the PKCE secrets, runs the
/// loopback redirect listener, opens the system browser, exchanges the code for tokens, and persists
/// the refresh token. The access token is held in memory. Registered as a singleton so the in-memory
/// token and connection state are shared app-wide.
/// </summary>
/// <remarks>
/// This is the walking-skeleton happy path: token refresh/rotation, single-flight refresh, silent
/// session restore, and Premium/Free handling are added later.
/// </remarks>
internal sealed partial class SpotifyAuthService : IAuthService
{
    private static readonly TimeSpan _consentTimeout = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly DevClientIdSource _clientIdSource;
    private readonly SpotifyOptions _options;
    private readonly ILogger<SpotifyAuthService> _logger;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry;

    public SpotifyAuthService(
        IHttpClientFactory httpClientFactory,
        IRefreshTokenStore refreshTokenStore,
        DevClientIdSource clientIdSource,
        IOptions<SpotifyOptions> options,
        ILogger<SpotifyAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _refreshTokenStore = refreshTokenStore;
        _clientIdSource = clientIdSource;
        _options = options.Value;
        _logger = logger;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public Account? CurrentAccount { get; private set; }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Silent restore needs the refresh-token exchange, which is not part of this slice, so there is
    /// never a usable session to restore yet.
    /// </summary>
    public Task<bool> RestoreSessionAsync() => Task.FromResult(false);

    public Task<string> GetAccessTokenAsync()
    {
        // No proactive/reactive refresh yet: return the token captured at connect, or fail clearly.
        if (_accessToken is null)
        {
            throw new InvalidOperationException("Not connected to Spotify.");
        }

        return Task.FromResult(_accessToken);
    }

    public Task DisconnectAsync()
    {
        _accessToken = null;
        _accessTokenExpiry = default;
        CurrentAccount = null;
        _refreshTokenStore.Clear();
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public async Task<AuthResult> ConnectAsync()
    {
        string clientId = _clientIdSource.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new AuthResult(false, false, false, "Enter your Spotify Client ID first.");
        }

        SetState(ConnectionState.Connecting);

        string redirectUri = SpotifyOAuthConstants.RedirectUri(_options.RedirectPort);
        PkceCodes codes = PkceCodes.Generate();

        using var listener = new LoopbackCallbackListener(_options.RedirectPort);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            LogListenerStartFailed(ex, _options.RedirectPort);
            SetState(ConnectionState.Error);
            return new AuthResult(false, false, false,
                $"The callback port {_options.RedirectPort} is in use. Close whatever is using it and try again.");
        }

        try
        {
            string authorizeUrl = BuildAuthorizeUrl(clientId, redirectUri, codes, _options.Scopes);
            if (!await Launcher.LaunchUriAsync(new Uri(authorizeUrl)))
            {
                SetState(ConnectionState.Error);
                return new AuthResult(false, false, false, "Couldn't open the browser to sign in to Spotify.");
            }

            using var timeout = new CancellationTokenSource(_consentTimeout);
            OAuthCallback callback = await listener.WaitForCallbackAsync(timeout.Token);

            if (!string.Equals(callback.State, codes.State, StringComparison.Ordinal))
            {
                SetState(ConnectionState.Error);
                return new AuthResult(false, false, false, "The sign-in response didn't match this request. Please try again.");
            }

            if (callback.Error is not null)
            {
                // Denial is a normal, retryable outcome — return to disconnected rather than error.
                SetState(ConnectionState.Disconnected);
                return new AuthResult(false, Denied: true, false, null);
            }

            if (string.IsNullOrEmpty(callback.Code))
            {
                SetState(ConnectionState.Error);
                return new AuthResult(false, false, false, "Spotify didn't return an authorization code. Please try again.");
            }

            return await ExchangeCodeAsync(clientId, redirectUri, callback.Code, codes.Verifier, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            SetState(ConnectionState.Disconnected);
            return new AuthResult(false, false, false, "Sign-in timed out before it was completed.");
        }
        catch (HttpRequestException ex)
        {
            LogTokenRequestFailed(ex);
            SetState(ConnectionState.Error);
            return new AuthResult(false, false, false, "Couldn't reach Spotify to complete sign-in. Please try again.");
        }
    }

    private async Task<AuthResult> ExchangeCodeAsync(
        string clientId, string redirectUri, string code, string verifier, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
        });

        using HttpClient http = _httpClientFactory.CreateClient();
        using HttpResponseMessage response = await http.PostAsync(SpotifyOAuthConstants.TokenEndpoint, form, ct);
        if (!response.IsSuccessStatusCode)
        {
            LogTokenExchangeRejected((int)response.StatusCode);
            SetState(ConnectionState.Error);
            return new AuthResult(false, false, false, "Spotify rejected the sign-in. Please try connecting again.");
        }

        TokenResponse? tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        if (tokens?.AccessToken is null)
        {
            SetState(ConnectionState.Error);
            return new AuthResult(false, false, false, "Spotify's response was missing the access token. Please try again.");
        }

        _accessToken = tokens.AccessToken;
        _accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            _refreshTokenStore.Save(tokens.RefreshToken);
        }

        SetState(ConnectionState.Connected);
        return new AuthResult(true, false, false, null);
    }

    private static string BuildAuthorizeUrl(string clientId, string redirectUri, PkceCodes codes, string[] scopes)
    {
        (string Key, string Value)[] parameters =
        [
            ("response_type", "code"),
            ("client_id", clientId),
            ("redirect_uri", redirectUri),
            ("scope", string.Join(' ', scopes)),
            ("state", codes.State),
            ("code_challenge_method", SpotifyOAuthConstants.CodeChallengeMethod),
            ("code_challenge", codes.Challenge),
        ];

        string query = string.Join('&', parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return $"{SpotifyOAuthConstants.AuthorizeEndpoint}?{query}";
    }

    private void SetState(ConnectionState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        ConnectionStateChanged?.Invoke(this, state);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);

    [LoggerMessage(Level = LogLevel.Error, Message = "OAuth callback listener failed to start on port {Port}.")]
    private partial void LogListenerStartFailed(Exception exception, int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Spotify token request failed at the network level.")]
    private partial void LogTokenRequestFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Spotify token exchange returned HTTP {StatusCode}.")]
    private partial void LogTokenExchangeRejected(int statusCode);
}
