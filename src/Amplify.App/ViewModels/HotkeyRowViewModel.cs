using System.Globalization;
using Amplify.App.Hotkeys;
using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs one hotkey row (Volume up or Volume down): the current combination shown as keycaps, the
/// per-row sub-text, and the record-to-rebind interaction. Capturing a new combination validates it
/// (a non-modifier key, with or without modifiers, and not the same as the other row's), registers
/// it globally, and persists it — keeping the previous binding if registration fails. Transient
/// messages and the recording prompt are surfaced through a shared status sink rather than on the
/// row itself.
/// </summary>
public sealed partial class HotkeyRowViewModel : ObservableObject
{
    private readonly HotkeyAction _action;
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settings;
    private readonly ResourceLoader _strings;

    // Reads the other row's current combo so a duplicate can be rejected; set by the parent once both
    // rows exist.
    private Func<Hotkey?> _otherCombo = () => null;

    // Surfaces a transient status message (or null to clear) to the shared status line; set by parent.
    private Action<string?> _setStatus = _ => { };

    /// <summary>Whether the row is listening for a new combination.</summary>
    [ObservableProperty]
    public partial bool IsRecording { get; set; }

    public HotkeyRowViewModel(
        HotkeyAction action, Hotkey current, IHotkeyService hotkeys, ISettingsService settings, ResourceLoader strings)
    {
        _action = action;
        Current = current;
        _hotkeys = hotkeys;
        _settings = settings;
        _strings = strings;
    }

    /// <summary>The current combination bound to this action.</summary>
    public Hotkey Current { get; private set; }

    /// <summary>The row title, e.g. "Volume up".</summary>
    public string ActionLabel => _strings.GetString(
        _action == HotkeyAction.VolumeUp ? "Hotkey_VolumeUp_Label" : "Hotkey_VolumeDown_Label");

    /// <summary>The keycap tokens for the current combo, resolved against the active keyboard layout.</summary>
    public IReadOnlyList<string> DisplayTokens => KeyLabelResolver.ToLayoutTokens(Current);

    /// <summary>Whether the keycaps (rather than the "listening" prompt) should be shown.</summary>
    public bool ShowKeycaps => !IsRecording;

    /// <summary>The sub-text describing the action, including the current volume step.</summary>
    public string SubText => string.Format(
        CultureInfo.CurrentCulture,
        _strings.GetString(_action == HotkeyAction.VolumeUp ? "Hotkey_VolumeUp_SubText" : "Hotkey_VolumeDown_SubText"),
        _settings.Current.VolumeStep);

    /// <summary>Wires up the accessor used to reject a combo already bound to the other action.</summary>
    public void SetOtherComboAccessor(Func<Hotkey?> otherCombo) => _otherCombo = otherCombo;

    /// <summary>Wires up the sink that surfaces this row's transient messages to the shared status line.</summary>
    public void SetStatusSink(Action<string?> setStatus) => _setStatus = setStatus;

    /// <summary>Enters recording mode, clearing any previous status message.</summary>
    public void BeginRecording()
    {
        _setStatus(null);
        IsRecording = true;
    }

    /// <summary>Leaves recording mode without changing the binding.</summary>
    public void CancelRecording() => IsRecording = false;

    /// <summary>
    /// Handles a captured key press while recording. Modifier-only or otherwise invalid presses are
    /// ignored (recording continues); a valid combo is validated, registered, and persisted.
    /// </summary>
    public void Capture(KeyModifiers modifiers, uint key)
    {
        if (!Hotkey.TryCreate(modifiers, key, out Hotkey? combo))
        {
            return;
        }

        if (combo == _otherCombo())
        {
            IsRecording = false;
            _setStatus(_strings.GetString("Hotkey_Conflict_Duplicate"));
            return;
        }

        if (_hotkeys.TryRegister(_action, combo))
        {
            Current = combo;
            _settings.Update(s => Assign(s, combo.ToCanonicalString()));
            IsRecording = false;
            _setStatus(null);
            NotifyBinding();
        }
        else
        {
            IsRecording = false;
            _setStatus(_strings.GetString("Hotkey_Conflict_InUse"));
        }
    }

    /// <summary>Refreshes the bound combo from an external change (e.g. a settings reset).</summary>
    public void Reload(Hotkey current)
    {
        if (current == Current)
        {
            return;
        }

        Current = current;
        NotifyBinding();
    }

    /// <summary>Refreshes the sub-text after the volume step changes.</summary>
    public void RefreshStep() => OnPropertyChanged(nameof(SubText));

    private void Assign(AppSettings settings, string canonical)
    {
        if (_action == HotkeyAction.VolumeUp)
        {
            settings.HotkeyVolumeUp = canonical;
        }
        else
        {
            settings.HotkeyVolumeDown = canonical;
        }
    }

    private void NotifyBinding() => OnPropertyChanged(nameof(DisplayTokens));

    partial void OnIsRecordingChanged(bool value) => OnPropertyChanged(nameof(ShowKeycaps));
}
