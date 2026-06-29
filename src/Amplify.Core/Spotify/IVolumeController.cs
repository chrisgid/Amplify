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
    /// active device on the last read. Hotkey nudges are ignored while this is <c>false</c>.
    /// </summary>
    bool CanControl { get; }

    /// <summary>
    /// Sets the active device's volume to <paramref name="percent"/> (clamped 0–100), updating the
    /// model optimistically before the Web API call. A no-op while <see cref="CanControl"/> is false.
    /// </summary>
    Task SetVolumeAsync(int percent);

    /// <summary>
    /// Changes the volume by one configured step in <paramref name="direction"/> (+1 up / -1 down),
    /// clamped to 0–100. A no-op while <see cref="CanControl"/> is false.
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
