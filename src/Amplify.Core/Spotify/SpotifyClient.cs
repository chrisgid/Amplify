using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Amplify.Core.Spotify;

/// <summary>
/// Typed <c>HttpClient</c> implementation of <see cref="ISpotifyClient"/> against the Spotify Web API.
/// No third-party SDK: requests are built by hand. Bearer authorization (and refresh-on-401) is
/// supplied by a message handler in the client's pipeline, so this client stays free of auth
/// concerns. Lives in the UI-free core so the request/response mapping is unit-testable with a fake
/// message handler. The base address (<c>https://api.spotify.com/</c>) is configured where the
/// client is registered.
/// </summary>
/// <remarks>
/// Public only so the typed-client registration in the app assembly can name it; callers depend on
/// <see cref="ISpotifyClient"/>, never on this concrete type.
/// </remarks>
public sealed class SpotifyClient(HttpClient http) : ISpotifyClient
{
    public async Task<PlayerState?> GetPlayerStateAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/me/player");

        using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);

        // No active device: Spotify returns an empty 204 — a normal state, not a failure.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new PlayerState(false, 0, null);
        }

        response.EnsureSuccessStatusCode();

        PlaybackState? state = await response.Content
            .ReadFromJsonAsync<PlaybackState>().ConfigureAwait(false);
        Device? device = state?.Device;
        if (device is null)
        {
            return new PlayerState(false, 0, null);
        }

        return new PlayerState(device.IsActive, device.VolumePercent ?? 0, device.Name);
    }

    public async Task SetVolumeAsync(int percent)
    {
        int clamped = Math.Clamp(percent, 0, 100);

        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"v1/me/player/volume?volume_percent={clamped}");

        using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);

        // 404 "Device not found" and 403 (restriction) both mean there's no active, controllable
        // device — a distinct, recoverable case rather than a generic failure.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            throw new DeviceNotControllableException();
        }

        response.EnsureSuccessStatusCode();
    }

    // Minimal projections of the Web API JSON — only the fields Amplify reads.
    private sealed record PlaybackState(
        [property: JsonPropertyName("device")] Device? Device);

    private sealed record Device(
        [property: JsonPropertyName("is_active")] bool IsActive,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("volume_percent")] int? VolumePercent);
}
