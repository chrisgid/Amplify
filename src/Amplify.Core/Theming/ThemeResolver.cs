using Amplify.Core.Settings;

namespace Amplify.Core.Theming;

/// <summary>
/// The effective appearance the app applies to its UI. <see cref="Default"/> defers to the current
/// Windows theme (the framework follows the OS live); the other two pin a fixed appearance.
/// </summary>
public enum ResolvedTheme
{
    /// <summary>Follow the current Windows theme.</summary>
    Default,

    /// <summary>Force the light theme.</summary>
    Light,

    /// <summary>Force the dark theme.</summary>
    Dark,
}

/// <summary>
/// Maps the stored <see cref="ThemeMode"/> preference to the effective <see cref="ResolvedTheme"/>.
/// Kept UI-free (no <c>Microsoft.UI.Xaml</c> types) so the mapping is unit-testable; the app layer
/// translates the result to the framework's <c>ElementTheme</c>.
/// </summary>
public static class ThemeResolver
{
    /// <summary>
    /// Resolves a preference to its effective appearance: <see cref="ThemeMode.Light"/> and
    /// <see cref="ThemeMode.Dark"/> are fixed; <see cref="ThemeMode.System"/> (and any unknown value)
    /// resolves to <see cref="ResolvedTheme.Default"/>, which follows the current OS theme.
    /// </summary>
    public static ResolvedTheme Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ResolvedTheme.Light,
        ThemeMode.Dark => ResolvedTheme.Dark,
        _ => ResolvedTheme.Default,
    };
}
