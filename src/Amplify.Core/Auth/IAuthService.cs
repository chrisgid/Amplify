namespace Amplify.Core.Auth;

/// <summary>
/// Connects a Spotify account using Authorization Code with PKCE, manages the access/refresh token
/// lifecycle, records whether the account is Premium, and disconnects. This is the gateway every
/// Spotify-backed feature depends on; there is no client secret.
/// </summary>
public interface IAuthService
{
    /// <summary>The current connection lifecycle state.</summary>
    ConnectionState State { get; }

    /// <summary>The connected account, or <c>null</c> when not connected.</summary>
    Account? CurrentAccount { get; }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler<ConnectionState> ConnectionStateChanged;

    /// <summary>
    /// Silently restores a session from a stored refresh token, without user interaction.
    /// Returns <c>true</c> when a usable session was restored.
    /// </summary>
    Task<bool> RestoreSessionAsync();

    /// <summary>Runs the interactive PKCE flow in the system browser and connects the account.</summary>
    Task<AuthResult> ConnectAsync();

    /// <summary>Returns a currently-valid access token, refreshing it transparently when needed.</summary>
    Task<string> GetAccessTokenAsync();

    /// <summary>Clears the in-memory tokens and removes the stored refresh token.</summary>
    Task DisconnectAsync();
}
