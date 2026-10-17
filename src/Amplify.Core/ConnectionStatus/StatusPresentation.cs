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
/// <param name="IsUnreachable">
/// Whether player-state reads have been failing for long enough to call Spotify unreachable (see
/// <see cref="ReachabilityTracker"/>). Separate from <paramref name="State"/> on purpose: the token
/// is still good, only the network is gone, so this clears itself on the next successful read.
/// </param>
public readonly record struct StatusPresentation(
    ConnectionState State, PlayerState? PlayerState, bool IsUnreachable)
{
    /// <summary>
    /// Whether the connected account card (success or no-active-device variant) should show. Suppressed
    /// while unreachable: the card asserts "Connected" beside a device name and a volume that are only
    /// as fresh as the last successful read, so leaving it up would present stale data as current —
    /// the whole point of surfacing the outage.
    /// </summary>
    public bool ShowConnectedCard => State == ConnectionState.Connected && !IsUnreachable;

    /// <summary>Whether the connecting <c>InfoBar</c> should show.</summary>
    public bool IsConnecting => State == ConnectionState.Connecting;

    /// <summary>Whether the error <c>InfoBar</c> (with Reconnect) should show.</summary>
    public bool IsError => State == ConnectionState.Error;

    /// <summary>
    /// Whether the unreachable <c>InfoBar</c> (with Try again) should show. Only while otherwise
    /// connected — a dead token already has its own <see cref="IsError"/> bar, and that one wins
    /// because re-authorising is the real fix there.
    /// </summary>
    public bool IsUnreachableNotice => State == ConnectionState.Connected && IsUnreachable;

    /// <summary>Whether Spotify reported an active device on the last refresh.</summary>
    public bool HasActiveDevice => PlayerState is { HasActiveDevice: true };

    /// <summary>The active device's label, or <c>null</c> when there is none.</summary>
    public string? DeviceName => HasActiveDevice ? PlayerState?.DeviceName : null;

    /// <summary>
    /// Whether the active device accepts volume commands. False both when there is no device and when
    /// the device itself can't be volume-controlled — the card names the device either way, but the
    /// latter needs its own line so a permanently dimmed volume control isn't left unexplained.
    /// </summary>
    public bool DeviceSupportsVolume => PlayerState is { HasActiveDevice: true, SupportsVolume: true };
}
