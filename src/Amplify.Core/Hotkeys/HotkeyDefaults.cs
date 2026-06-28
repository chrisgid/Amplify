using Amplify.Core.Settings;

namespace Amplify.Core.Hotkeys;

/// <summary>
/// Resolves the effective hotkey for an action: the persisted canonical string when it's valid,
/// falling back to the built-in default. The defaults are taken from a fresh <see cref="AppSettings"/>
/// so there is a single source of truth for them (owned by the settings layer) rather than a second
/// copy here.
/// </summary>
public static class HotkeyDefaults
{
    private static readonly AppSettings _defaults = new();

    /// <summary>The stored combo if it parses to a valid hotkey, otherwise the default for the action.</summary>
    public static Hotkey Resolve(string? canonical, HotkeyAction action) =>
        Parse(canonical) ?? For(action);

    /// <summary>The built-in default hotkey for <paramref name="action"/>.</summary>
    public static Hotkey For(HotkeyAction action)
    {
        string canonical = action == HotkeyAction.VolumeUp ? _defaults.HotkeyVolumeUp : _defaults.HotkeyVolumeDown;
        return Parse(canonical)
            ?? throw new InvalidOperationException($"The built-in default hotkey for {action} is invalid: '{canonical}'.");
    }

    private static Hotkey? Parse(string? canonical) =>
        Hotkey.TryParse(canonical, out Hotkey? hotkey) && hotkey.IsValid ? hotkey : null;
}
