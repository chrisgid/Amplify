namespace Amplify.Core.Auth;

/// <summary>
/// Secure, persistent storage for the long-lived Spotify refresh token. Implemented over an
/// OS-managed secret store so the token is never written to settings or source. Abstracted as an
/// interface so token handling can be unit-tested without the real platform store.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Returns the stored refresh token, or <c>null</c> when none is saved.</summary>
    string? Load();

    /// <summary>Saves (or replaces) the refresh token.</summary>
    void Save(string refreshToken);

    /// <summary>Removes any stored refresh token. Safe to call when nothing is stored.</summary>
    void Clear();
}
