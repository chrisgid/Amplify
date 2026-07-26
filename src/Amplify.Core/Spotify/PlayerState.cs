namespace Amplify.Core.Spotify;

/// <summary>
/// A projection of Spotify's current playback state, limited to what Amplify needs to control
/// volume. Built from <c>GET /v1/me/player</c>: a <c>200</c> describing an active device carries its
/// volume and name. Nothing playing is surfaced as <see cref="HasActiveDevice"/> being <c>false</c>
/// with every other field empty, rather than as an error — that covers a <c>204 No Content</c>, a
/// <c>200</c> with no device, and a <c>200</c> whose device is inactive.
/// </summary>
/// <param name="HasActiveDevice">
/// Whether Spotify has an active device. Presence alone doesn't mean its volume can be changed — see
/// <paramref name="SupportsVolume"/>.
/// </param>
/// <param name="VolumePercent">The active device's volume, 0–100 (0 when there is no device).</param>
/// <param name="DeviceName">The active device's display name, or <c>null</c> when there is none.</param>
/// <param name="SupportsVolume">
/// Whether the active device accepts volume commands. <c>false</c> when Spotify reports the device
/// can't be used to set the volume, or that it refuses Web API commands altogether — both mean a
/// volume call would be rejected, so the control is disabled up front rather than after a failed
/// write. Always <c>false</c> when there is no active device.
/// </param>
public sealed record PlayerState(
    bool HasActiveDevice, int VolumePercent, string? DeviceName, bool SupportsVolume);
