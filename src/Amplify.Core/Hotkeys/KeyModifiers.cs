namespace Amplify.Core.Hotkeys;

/// <summary>
/// The modifier keys that must be held for a global hotkey to fire. A valid hotkey needs at least
/// one of these alongside a non-modifier key. The flag values are independent of any Win32 modifier
/// constants — they are mapped to <c>MOD_*</c> values where the registration happens.
/// </summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifiers.</summary>
    None = 0,

    /// <summary>The Control key.</summary>
    Ctrl = 1,

    /// <summary>The Alt (Menu) key.</summary>
    Alt = 2,

    /// <summary>The Shift key.</summary>
    Shift = 4,

    /// <summary>The Windows key.</summary>
    Win = 8,
}
