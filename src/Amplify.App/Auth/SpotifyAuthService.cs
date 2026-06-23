using System.Net;
using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Amplify.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.System;

namespace Amplify.App.Auth;

/// <summary>
/// Connects a Spotify account with Authorization Code + PKCE and owns the token lifecycle: it runs
/// the interactive browser flow, stores the refresh token in the Credential Locker, keeps the access
/// token in memory with its expiry, and refreshes it transparently — proactively before expiry and
/// reactively when a request is rejected. Registered as a singleton so the token and connection
/// state are shared app-wide. The per-user Client ID is read from settings; Amplify ships none.
/// </summary>
internal sealed partial class SpotifyAuthService : IAuthService, ISpotifyTokenProvider, IDisposable
{
    private static readonly TimeSpan _consentTimeout = TimeSpan.FromMinutes(5);

    // Refresh this far ahead of the stated expiry so a token never goes stale mid-request.
    private static readonly TimeSpan _refreshSkew = TimeSpan.FromSeconds(60);

    private readonly SpotifyTokenClient _tokenClient;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly ISettingsService _settings;
    private readonly SpotifyOptions _options;
    private readonly ILogger<SpotifyAuthService> _logger;

    // Serialises refreshes so a burst of callers triggers at most one token request in flight.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry;
    private string? _refreshToken;

