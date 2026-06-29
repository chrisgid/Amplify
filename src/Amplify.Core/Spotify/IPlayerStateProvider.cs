namespace Amplify.Core.Spotify;

/// <summary>
/// The single source of Spotify player state (active device + volume) for the app. One implementation
/// polls <c>GET /v1/me/player</c> on a timer while connected and visible, and also reads on demand, so
/// every consumer (the connection-status card and the volume controller) sees the same state from one
/// set of requests rather than each polling independently. Consumers observe
/// <see cref="PlayerStateChanged"/> and may seed from <see cref="Current"/>.
/// </summary>
public interface IPlayerStateProvider
{
    /// <summary>
    /// The last-known player state, or <c>null</c> when it hasn't been read yet or the account is
    /// disconnected. Lets a consumer resolved after the first read seed itself without waiting for the
    /// next change.
    /// </summary>
    PlayerState? Current { get; }

    /// <summary>
    /// Raised whenever the player state is re-read (poll, reconnect, or on-demand refresh), carrying
    /// the new state — or <c>null</c> when there is no active device or the account disconnected.
    /// </summary>
    event EventHandler<PlayerState?> PlayerStateChanged;

    /// <summary>
    /// Reads the current player state now and publishes it. Used for immediate reconciles (e.g. when a
    /// screen is shown) on top of the background poll; overlapping calls collapse into one in-flight read.
    /// </summary>
    Task RefreshAsync();
}
