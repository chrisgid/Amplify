namespace Amplify.Core.Auth;

/// <summary>
/// The Spotify connection lifecycle exposed by <see cref="IAuthService"/>.
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
