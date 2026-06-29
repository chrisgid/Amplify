using Amplify.Core.Auth;
using Amplify.Core.ConnectionStatus;
using Amplify.Core.Spotify;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs the status block at the top of the main screen: the connected account card (with or
/// without an active device — neither is a warning) and the connecting/error <c>InfoBar</c>s.
/// Connection state and the account come from <see cref="IAuthService"/> (already read during
/// connect — this view-model never calls the profile endpoint itself); active-device presence/name
/// comes from the shared <see cref="IPlayerStateProvider"/>, which this view-model only observes
/// (the polling lives there, fed to every consumer from one set of requests).
/// </summary>
public sealed partial class StatusViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IPlayerStateProvider _playerStateProvider;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ILogger<StatusViewModel> _logger;
    private readonly ResourceLoader _strings = new();

    private PlayerState? _playerState;

    public StatusViewModel(
        IAuthService auth, IPlayerStateProvider playerStateProvider, ILogger<StatusViewModel> logger)
    {
        _auth = auth;
        _playerStateProvider = playerStateProvider;
        _logger = logger;

        // Captured on the UI thread (the view-model is resolved while the main page is built), so the
        // connection-state event (which can be raised off-thread during a background refresh) can
        // safely touch bindable state.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Seed from the provider's last-known state in case it read before this view-model existed.
        _playerState = _playerStateProvider.Current;

        _auth.ConnectionStateChanged += OnConnectionStateChanged;
        _playerStateProvider.PlayerStateChanged += OnPlayerStateChanged;
    }

    // All the state-combination rules (which card/InfoBar to show) live in StatusPresentation
    // (Amplify.Core), so they're unit-tested without WinUI; this is a thin, always-current
    // projection of it for x:Bind.
    private StatusPresentation Presentation => new(_auth.State, _playerState);

    /// <summary>The current connection lifecycle state.</summary>
    public ConnectionState State => _auth.State;

    /// <summary>The connected account, or <c>null</c> when not connected.</summary>
    public Account? Account => _auth.CurrentAccount;

    /// <summary>Whether Spotify reported an active device on the last refresh.</summary>
    public bool HasActiveDevice => Presentation.HasActiveDevice;

    /// <summary>The active device's label, or <c>null</c> when there is none.</summary>
    public string? DeviceName => Presentation.DeviceName;

    /// <summary>The card's device line: the active device's name, or a "no active device" hint.</summary>
    public string DeviceLineText => HasActiveDevice
        ? DeviceName ?? string.Empty
        : _strings.GetString("Status_DeviceLine_NoActiveDevice");

    /// <summary>The "Connected" label shown beside the card.</summary>
    public string ConnectedLabelText => _strings.GetString("Status_Connected_Label");

    /// <summary>Whether the connected account card should show — with or without an active device.</summary>
    public bool ShowConnectedCard => Presentation.ShowConnectedCard;

    /// <summary>Whether the connecting <c>InfoBar</c> should show.</summary>
    public bool IsConnecting => Presentation.IsConnecting;

    /// <summary>Whether the error <c>InfoBar</c> (with Reconnect) should show.</summary>
    public bool IsError => Presentation.IsError;

    /// <summary>Re-runs the connect/refresh path after a failure.</summary>
    [RelayCommand]
    private async Task ReconnectAsync()
    {
        try
        {
            await _auth.ConnectAsync();
        }
        catch (Exception ex)
        {
            // IAuthService.ConnectAsync is documented to convert every failure into a non-success
            // AuthResult rather than throwing, but guard against an unexpected escape so a bug there
            // can never leave Reconnect permanently broken with no diagnostics.
            LogReconnectFailed(ex);
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        _dispatcher.RunOnUi(() => HandleStateChanged(state));

    private void HandleStateChanged(ConnectionState state)
    {
        // Drop any stale device immediately on disconnect; the provider also publishes null, but doing
        // it here keeps the card consistent without waiting for that to arrive.
        if (state != ConnectionState.Connected)
        {
            _playerState = null;
        }

        NotifyStateChanged();
    }

    private void OnPlayerStateChanged(object? sender, PlayerState? state) =>
        _dispatcher.RunOnUi(() =>
        {
            _playerState = state;
            NotifyPlayerStateChanged();
        });

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(ShowConnectedCard));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(IsError));
        NotifyPlayerStateChanged();
    }

    private void NotifyPlayerStateChanged()
    {
        OnPropertyChanged(nameof(HasActiveDevice));
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(DeviceLineText));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconnect attempt failed unexpectedly.")]
    private partial void LogReconnectFailed(Exception exception);
}
