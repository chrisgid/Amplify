namespace Amplify.Core.Auth;

/// <summary>
/// Fixed endpoints and parameter values for Spotify's Authorization Code with PKCE flow. These live
/// on the Spotify Accounts service (not the Web API) and are the same for every user, so they are
/// centralised here rather than scattered as string literals.
/// </summary>
public static class SpotifyOAuthConstants
{
    /// <summary>Authorization endpoint the system browser is sent to.</summary>
    public const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";

    /// <summary>Token endpoint for the code exchange and refresh.</summary>
    public const string TokenEndpoint = "https://accounts.spotify.com/api/token";

    /// <summary>
    /// Web API endpoint for the current user's profile, read once on connect to record the display
    /// name and Premium status. The profile is owned by authentication; the Web API client does not
    /// expose a separate profile call.
    /// </summary>
    public const string CurrentUserEndpoint = "https://api.spotify.com/v1/me";

    /// <summary>Path component of the loopback redirect URI (<c>http://127.0.0.1:{port}/callback</c>).</summary>
    public const string RedirectPath = "/callback";

    /// <summary>PKCE challenge method; Amplify always uses SHA-256.</summary>
    public const string CodeChallengeMethod = "S256";

    /// <summary>
    /// Builds the registered loopback redirect URI for the given port. Uses the literal loopback IP
    /// (not <c>localhost</c>) so it matches the URI registered in the Spotify dashboard exactly.
    /// </summary>
    public static string RedirectUri(int port) => $"http://127.0.0.1:{port}{RedirectPath}";
}
