using Amplify.Core.Settings;
using Amplify.Core.Theming;

namespace Amplify.Tests.Theming;

public class ThemeResolverTests
{
    [Theory]
    [InlineData(ThemeMode.System, ResolvedTheme.Default)] // Default follows the current OS theme
    [InlineData(ThemeMode.Light, ResolvedTheme.Light)]
    [InlineData(ThemeMode.Dark, ResolvedTheme.Dark)]
    public void ResolveMapsPreferenceToEffectiveTheme(ThemeMode mode, ResolvedTheme expected)
    {
        Assert.Equal(expected, ThemeResolver.Resolve(mode));
    }

    [Fact]
    public void ResolveTreatsUnknownModeAsSystem()
    {
        // A value outside the defined range (e.g. a forward-compatible setting) follows the OS.
        Assert.Equal(ResolvedTheme.Default, ThemeResolver.Resolve((ThemeMode)999));
    }

    [Theory]
    [InlineData(ThemeMode.System, false, ResolvedTheme.Light)]
    [InlineData(ThemeMode.System, true, ResolvedTheme.Dark)]
    [InlineData((ThemeMode)999, true, ResolvedTheme.Dark)] // unknown follows the OS, like System
    public void ResolveEffectiveCollapsesSystemToTheCurrentOsTheme(
        ThemeMode mode, bool systemIsDark, ResolvedTheme expected)
    {
        Assert.Equal(expected, ThemeResolver.ResolveEffective(mode, () => systemIsDark));
    }

    [Theory]
    [InlineData(ThemeMode.Light, false, ResolvedTheme.Light)]
    [InlineData(ThemeMode.Light, true, ResolvedTheme.Light)]
    [InlineData(ThemeMode.Dark, false, ResolvedTheme.Dark)]
    [InlineData(ThemeMode.Dark, true, ResolvedTheme.Dark)]
    public void ResolveEffectiveIgnoresTheOsThemeWhenOverridden(
        ThemeMode mode, bool systemIsDark, ResolvedTheme expected)
    {
        // A manual override pins the appearance: the caption buttons follow the app, not Windows.
        Assert.Equal(expected, ThemeResolver.ResolveEffective(mode, () => systemIsDark));
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void ResolveEffectiveDoesNotReadTheOsThemeWhenOverridden(ThemeMode mode)
    {
        // The read is a COM call into UISettings, so an answer that can't change it isn't worth
        // asking for — the caller passes a delegate precisely so this case can skip it.
        bool wasRead = false;

        ThemeResolver.ResolveEffective(mode, () => { wasRead = true; return true; });

        Assert.False(wasRead);
    }

    [Theory]
    [InlineData(ThemeMode.System)]
    [InlineData((ThemeMode)999)]
    public void ResolveEffectiveReadsTheOsThemeWhenFollowingTheSystem(ThemeMode mode)
    {
        bool wasRead = false;

        ThemeResolver.ResolveEffective(mode, () => { wasRead = true; return true; });

        Assert.True(wasRead);
    }

    [Theory]
    [InlineData(ThemeMode.System)]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData((ThemeMode)999)]
    public void ResolveEffectiveNeverReturnsDefault(ThemeMode mode)
    {
        // Its whole point is to be handed to a colour picker, which has no "follow the OS" option.
        Assert.NotEqual(ResolvedTheme.Default, ThemeResolver.ResolveEffective(mode, () => false));
        Assert.NotEqual(ResolvedTheme.Default, ThemeResolver.ResolveEffective(mode, () => true));
    }
}
