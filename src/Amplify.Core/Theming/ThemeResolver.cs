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

    /// <summary>
    /// Resolves a preference all the way to a concrete <see cref="ResolvedTheme.Light"/> or
    /// <see cref="ResolvedTheme.Dark"/> — never <see cref="ResolvedTheme.Default"/> — for the parts of
    /// the UI the framework can't theme for us. The system caption buttons are the case in point: they
    /// are drawn by the OS rather than by the XAML tree, so they must be handed explicit colours and
    /// "follow the OS" has to be collapsed to an actual light or dark by the caller.
    /// </summary>
    /// <param name="mode">The stored appearance preference.</param>
    /// <param name="systemIsDark">Reads whether Windows is currently using its dark theme. The OS
    /// query is a platform concern, kept out of this mapping so it stays testable — and taken as a
    /// delegate rather than a value so it is invoked <em>only</em> for the modes that follow the OS.
    /// A pinned Light/Dark preference never touches it.</param>
    public static ResolvedTheme ResolveEffective(ThemeMode mode, Func<bool> systemIsDark) => Resolve(mode) switch
    {
        ResolvedTheme.Light => ResolvedTheme.Light,
        ResolvedTheme.Dark => ResolvedTheme.Dark,
        _ => systemIsDark() ? ResolvedTheme.Dark : ResolvedTheme.Light,
    };
}
