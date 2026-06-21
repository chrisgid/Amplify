namespace Amplify.Core.Spotify;

/// <summary>
/// A projection of Spotify's current playback state, limited to what Amplify needs to control
/// volume. Built from <c>GET /v1/me/player</c>: a <c>200</c> response carries the active device's
/// volume and name, while a <c>204 No Content</c> means nothing is playing — surfaced here as
/// <see cref="HasActiveDevice"/> being <c>false</c> rather than as an error.
/// </summary>
/// <param name="HasActiveDevice">Whether Spotify has an active device that can be controlled.</param>
/// <param name="VolumePercent">The active device's volume, 0–100 (0 when there is no device).</param>
/// <param name="DeviceName">The active device's display name, or <c>null</c> when there is none.</param>
public sealed record PlayerState(bool HasActiveDevice, int VolumePercent, string? DeviceName);
