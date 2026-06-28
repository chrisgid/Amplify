using Amplify.Core.Hotkeys;

namespace Amplify.Tests.Hotkeys;

public class HotkeyTests
{
    private const uint _vkUp = 0x26;
    private const uint _vkDown = 0x28;
    private const uint _vkK = 0x4B;
    private const uint _vkControl = 0x11;

    public static TheoryData<KeyModifiers, uint, string> CanonicalCases => new()
    {
        { KeyModifiers.Ctrl | KeyModifiers.Alt, _vkUp, "ctrl+alt+arrowup" },
        { KeyModifiers.Ctrl | KeyModifiers.Alt, _vkDown, "ctrl+alt+arrowdown" },
        { KeyModifiers.Ctrl, _vkK, "ctrl+k" },
        { KeyModifiers.Win | KeyModifiers.Shift, 0x73, "shift+win+f4" },        // VK_F4
        { KeyModifiers.Alt, 0x62, "alt+num2" },                                  // VK_NUMPAD2
        { KeyModifiers.None, 0x7A, "f11" },                                       // single key, no modifier
        { KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Win, 0x39, "ctrl+alt+shift+win+9" },
    };

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void ToCanonicalStringProducesExpectedForm(KeyModifiers modifiers, uint key, string expected) =>
        Assert.Equal(expected, new Hotkey(modifiers, key).ToCanonicalString());

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void CanonicalRoundTrips(KeyModifiers modifiers, uint key, string canonical)
    {
        var original = new Hotkey(modifiers, key);

        Assert.True(Hotkey.TryParse(canonical, out Hotkey? parsed));
        Assert.Equal(original, parsed);
        Assert.Equal(canonical, parsed.ToCanonicalString());
    }

    [Fact]
    public void ToCanonicalStringOrdersModifiersIndependentlyOfFlagOrder()
    {
        // The same flags combined in a different order still serialise canonically.
        var a = new Hotkey(KeyModifiers.Win | KeyModifiers.Ctrl, _vkK);
        var b = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Win, _vkK);

        Assert.Equal("ctrl+win+k", a.ToCanonicalString());
        Assert.Equal(a.ToCanonicalString(), b.ToCanonicalString());
    }

    [Theory]
    [InlineData("CTRL+ALT+ARROWUP")]
    [InlineData("alt+ctrl+arrowup")]
    [InlineData(" ctrl + alt + arrowup ")]
    public void TryParseIsCaseAndOrderAndWhitespaceTolerant(string canonical)
    {
        Assert.True(Hotkey.TryParse(canonical, out Hotkey? parsed));
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, _vkUp), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ctrl+alt")]          // no key
    [InlineData("ctrl+alt+nonsense")] // unknown key token
    [InlineData("ctrl+a+b")]          // two keys
    public void TryParseRejectsInvalid(string? canonical)
    {
        Assert.False(Hotkey.TryParse(canonical, out Hotkey? parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void CanonicalFallsBackToNumericForUnmappedKey()
    {
        // An exotic VK with no friendly name still round-trips via the vk{code} form.
        var hotkey = new Hotkey(KeyModifiers.Ctrl, 0xFF);

        Assert.Equal("ctrl+vk255", hotkey.ToCanonicalString());
        Assert.True(Hotkey.TryParse("ctrl+vk255", out Hotkey? parsed));
        Assert.Equal(hotkey, parsed);
    }

    [Fact]
    public void ToDisplayTokensUsesSymbolsForNamedKeys() =>
        Assert.Equal(
            ["Ctrl", "Alt", "↑"],
            new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, _vkUp).ToDisplayTokens());

    [Fact]
    public void ToDisplayTokensUsesUpperCaseForLetters() =>
        Assert.Equal(["Ctrl", "K"], new Hotkey(KeyModifiers.Ctrl, _vkK).ToDisplayTokens());

    [Theory]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Alt, _vkUp, true)]   // modifier + key
    [InlineData(KeyModifiers.None, 0x7Au, true)]                       // single key (F11) is allowed
    [InlineData(KeyModifiers.Ctrl, 0u, false)]                        // no key
    [InlineData(KeyModifiers.None, _vkControl, false)]                 // modifier-only press
    [InlineData(KeyModifiers.Ctrl, _vkControl, false)]                 // still modifier-only
    public void TryCreateValidatesCombos(KeyModifiers modifiers, uint key, bool expected)
    {
        bool created = Hotkey.TryCreate(modifiers, key, out Hotkey? hotkey);

        Assert.Equal(expected, created);
        Assert.Equal(expected, hotkey is not null);
    }

    [Theory]
    [InlineData(0x10, true)]   // VK_SHIFT
    [InlineData(0x11, true)]   // VK_CONTROL
    [InlineData(0x12, true)]   // VK_MENU (Alt)
    [InlineData(0x5B, true)]   // VK_LWIN
    [InlineData(0x41, false)]  // 'A'
    [InlineData(0x26, false)]  // arrow up
    public void IsModifierVirtualKeyIdentifiesModifiers(uint vk, bool expected) =>
        Assert.Equal(expected, Hotkey.IsModifierVirtualKey(vk));

    [Fact]
    public void EqualityDetectsDuplicates()
    {
        var a = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, _vkUp);
        var b = new Hotkey(KeyModifiers.Alt | KeyModifiers.Ctrl, _vkUp);
        var different = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, _vkDown);

        Assert.Equal(a, b);
        Assert.NotEqual(a, different);
    }
}
