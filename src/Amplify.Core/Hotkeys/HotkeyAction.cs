namespace Amplify.Core.Hotkeys;

/// <summary>
/// The two volume actions a global hotkey can trigger. Each action has exactly one bound
/// combination at a time, persisted and registered independently.
/// </summary>
public enum HotkeyAction
{
    /// <summary>Raise the Spotify volume by the configured step.</summary>
    VolumeUp,

    /// <summary>Lower the Spotify volume by the configured step.</summary>
    VolumeDown,
}
