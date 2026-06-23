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
}
