using Amplify.App.Dev;
using Amplify.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Amplify.App.Views;

/// <summary>
/// The main control screen. Currently a placeholder hosting the temporary volume controls (which
/// share their state with the global hotkeys) plus a way through to settings. The connected status
/// card, hotkey bindings, and live volume meter replace this content later.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly DevPlaybackSlice _playback;
    private readonly ShellViewModel _shell;

    public MainPage()
    {
        InitializeComponent();
        _playback = App.Services.GetRequiredService<DevPlaybackSlice>();
        _shell = App.Services.GetRequiredService<ShellViewModel>();

        // Keep this page's instance alive across a trip to settings so its state is preserved.
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _playback.Changed += OnPlaybackChanged;
        UpdateUi();
        await _playback.RefreshAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _playback.Changed -= OnPlaybackChanged;
    }

    private void OnPlaybackChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(UpdateUi);

    private void UpdateUi()
    {
        VolumeText.Text = _playback.HasActiveDevice ? $"Volume {_playback.Volume}%" : "Volume —";
        DeviceText.Text = _playback switch
        {
            { LastError: { } error } => error,
            { HasActiveDevice: true, DeviceName: { } name } => $"Now controlling: {name}",
            _ => "No active Spotify device. Start playback in Spotify, then press Refresh.",
        };
    }

    private void OnVolumeUpClick(object sender, RoutedEventArgs e) => _ = _playback.NudgeAsync(1);

    private void OnVolumeDownClick(object sender, RoutedEventArgs e) => _ = _playback.NudgeAsync(-1);

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await _playback.RefreshAsync();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _shell.GoToSettingsCommand.Execute(null);
}
