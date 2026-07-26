using Amplify.Core.Auth;
using Amplify.Core.ConnectionStatus;
using Amplify.Core.Spotify;

namespace Amplify.Tests.ConnectionStatus;

public class StatusPresentationTests
{
    [Theory]
    [InlineData(ConnectionState.Disconnected)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Error)]
    public void NonConnectedStatesNeverShowTheCard(ConnectionState state)
    {
        var presentation = new StatusPresentation(state, null);

        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ConnectingStateShowsOnlyTheConnectingInfoBar()
    {
        var presentation = new StatusPresentation(ConnectionState.Connecting, null);

        Assert.True(presentation.IsConnecting);
        Assert.False(presentation.IsError);
        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ErrorStateShowsOnlyTheErrorInfoBar()
    {
        var presentation = new StatusPresentation(ConnectionState.Error, null);

        Assert.True(presentation.IsError);
        Assert.False(presentation.IsConnecting);
        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ConnectedWithActiveDeviceShowsTheCardWithTheDeviceName()
    {
        var playerState = new PlayerState(true, 42, "Kitchen Speaker", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.True(presentation.ShowConnectedCard);
        Assert.True(presentation.HasActiveDevice);
        Assert.True(presentation.DeviceSupportsVolume);
        Assert.Equal("Kitchen Speaker", presentation.DeviceName);
    }

    [Fact]
    public void ActiveDeviceWithoutVolumeSupportIsStillNamedButFlaggedUnsupported()
    {
        // The card names the device either way — the caller needs the flag to explain why the volume
        // control beneath it is dimmed.
        var playerState = new PlayerState(true, 100, "Living Room TV", false);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.True(presentation.HasActiveDevice);
        Assert.False(presentation.DeviceSupportsVolume);
        Assert.Equal("Living Room TV", presentation.DeviceName);
    }

    [Fact]
    public void ConnectedWithoutActiveDeviceStillShowsTheCardWithNoDeviceName()
    {
        // No active device is a normal, non-error state (the user just hasn't started playback
        // anywhere yet) — the card still shows, just without a device name.
        var playerState = new PlayerState(false, 0, null, false);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.True(presentation.ShowConnectedCard);
        Assert.False(presentation.HasActiveDevice);
        Assert.False(presentation.DeviceSupportsVolume);
        Assert.Null(presentation.DeviceName);
    }

    [Fact]
    public void ConnectedWithNoPlayerStateYetIsTreatedAsNoActiveDevice()
    {
        // The player state hasn't been read yet (e.g. the refresh is still in flight) — treat that
        // the same as "no active device" rather than failing or waiting to show the card.
        var presentation = new StatusPresentation(ConnectionState.Connected, null);

        Assert.True(presentation.ShowConnectedCard);
        Assert.False(presentation.HasActiveDevice);
    }

    [Fact]
    public void DeviceNameAndVolumeSupportAreSuppressedWhenThereIsNoActiveDevice()
    {
        // Defensive, and deliberately constructs a state the client can no longer produce:
        // DeviceName should already be null and SupportsVolume false when HasActiveDevice is false
        // (per contracts.md). PlayerState is a plain record with no invariant of its own, so the
        // presentation must not surface either value even if some future producer sets them.
        var playerState = new PlayerState(false, 0, "Stale Device", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.Null(presentation.DeviceName);
        Assert.False(presentation.DeviceSupportsVolume);
    }
}
