namespace Amplify.Core.Settings;

/// <summary>
/// The complete set of user preferences Amplify persists between runs. This is the single source of
/// truth for everything that crosses feature boundaries; it is serialised to JSON and owned by
/// <see cref="ISettingsService"/>. Sensitive data (the refresh token) is deliberately absent — it
/// lives in the Windows Credential Locker, not here.
/// </summary>
public sealed class AppSettings
{
    /// <summary>The schema version this file knows how to load; bumped only for structural changes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The version of the persisted shape, serialised first. The loader compares it against
    /// <see cref="CurrentSchemaVersion"/> to decide whether to migrate, load as-is, or reset.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Launch Amplify automatically when the user signs in to Windows.</summary>
    public bool LaunchAtStartup { get; set; } = true;

    /// <summary>Open into the tray rather than showing the window on launch.</summary>
    public bool StartMinimizedToTray { get; set; } = true;

    /// <summary>Closing the window hides it to the tray instead of exiting the app.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// Internal one-shot state, not a user-facing preference: set once the "still running in the
    /// tray" hint has been shown the first time the window hides to the tray, so it is never shown
    /// again. Appears nowhere in the Settings UI; cleared by a full reset (restoring the hint).
    /// </summary>
    public bool TrayHintShown { get; set; }

    /// <summary>
    /// The per-user Spotify Client ID captured during onboarding. Not a secret under PKCE, but
    /// per-user, so it belongs with user data rather than shipped configuration.
    /// </summary>
    public string SpotifyClientId { get; set; } = "";

    /// <summary>The app's theme preference; defaults to following the Windows theme.</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>The smallest valid <see cref="VolumeStep"/>.</summary>
    public const int MinVolumeStep = 1;

    /// <summary>The largest valid <see cref="VolumeStep"/>.</summary>
    public const int MaxVolumeStep = 25;

    /// <summary>
    /// The percentage each hotkey press changes the volume by; clamped to
    /// [<see cref="MinVolumeStep"/>, <see cref="MaxVolumeStep"/>] when loaded.
    /// </summary>
    public int VolumeStep { get; set; } = 5;

    /// <summary>The volume-up hotkey in its canonical string form.</summary>
    public string HotkeyVolumeUp { get; set; } = "ctrl+alt+arrowup";

    /// <summary>The volume-down hotkey in its canonical string form.</summary>
    public string HotkeyVolumeDown { get; set; } = "ctrl+alt+arrowdown";

    /// <summary>The last-known window placement, restored on the next launch when present.</summary>
    public WindowState? Window { get; set; }
}

/// <summary>
/// The persisted size and position of the main window, in device (physical) pixels — the screen
/// coordinate space the WinUI <c>AppWindow</c> reads and writes directly, so restoring needs no
/// DPI conversion. <see langword="null"/> until the user first moves or resizes the window.
/// </summary>
/// <param name="Width">Window width in device pixels.</param>
/// <param name="Height">Window height in device pixels.</param>
/// <param name="X">Left edge in device pixels.</param>
/// <param name="Y">Top edge in device pixels.</param>
public sealed record WindowState(int Width, int Height, int X, int Y);
