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
/// connect — this view-model never calls the profile endpoint itself); active-device
/// presence/name comes from <see cref="ISpotifyClient.GetPlayerStateAsync"/>, read once on
/// connect and then polled on <see cref="_pollInterval"/> while connected, since Spotify has no
/// push notification for "a device became active".
/// </summary>
public sealed partial class StatusViewModel : ObservableObject
{
    // Spotify has no push notification for "a device became active" — polling on a short interval
    // while connected is the only way to notice a device starting playback without a manual
    // refresh. Chosen short enough to feel responsive, long enough not to hammer the Web API.
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    private readonly IAuthService _auth;
    private readonly ISpotifyClient _spotifyClient;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ILogger<StatusViewModel> _logger;
    private readonly ResourceLoader _strings = new();
    private readonly DispatcherQueueTimer? _pollTimer;

    private PlayerState? _playerState;

    // Whether the main window is currently visible. Set by the shell via Suspend()/Resume() (driven
    // by Window.VisibilityChanged — e.g. minimised) so polling stops when nobody can see the result.
    // Defaults to true: the window is visible when this view-model is first resolved.
    private bool _windowVisible = true;

    public StatusViewModel(IAuthService auth, ISpotifyClient spotifyClient, ILogger<StatusViewModel> logger)
    {
        _auth = auth;
        _spotifyClient = spotifyClient;
        _logger = logger;

        // Captured on the UI thread (the view-model is resolved while the main page is built), so
        // the connection-state event (which can be raised off-thread during a background refresh)
        // and the player-state read below can both safely touch bindable state.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _pollTimer = _dispatcher?.CreateTimer();
        if (_pollTimer is not null)
        {
            _pollTimer.Interval = _pollInterval;
            _pollTimer.IsRepeating = true;
            _pollTimer.Tick += (_, _) => _ = RefreshPlayerStateAsync();
        }

        _auth.ConnectionStateChanged += OnConnectionStateChanged;

        if (_auth.State == ConnectionState.Connected)
        {
            _ = RefreshPlayerStateAsync();
        }

        UpdatePollingState();
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

    /// <summary>
    /// Stops polling while the main window isn't visible (e.g. minimised) — there's no point
    /// reading Spotify's player state on a timer when nobody can see the result. Called by the
    /// shell from <c>Window.VisibilityChanged</c>.
    /// </summary>
    public void Suspend()
    {
        _windowVisible = false;
        UpdatePollingState();
    }

    /// <summary>
    /// Resumes polling when the main window becomes visible again, with an immediate refresh so the
    /// card catches up on anything that changed while suspended (e.g. playback was started on a
    /// device while Amplify was minimised). Called by the shell from <c>Window.VisibilityChanged</c>.
    /// </summary>
    public void Resume()
    {
        _windowVisible = true;
        if (_auth.State == ConnectionState.Connected)
        {
            _ = RefreshPlayerStateAsync();
        }

        UpdatePollingState();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        _dispatcher.RunOnUi(() => HandleStateChanged(state));

    private void HandleStateChanged(ConnectionState state)
    {
        if (state == ConnectionState.Connected)
        {
            NotifyStateChanged();
            _ = RefreshPlayerStateAsync();
        }
        else
        {
            _playerState = null;
            NotifyStateChanged();
        }

        UpdatePollingState();
    }

    // The timer should only run while both connected and visible — either condition failing means
    // polling would either be useless (not connected) or wasted (not visible).
    private void UpdatePollingState()
    {
        if (_auth.State == ConnectionState.Connected && _windowVisible)
        {
            _pollTimer?.Start();
        }
        else
        {
            _pollTimer?.Stop();
        }
    }

    private async Task RefreshPlayerStateAsync()
    {
        PlayerState? state;
        try
        {
            state = await _spotifyClient.GetPlayerStateAsync();
        }
        catch (HttpRequestException ex)
        {
            LogPlayerStateRefreshFailed(ex);
            return;
        }

        _dispatcher.RunOnUi(() =>
        {
            _playerState = state;
            NotifyPlayerStateChanged();
        });
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't read Spotify player state for the status card.")]
    private partial void LogPlayerStateRefreshFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconnect attempt failed unexpectedly.")]
    private partial void LogReconnectFailed(Exception exception);
}
