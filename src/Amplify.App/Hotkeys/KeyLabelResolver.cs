using System.Runtime.InteropServices;
using Amplify.Core.Hotkeys;

namespace Amplify.App.Hotkeys;

/// <summary>
/// Produces the display keycap tokens for a hotkey, refining the layout-independent tokens from
/// <see cref="Hotkey.ToDisplayTokens"/> so that character keys show the glyph printed on the user's
/// active keyboard layout (e.g. a punctuation key reads correctly on a non-US layout). Named keys —
/// arrows, function keys, editing keys — keep their fixed symbolic labels.
/// </summary>
public static class KeyLabelResolver
{
    private const uint _mapvkVkToChar = 2;          // MAPVK_VK_TO_CHAR
    private const uint _deadKeyFlag = 0x80000000;   // top bit set => the key is a dead key

    /// <summary>
    /// The keycap tokens for <paramref name="hotkey"/>, with the key glyph resolved from the active
    /// keyboard layout where it is a character key.
    /// </summary>
    public static IReadOnlyList<string> ToLayoutTokens(Hotkey hotkey)
    {
        IReadOnlyList<string> tokens = hotkey.ToDisplayTokens();
        if (tokens.Count == 0 || !IsCharacterKey(hotkey.Key) || ResolveGlyph(hotkey.Key) is not { } glyph)
        {
            return tokens;
        }

        List<string> refined = [.. tokens];
        refined[^1] = glyph;
        return refined;
    }

    // Character-producing keys whose printed glyph can differ by layout: digits, letters, and the
    // OEM punctuation ranges. Everything else (arrows, F-keys, Space, …) keeps its symbolic label.
    private static bool IsCharacterKey(uint vk) =>
        vk is (>= 0x30 and <= 0x39)  // 0-9
            or (>= 0x41 and <= 0x5A) // A-Z
            or (>= 0xBA and <= 0xC0) // OEM ; = , - . / `
            or (>= 0xDB and <= 0xDF) // OEM [ \ ] '
            or 0xE2;                 // OEM_102 (the extra <> / \| key on some layouts)

    private static string? ResolveGlyph(uint vk)
    {
        uint mapped = MapVirtualKeyExW(vk, _mapvkVkToChar, GetKeyboardLayout(0));
        if (mapped == 0 || (mapped & _deadKeyFlag) != 0)
        {
            return null;
        }

        char c = (char)(mapped & 0xFFFF);
        return char.IsWhiteSpace(c) || char.IsControl(c) ? null : char.ToUpperInvariant(c).ToString();
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint MapVirtualKeyExW(uint uCode, uint uMapType, nint dwhkl);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetKeyboardLayout(uint idThread);
}
