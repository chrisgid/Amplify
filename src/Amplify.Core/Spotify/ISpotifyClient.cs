namespace Amplify.Core.Spotify;

/// <summary>
/// A thin client over the Spotify Web API endpoints Amplify uses to read and change playback volume.
/// Implemented as a typed <c>HttpClient</c> that authorises each request with the current access
/// token; it always targets Spotify's <em>active</em> device, so there is no device parameter.
/// </summary>
public interface ISpotifyClient
{
    /// <summary>
    /// Reads the current playback state (<c>GET /v1/me/player</c>). Returns a state with
    /// <see cref="PlayerState.HasActiveDevice"/> <c>false</c> when Spotify reports no active device
    /// (HTTP <c>204</c>); <c>null</c> only when the state could not be determined.
    /// </summary>
    Task<PlayerState?> GetPlayerStateAsync();

    /// <summary>
    /// Sets the active device's volume (<c>PUT /v1/me/player/volume</c>). The percentage is clamped
    /// to the valid 0–100 range before the request is sent.
    /// </summary>
    Task SetVolumeAsync(int percent);
}