    public SpotifyAuthService(
        SpotifyTokenClient tokenClient,
        IRefreshTokenStore refreshTokenStore,
        ISettingsService settings,
        IOptions<SpotifyOptions> options,
        ILogger<SpotifyAuthService> logger)
    {
        _tokenClient = tokenClient;
        _refreshTokenStore = refreshTokenStore;
        _settings = settings;
        _options = options.Value;
        _logger = logger;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public Account? CurrentAccount { get; private set; }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Silently restores a session from the stored refresh token: refreshes the access token, reads
    /// the account, and transitions to connected. Returns <c>false</c> (leaving the app on onboarding)
    /// when there is no stored token or Client ID, or the refresh fails.
    /// </summary>
    public async Task<bool> RestoreSessionAsync()
    {
        string? refreshToken = _refreshTokenStore.Load();
        string clientId = _settings.Current.SpotifyClientId;
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        _refreshToken = refreshToken;
        SetState(ConnectionState.Connecting);
        try
        {
            TokenSet tokens = await _tokenClient.RefreshAsync(clientId, refreshToken).ConfigureAwait(false);
            ApplyTokens(tokens);
            CurrentAccount = await _tokenClient.GetAccountAsync(_accessToken!).ConfigureAwait(false);
            SetState(ConnectionState.Connected);
            return true;
        }
        catch (HttpRequestException ex)
        {
            LogRestoreFailed(ex);
            // A 400/401 means the refresh token is no longer valid — drop it so we don't retry a dead
            // token on every launch. Transient failures keep the token for a later attempt.
            if (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                _refreshTokenStore.Clear();
            }

            ClearTokens();
            SetState(ConnectionState.Disconnected);
            return false;
        }
    }

    public async Task<AuthResult> ConnectAsync()
    {
        string clientId = _settings.Current.SpotifyClientId;
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

            switch (OAuthCallbackEvaluator.Evaluate(callback.Code, callback.State, callback.Error, codes.State))
            {
                case OAuthCallbackOutcome.StateMismatch:
                    SetState(ConnectionState.Error);
                    return new AuthResult(false, false, false,
                        "The sign-in response didn't match this request. Please try again.");

                case OAuthCallbackOutcome.Denied:
                    // Denial is a normal, retryable outcome — return to disconnected rather than error.
                    SetState(ConnectionState.Disconnected);
                    return new AuthResult(false, Denied: true, false, null);

                case OAuthCallbackOutcome.MissingCode:
                    SetState(ConnectionState.Error);
                    return new AuthResult(false, false, false,
                        "Spotify didn't return an authorization code. Please try again.");
            }

            return await CompleteConnectAsync(clientId, redirectUri, callback.Code!, codes.Verifier, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            SetState(ConnectionState.Disconnected);
            return new AuthResult(false, false, false, "Sign-in timed out before it was completed.");
        }
        catch (HttpRequestException ex)
        {
            LogConnectFailed(ex);
            SetState(ConnectionState.Error);
            return new AuthResult(false, false, false, "Couldn't complete sign-in with Spotify. Please try again.");
        }
    }

    public Task<string> GetAccessTokenAsync()
    {
        // Fast path: a comfortably-valid token needs no lock.
        if (TokenIsFresh())
        {
            return Task.FromResult(_accessToken!);
        }

        return RefreshIfNeededAsync();
    }

    /// <summary>Forces a refresh after a 401, collapsing concurrent retries onto one token request.</summary>
    public async Task<string> RefreshAccessTokenAsync(string previousToken)
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Another caller may have already refreshed in response to the same 401 burst.
            if (_accessToken is not null && !string.Equals(_accessToken, previousToken, StringComparison.Ordinal))
            {
                return _accessToken;
            }

            await RefreshCoreAsync().ConfigureAwait(false);
            return _accessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public Task DisconnectAsync()
    {
        ClearTokens();
        _refreshTokenStore.Clear();
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    /// <summary>Disposes the refresh lock. Called by the host when the singleton is torn down.</summary>
    public void Dispose() => _refreshLock.Dispose();

    private async Task<string> RefreshIfNeededAsync()
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check under the lock: a concurrent caller may have just refreshed.
            if (TokenIsFresh())
            {
                return _accessToken!;
            }

            await RefreshCoreAsync().ConfigureAwait(false);
            return _accessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // Performs the actual refresh. Must be called while holding _refreshLock.
    private async Task RefreshCoreAsync()
    {
        if (_refreshToken is null)
        {
            throw new InvalidOperationException("Not connected to Spotify.");
        }

        string clientId = _settings.Current.SpotifyClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("No Spotify Client ID is configured.");
        }

        try
        {
            TokenSet tokens = await _tokenClient.RefreshAsync(clientId, _refreshToken).ConfigureAwait(false);
            ApplyTokens(tokens);
        }
        catch (HttpRequestException ex)
        {
            LogRefreshFailed(ex);
            SetState(ConnectionState.Error);
            throw;
        }
    }

    private async Task<AuthResult> CompleteConnectAsync(
        string clientId, string redirectUri, string code, string verifier, CancellationToken ct)
    {
        TokenSet tokens = await _tokenClient.ExchangeCodeAsync(clientId, redirectUri, code, verifier, ct)
            .ConfigureAwait(false);
        ApplyTokens(tokens);

        CurrentAccount = await _tokenClient.GetAccountAsync(_accessToken!, ct).ConfigureAwait(false);
        SetState(ConnectionState.Connected);
        return new AuthResult(true, false, NotPremium: !CurrentAccount.IsPremium, null);
    }

    private bool TokenIsFresh() =>
        _accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiry - _refreshSkew;

    private void ApplyTokens(TokenSet tokens)
    {
        _accessToken = tokens.AccessToken;
        _accessTokenExpiry = tokens.ExpiresAt(DateTimeOffset.UtcNow);

        // Spotify rotates the refresh token only sometimes; persist a new one, keep the old otherwise.
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            _refreshToken = tokens.RefreshToken;
            _refreshTokenStore.Save(tokens.RefreshToken);
        }
    }

    private void ClearTokens()
    {
        _accessToken = null;
        _accessTokenExpiry = default;
        _refreshToken = null;
        CurrentAccount = null;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "OAuth callback listener failed to start on port {Port}.")]
    private partial void LogListenerStartFailed(Exception exception, int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Spotify sign-in failed to complete.")]
    private partial void LogConnectFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Silent session restore failed; staying signed out.")]
    private partial void LogRestoreFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Spotify access-token refresh failed.")]
    private partial void LogRefreshFailed(Exception exception);
}
