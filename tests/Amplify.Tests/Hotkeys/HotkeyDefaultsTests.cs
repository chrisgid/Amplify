using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;

namespace Amplify.Tests.Hotkeys;

public class HotkeyDefaultsTests
{
    [Fact]
    public void ForMatchesTheAppSettingsDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(settings.HotkeyVolumeUp, HotkeyDefaults.For(HotkeyAction.VolumeUp).ToCanonicalString());
        Assert.Equal(settings.HotkeyVolumeDown, HotkeyDefaults.For(HotkeyAction.VolumeDown).ToCanonicalString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hotkey")]
    [InlineData("ctrl")]   // modifier-only => no key => not a usable hotkey
    public void ResolveFallsBackToDefaultWhenStoredValueIsUnusable(string? stored)
    {
        Hotkey resolved = HotkeyDefaults.Resolve(stored, HotkeyAction.VolumeUp);

        Assert.Equal(HotkeyDefaults.For(HotkeyAction.VolumeUp), resolved);
    }

    [Fact]
    public void ResolveUsesStoredValueWhenValid()
    {
        Hotkey resolved = HotkeyDefaults.Resolve("ctrl+shift+k", HotkeyAction.VolumeUp);

        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, 0x4B), resolved);
    }
}
