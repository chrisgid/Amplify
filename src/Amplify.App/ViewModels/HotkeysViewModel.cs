using System.ComponentModel;
using Amplify.App.Hotkeys;
using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs the keyboard-shortcuts section of the main screen: the two rebindable rows (Volume up /
/// Volume down) and the shared status line beneath them. It seeds each row from the persisted
/// bindings, keeps them current when settings change elsewhere (e.g. the volume step, or a reset),
/// and aggregates the rows' recording state and transient messages into one status line.
/// </summary>
public sealed partial class HotkeysViewModel : ObservableObject
{
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();

    private string? _statusMessage;

    public HotkeysViewModel(IHotkeyService hotkeys, ISettingsService settings)
    {
        _hotkeys = hotkeys;
        _settings = settings;

        // Captured on the UI thread (resolved while the page is built) so settings changes raised on
        // other threads can be marshalled back before touching bindable state.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        Up = new HotkeyRowViewModel(HotkeyAction.VolumeUp, Resolve(HotkeyAction.VolumeUp), hotkeys, settings, _strings);
        Down = new HotkeyRowViewModel(HotkeyAction.VolumeDown, Resolve(HotkeyAction.VolumeDown), hotkeys, settings, _strings);
        Up.SetOtherComboAccessor(() => Down.Current);
        Down.SetOtherComboAccessor(() => Up.Current);
        Up.SetStatusSink(SetStatus);
        Down.SetStatusSink(SetStatus);
        Up.PropertyChanged += OnRowPropertyChanged;
        Down.PropertyChanged += OnRowPropertyChanged;

        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>The Volume-up row.</summary>
    public HotkeyRowViewModel Up { get; }

    /// <summary>The Volume-down row.</summary>
    public HotkeyRowViewModel Down { get; }

    /// <summary>
    /// The shared line under the rows: the recording prompt while listening, a transient message
    /// (e.g. a conflict) once shown, or the idle "works globally" hint.
    /// </summary>
    public string StatusLineText =>
        IsRecording ? _strings.GetString("Hotkey_Recording_Helper")
        : _statusMessage ?? _strings.GetString("Hotkey_Footer_Hint");

    /// <summary>Whether the status line is showing a message that should stand out (a conflict).</summary>
    public bool StatusLineIsCaution => !IsRecording && _statusMessage is not null;

    /// <summary>Whether the status line is showing ordinary (non-caution) text.</summary>
    public bool StatusLineIsNormal => !StatusLineIsCaution;

    private bool IsRecording => Up.IsRecording || Down.IsRecording;

    private void SetStatus(string? message)
    {
        _statusMessage = message;
        NotifyStatusLine();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeyRowViewModel.IsRecording))
        {
            // Mute hotkey actions while recording so the combo being captured doesn't also fire.
            _hotkeys.IsSuspended = IsRecording;
            NotifyStatusLine();
        }
    }

    private void NotifyStatusLine()
    {
        OnPropertyChanged(nameof(StatusLineText));
        OnPropertyChanged(nameof(StatusLineIsCaution));
        OnPropertyChanged(nameof(StatusLineIsNormal));
    }

    private Hotkey Resolve(HotkeyAction action)
    {
        string canonical = action == HotkeyAction.VolumeUp
            ? _settings.Current.HotkeyVolumeUp
            : _settings.Current.HotkeyVolumeDown;
        return HotkeyDefaults.Resolve(canonical, action);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        _dispatcher.RunOnUi(() =>
        {
            Up.Reload(Resolve(HotkeyAction.VolumeUp));
            Down.Reload(Resolve(HotkeyAction.VolumeDown));
            Up.RefreshStep();
            Down.RefreshStep();
        });
}
