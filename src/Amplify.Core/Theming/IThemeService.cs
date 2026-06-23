using Amplify.Core.Settings;

namespace Amplify.Core.Theming;

/// <summary>
/// Applies the user's appearance preference to the app. <see cref="ThemeMode.System"/> follows the
/// current Windows light/dark theme (and accent) live; <see cref="ThemeMode.Light"/> and
/// <see cref="ThemeMode.Dark"/> pin the app to a fixed appearance regardless of the OS setting.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Applies the given theme preference, recomputing the effective appearance and notifying
    /// listeners via <see cref="ThemeChanged"/> when it changes.
    /// </summary>
    void Apply(ThemeMode mode);

    /// <summary>
    /// Raised when the effective theme changes — either because the stored preference changed or,
    /// while following the system, because Windows switched its theme or accent colour. The shell's
    /// window listens to this to re-apply the appearance to its content.
    /// </summary>
    event EventHandler ThemeChanged;
}
