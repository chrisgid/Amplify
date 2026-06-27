using Amplify.App.Dev;
using Amplify.App.ViewModels;
using Amplify.Core.Hotkeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;

namespace Amplify.App.Views;

/// <summary>
/// The main control screen: the connection-status card, the two rebindable global-hotkey rows, and
/// (temporarily) the volume controls from the walking skeleton. The live volume meter replaces the
/// temporary controls later.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly DevPlaybackSlice _playback;
    private readonly ShellViewModel _shell;

    public MainPage()
    {
        // Resolve the view-models before InitializeComponent so x:Bind sees them as the bindings are
        // wired up.
        StatusViewModel = App.Services.GetRequiredService<StatusViewModel>();
        HotkeysViewModel = App.Services.GetRequiredService<HotkeysViewModel>();
        InitializeComponent();
        _playback = App.Services.GetRequiredService<DevPlaybackSlice>();
        _shell = App.Services.GetRequiredService<ShellViewModel>();

        // Keep this page's instance alive across a trip to settings so its state is preserved.
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    /// <summary>The bound connection-status view-model; public so generated <c>x:Bind</c> code can read it.</summary>
    public StatusViewModel StatusViewModel { get; }

    /// <summary>The bound hotkeys view-model; public so generated <c>x:Bind</c> code can read it.</summary>
    public HotkeysViewModel HotkeysViewModel { get; }

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

    private void OnUpEditClick(object sender, RoutedEventArgs e) => ToggleRecording(HotkeysViewModel.Up, UpHotkeyRow);

    private void OnDownEditClick(object sender, RoutedEventArgs e) => ToggleRecording(HotkeysViewModel.Down, DownHotkeyRow);

    // Clicking edit starts listening; moving focus onto the row itself means subsequent key presses
    // arrive at its KeyDown handler (and the non-button row never activates on Space/Enter).
    private static void ToggleRecording(HotkeyRowViewModel row, Control rowElement)
    {
        if (row.IsRecording)
        {
            row.CancelRecording();
            return;
        }

        row.BeginRecording();
        rowElement.Focus(FocusState.Programmatic);
    }

    // Leaving the row (clicking elsewhere) abandons an in-progress recording. LostFocus is a bubbling
    // event, so it also fires as focus moves between the row and its own edit button (e.g. when
    // recording starts and focus shifts onto the row) — only cancel when focus has actually left the
    // row's subtree.
    private void OnRowLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement rowElement
            || rowElement.Tag is not HotkeyRowViewModel row
            || !row.IsRecording)
        {
            return;
        }

        DependencyObject? focused = FocusManager.GetFocusedElement(rowElement.XamlRoot) as DependencyObject;
        if (!IsWithin(focused, rowElement))
        {
            row.CancelRecording();
        }
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject ancestor)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void OnRowKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not HotkeyRowViewModel row || !row.IsRecording)
        {
            return;
        }

        // Swallow keys while recording so they don't leak to focus navigation or other handlers.
        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            row.CancelRecording();
            return;
        }

        // Wait for the non-modifier key; a bare modifier press isn't a usable combo yet.
        if (Hotkey.IsModifierVirtualKey((uint)e.Key))
        {
            return;
        }

        row.Capture(CurrentModifiers(), (uint)e.Key);
    }

    private static KeyModifiers CurrentModifiers()
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if (IsDown(VirtualKey.Control))
        {
            modifiers |= KeyModifiers.Ctrl;
        }

        if (IsDown(VirtualKey.Menu))
        {
            modifiers |= KeyModifiers.Alt;
        }

        if (IsDown(VirtualKey.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
        {
            modifiers |= KeyModifiers.Win;
        }

        return modifiers;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
