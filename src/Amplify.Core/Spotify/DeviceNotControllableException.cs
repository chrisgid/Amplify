namespace Amplify.Core.Spotify;

/// <summary>
/// Thrown by <see cref="ISpotifyClient.SetVolumeAsync"/> when Spotify rejects a volume change because
/// there is no active, controllable device (HTTP <c>404</c> "Device not found" or <c>403</c>
/// restriction). Distinct from a transient <see cref="HttpRequestException"/> so callers can revert the
/// optimistic value and fall back to the "no active device" guidance rather than a generic failure.
/// </summary>
public sealed class DeviceNotControllableException : Exception
{
    public DeviceNotControllableException()
        : base("Spotify has no active device that can be controlled right now.")
    {
    }

    public DeviceNotControllableException(string message)
        : base(message)
    {
    }

    public DeviceNotControllableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
