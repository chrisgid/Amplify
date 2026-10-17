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
        var presentation = new StatusPresentation(state, null, false);

        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ConnectingStateShowsOnlyTheConnectingInfoBar()
    {
        var presentation = new StatusPresentation(ConnectionState.Connecting, null, false);

        Assert.True(presentation.IsConnecting);
        Assert.False(presentation.IsError);
        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ErrorStateShowsOnlyTheErrorInfoBar()
    {
        var presentation = new StatusPresentation(ConnectionState.Error, null, false);

        Assert.True(presentation.IsError);
        Assert.False(presentation.IsConnecting);
        Assert.False(presentation.ShowConnectedCard);
    }

    [Fact]
    public void ConnectedWithActiveDeviceShowsTheCardWithTheDeviceName()
    {
        var playerState = new PlayerState(true, 42, "Kitchen Speaker", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, false);

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
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, false);

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
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, false);

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
        var presentation = new StatusPresentation(ConnectionState.Connected, null, false);

        Assert.True(presentation.ShowConnectedCard);
        Assert.False(presentation.HasActiveDevice);
    }

    [Fact]
    public void DeviceNameAndVolumeSupportAreSuppressedWhenThereIsNoActiveDevice()
    {
        // Defensive: per contracts.md, DeviceName should already be null and SupportsVolume false
        // when HasActiveDevice is false. PlayerState is a plain record that can't enforce that, so
        // the presentation must not surface either value even if some future producer sets them.
        var playerState = new PlayerState(false, 0, "Stale Device", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, false);

        Assert.Null(presentation.DeviceName);
        Assert.False(presentation.DeviceSupportsVolume);
    }

    [Fact]
    public void UnreachableReplacesTheConnectedCardWithItsOwnNotice()
    {
        // The card asserts "Connected" next to a device name and volume that are only as fresh as the
        // last successful read. While Spotify is unreachable that is stale data presented as current,
        // so the card gives way to a notice that says so.
        var playerState = new PlayerState(true, 42, "Kitchen Speaker", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, true);

        Assert.True(presentation.IsUnreachableNotice);
        Assert.False(presentation.ShowConnectedCard);
        Assert.False(presentation.IsError);
        Assert.False(presentation.IsConnecting);
    }

    [Fact]
    public void ClearingUnreachableBringsTheCardStraightBack()
    {
        // The self-healing path: the same inputs with the flag cleared — as the next successful read
        // leaves them — must restore the healthy card with no other state change and no user action.
        var playerState = new PlayerState(true, 42, "Kitchen Speaker", true);
        var presentation = new StatusPresentation(ConnectionState.Connected, playerState, false);

        Assert.False(presentation.IsUnreachableNotice);
        Assert.True(presentation.ShowConnectedCard);
        Assert.Equal("Kitchen Speaker", presentation.DeviceName);
    }

    [Theory]
    [InlineData(ConnectionState.Disconnected)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Error)]
    public void UnreachableIsOnlyNoticedWhileOtherwiseConnected(ConnectionState state)
    {
        // Not connected means there is nothing to be unreachable from, and an auth error already has
        // its own InfoBar — showing both would offer two different recoveries for one problem, and
        // re-authorising is the one that actually fixes a dead token.
        var presentation = new StatusPresentation(state, null, true);

        Assert.False(presentation.IsUnreachableNotice);
    }

    [Fact]
    public void AnAuthErrorKeepsItsOwnInfoBarEvenWhileReadsAreFailing()
    {
        // A dead token usually comes with failing reads; the error bar (and its Reconnect) must win,
        // because the Try again button on the unreachable bar would never recover from it.
        var presentation = new StatusPresentation(ConnectionState.Error, null, true);

        Assert.True(presentation.IsError);
        Assert.False(presentation.IsUnreachableNotice);
    }
}
