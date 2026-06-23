namespace Amplify.Core.Auth;

/// <summary>
/// Seam over the authentication service's token lifecycle, used by the Web API authorization
/// handler. Kept separate from <see cref="IAuthService"/> so the handler depends only on what it
/// needs: a current token, and a way to force a refresh when the API rejects that token.
/// </summary>
public interface ISpotifyTokenProvider
{
    /// <summary>Returns a currently-valid access token, refreshing proactively when near expiry.</summary>
    Task<string> GetAccessTokenAsync();

    /// <summary>
    /// Forces a token refresh after a request was rejected with <c>401</c>.
    /// <paramref name="previousToken"/> is the bearer that failed; if another caller has already
    /// refreshed since (the current token differs), that newer token is returned without a redundant
    /// refresh.
    /// </summary>
    Task<string> RefreshAccessTokenAsync(string previousToken);
}
