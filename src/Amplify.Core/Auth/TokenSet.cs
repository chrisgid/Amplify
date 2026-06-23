namespace Amplify.Core.Auth;

/// <summary>
/// The tokens returned by Spotify's Accounts service from a code exchange or a refresh. The
/// <see cref="RefreshToken"/> is <c>null</c> when the response did not include one (Spotify may omit
/// it on refresh, in which case the previously stored token stays valid).
/// </summary>
/// <param name="AccessToken">The bearer token used to authorise Web API calls.</param>
/// <param name="RefreshToken">A new refresh token to persist, or <c>null</c> to keep the existing one.</param>
/// <param name="ExpiresInSeconds">Lifetime of <paramref name="AccessToken"/> in seconds.</param>
public sealed record TokenSet(string AccessToken, string? RefreshToken, int ExpiresInSeconds)
{
    /// <summary>The absolute UTC instant at which <see cref="AccessToken"/> expires.</summary>
    public DateTimeOffset ExpiresAt(DateTimeOffset now) => now.AddSeconds(ExpiresInSeconds);
}
