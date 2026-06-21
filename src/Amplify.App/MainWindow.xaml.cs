using Amplify.App.Auth;
using Amplify.App.Interop;
using Amplify.Core.Auth;
using Amplify.Core.Spotify;
using Microsoft.UI.Xaml;

namespace Amplify.App;

/// <summary>
/// The single main window that hosts Amplify's screens. Currently a bare shell with a temporary
/// connect-and-control test; the Mica backdrop, custom title bar, and routing are added later.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    // Fixed nudge while there's no configurable step size yet; the real default is also 5%.
    private const int _volumeStep = 5;

    private readonly IAuthService _authService;
    private readonly ISpotifyClient _spotifyClient;
    private readonly DevClientIdSource _clientIdSource;

    private GlobalHotkeyWindow? _hotkeys;
    private int _volume;
    private bool _hasActiveDevice;

    public MainWindow(IAuthService authService, ISpotifyClient spotifyClient, DevClientIdSource clientIdSource)
    {
        _authService = authService;
        _spotifyClient = spotifyClient;
        _clientIdSource = clientIdSource;
        InitializeComponent();
        Closed += OnClosed;
    }

    // Temporary handler for the walking-skeleton connect test; removed with the rest of the
    // throwaway UI when onboarding lands. Resumes on the UI thread after each await.
    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        _clientIdSource.ClientId = ClientIdBox.Text.Trim();
        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting…";
        try
        {
            AuthResult result = await _authService.ConnectAsync();
            StatusText.Text = result switch
            {
                { Success: true } => $"Connected. State: {_authService.State}.",
                { Denied: true } => "Access not granted. You can try again.",
                _ => result.Error ?? "Connection failed.",
            };

            if (result.Success)
            {
                ArmHotkeys();
                VolumePanel.Visibility = Visibility.Visible;
                await RefreshVolumeAsync();
            }
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void OnVolumeUpClick(object sender, RoutedEventArgs e) => _ = NudgeAsync(_volumeStep);

    private void OnVolumeDownClick(object sender, RoutedEventArgs e) => _ = NudgeAsync(-_volumeStep);

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await RefreshVolumeAsync();

    // Global hotkeys fire on the window's UI thread (the message loop we hooked), so it is safe to
    // touch the UI directly from here.
    private void OnVolumeNudged(object? sender, int direction) => _ = NudgeAsync(direction * _volumeStep);

    private void ArmHotkeys()
    {
        if (_hotkeys is not null)
        {
            return;
        }

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _hotkeys = new GlobalHotkeyWindow(hwnd);
        _hotkeys.VolumeNudged += OnVolumeNudged;
        try
        {
            _hotkeys.Register();
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async Task RefreshVolumeAsync()
    {
        try
        {
            PlayerState? state = await _spotifyClient.GetPlayerStateAsync();
            _hasActiveDevice = state is { HasActiveDevice: true };
            if (_hasActiveDevice && state is not null)
            {
                _volume = state.VolumePercent;
                DeviceText.Text = $"Now controlling: {state.DeviceName}";
            }
            else
            {
                DeviceText.Text = "No active Spotify device. Start playback in Spotify, then press Refresh.";
            }

            UpdateVolumeText();
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Couldn't read the current volume from Spotify.";
        }
    }

    private async Task NudgeAsync(int delta)
    {
        if (!_hasActiveDevice)
        {
            return;
        }

        int target = Math.Clamp(_volume + delta, 0, 100);
        try
        {
            // Commit only once Spotify accepts the change, so the displayed value can't drift from reality.
            await _spotifyClient.SetVolumeAsync(target);
            _volume = target;
            UpdateVolumeText();
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Couldn't change the volume. Make sure Spotify is playing on an active device.";
        }
    }

    private void UpdateVolumeText() =>
        VolumeText.Text = _hasActiveDevice ? $"Volume {_volume}%" : "Volume —";

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    /// <summary>Releases the global hotkey registration and window hook.</summary>
    public void Dispose()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
    }
}
