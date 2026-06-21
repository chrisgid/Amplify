using Amplify.Core.Spotify;

namespace Amplify.App.Dev;

/// <summary>
/// TEMPORARY scaffolding that keeps the end-to-end volume slice working through the shell: it reads
/// the active device's volume and changes it, holding the last-known value so the main screen and the
/// global hotkeys share one source of truth. A fixed 5% step stands in until a configurable step and
/// the real volume controller land — at which point this holder is removed.
/// </summary>
public sealed class DevPlaybackSlice(ISpotifyClient spotifyClient)
{
    // Matches the real default step; there is no configurable step size yet.
    private const int _step = 5;

    private bool _refreshing;

    /// <summary>Last-known volume of the active device (0..100).</summary>
    public int Volume { get; private set; }

    /// <summary>Whether Spotify reported an active device on the last refresh.</summary>
    public bool HasActiveDevice { get; private set; }

    /// <summary>Label of the active device, or <c>null</c> when there is none.</summary>
    public string? DeviceName { get; private set; }

    /// <summary>Set when the last Spotify call failed, so the UI can show why.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised after any state change so the UI can refresh.</summary>
    public event EventHandler? Changed;

    /// <summary>Reads the current player state from Spotify and updates the cached values.</summary>
    public async Task RefreshAsync()
    {
        // Connecting and the first navigation to the main screen can both start a refresh; collapse
        // overlapping calls (all on the UI thread) into a single in-flight read.
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            PlayerState? state = await spotifyClient.GetPlayerStateAsync();
            HasActiveDevice = state is { HasActiveDevice: true };
            if (HasActiveDevice && state is not null)
            {
                Volume = state.VolumePercent;
                DeviceName = state.DeviceName;
            }
            else
            {
                DeviceName = null;
            }

            LastError = null;
        }
        catch (HttpRequestException)
        {
            LastError = "Couldn't read the current volume from Spotify.";
        }
        finally
        {
            _refreshing = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Nudges the volume by one step in the given direction (+1 up / -1 down), clamped 0..100.</summary>
    public Task NudgeAsync(int direction) => SetVolumeAsync(Volume + (direction * _step));

    /// <summary>Sets the active device's volume to <paramref name="percent"/>, clamped 0..100.</summary>
    public async Task SetVolumeAsync(int percent)
    {
        if (!HasActiveDevice)
        {
            return;
        }

        int target = Math.Clamp(percent, 0, 100);
        try
        {
            // Commit only once Spotify accepts the change, so the displayed value can't drift.
            await spotifyClient.SetVolumeAsync(target);
            Volume = target;
            LastError = null;
        }
        catch (HttpRequestException)
        {
            LastError = "Couldn't change the volume. Make sure Spotify is playing on an active device.";
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
