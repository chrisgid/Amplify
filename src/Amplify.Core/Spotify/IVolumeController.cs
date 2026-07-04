namespace Amplify.Core.Spotify;

/// <summary>
/// Owns the volume model for the active Spotify device: the last-known level, whether it can be
/// controlled right now, the step math behind the global hotkeys, and the optimistic UI updates that
/// keep the meter responsive. Sits over <see cref="ISpotifyClient"/> and reacts to hotkey presses; the
/// UI and notifications observe it rather than calling the Web API directly.
/// </summary>
public interface IVolumeController
{
    /// <summary>The last-known volume of the active device, 0–100.</summary>
    int Volume { get; }

    /// <summary>
    /// Whether volume can be changed right now — i.e. an account is connected and Spotify reported an
    /// active device on the last read.
    /// </summary>
    bool CanControl { get; }

    /// <summary>
    /// Sets the active device's volume to <paramref name="percent"/> (clamped 0–100), updating the
    /// model optimistically before the Web API call. When <see cref="CanControl"/> is false, first
    /// attempts one on-demand read (see <see cref="NudgeAsync"/>) and applies only if a device is
    /// found; otherwise a no-op.
    /// </summary>
    Task SetVolumeAsync(int percent);

    /// <summary>
    /// Changes the volume by one configured step in <paramref name="direction"/> (+1 up / -1 down),
    /// clamped to 0–100. While connected but <see cref="CanControl"/> is false, first does a single
    /// on-demand read — throttled — to pick up a device that became active while polling was suspended
    /// (e.g. the window is minimised), then nudges from the freshly-read level; if none is found it is
    /// a no-op.
    /// </summary>
    Task NudgeAsync(int direction);

    /// <summary>
    /// Re-reads the current player state from Spotify and reconciles the model (volume, device
    /// presence). Called at calm moments — when an account connects and when the window regains
    /// visibility — rather than on a continuous timer.
    /// </summary>
    Task RefreshAsync();

    /// <summary>Raised after the volume changes (optimistically or on reconcile); carries the new 0–100 value.</summary>
    event EventHandler<int> VolumeChanged;

    /// <summary>
    /// Raised when control availability or device context changes without the volume itself changing
    /// (e.g. a device appeared/disappeared), so the UI can re-evaluate <see cref="CanControl"/>.
    /// </summary>
    event EventHandler? StateChanged;
}
