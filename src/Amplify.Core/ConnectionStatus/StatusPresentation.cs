using Amplify.Core.Auth;
using Amplify.Core.Spotify;

namespace Amplify.Core.ConnectionStatus;

/// <summary>
/// Pure mapping from a connection state + last-known player state to the status block's
/// presentation flags. Kept free of any UI dependency — mirroring <c>ShellRouter</c> and
/// <c>OnboardingFlow</c> — so the state-combination rules can be unit-tested without WinUI; the
/// view-model that owns the live state is a thin adapter over this.
/// </summary>
/// <param name="State">The current connection lifecycle state.</param>
/// <param name="PlayerState">
/// The last-known player state, or <c>null</c> when it hasn't been read yet (e.g. not connected).
/// </param>
public readonly record struct StatusPresentation(ConnectionState State, PlayerState? PlayerState)
{
    /// <summary>Whether the connected account card (success or no-active-device variant) should show.</summary>
    public bool ShowConnectedCard => State == ConnectionState.Connected;

    /// <summary>Whether the connecting <c>InfoBar</c> should show.</summary>
    public bool IsConnecting => State == ConnectionState.Connecting;

    /// <summary>Whether the error <c>InfoBar</c> (with Reconnect) should show.</summary>
    public bool IsError => State == ConnectionState.Error;

    /// <summary>Whether Spotify reported an active device on the last refresh.</summary>
    public bool HasActiveDevice => PlayerState is { HasActiveDevice: true };

    /// <summary>The active device's label, or <c>null</c> when there is none.</summary>
    public string? DeviceName => HasActiveDevice ? PlayerState?.DeviceName : null;
}
