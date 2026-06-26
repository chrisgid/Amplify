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
        var playerState = new PlayerState(true, 42, "Kitchen Speaker");
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.True(presentation.ShowConnectedCard);
        Assert.True(presentation.HasActiveDevice);
        Assert.Equal("Kitchen Speaker", presentation.DeviceName);
    }

    [Fact]
    public void ConnectedWithoutActiveDeviceStillShowsTheCardWithNoDeviceName()
    {
        // No active device is a normal, non-error state (the user just hasn't started playback
        // anywhere yet) — the card still shows, just without a device name.
        var playerState = new PlayerState(false, 0, null);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.True(presentation.ShowConnectedCard);
        Assert.False(presentation.HasActiveDevice);
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
    public void DeviceNameIsSuppressedWhenThereIsNoActiveDeviceEvenIfPlayerStateCarriesOne()
    {
        // Defensive: PlayerState.DeviceName should already be null when HasActiveDevice is false
        // (per contracts.md), but the presentation shouldn't surface a stale name either way.
        var playerState = new PlayerState(false, 0, "Stale Device");
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState);

        Assert.Null(presentation.DeviceName);
    }
}
