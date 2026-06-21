namespace Amplify.Core.Auth;

/// <summary>
/// The Spotify connection lifecycle exposed by <see cref="IAuthService"/>. A Free (non-Premium)
/// account is <see cref="Connected"/> with the account flagged non-Premium — it is not a separate
/// state and not a failure.
/// </summary>
public enum ConnectionState
{
    /// <summary>No stored or usable session.</summary>
    Disconnected,

    /// <summary>Restoring, refreshing, or authorising.</summary>
    Connecting,

    /// <summary>Token valid and Spotify reachable.</summary>
    Connected,

    /// <summary>Token/refresh failed or Spotify unreachable.</summary>
    Error,
}
