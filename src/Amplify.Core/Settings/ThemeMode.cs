namespace Amplify.Core.Settings;

/// <summary>
/// The app's theme preference. <see cref="System"/> follows the current Windows theme live; the
/// other two pin the app to a fixed appearance regardless of the OS setting.
/// </summary>
public enum ThemeMode
{
    /// <summary>Follow the user's Windows theme (the default).</summary>
    System,

    /// <summary>Always use the light theme.</summary>
    Light,

    /// <summary>Always use the dark theme.</summary>
    Dark,
}
