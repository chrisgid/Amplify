using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Amplify.Core.Hotkeys;

/// <summary>
/// A global keyboard shortcut: a set of <see cref="KeyModifiers"/> plus a single non-modifier key,
/// identified by its Win32 virtual-key code. The virtual-key code is the stable identity used for
/// registration and persistence; it is independent of the keyboard layout (e.g. the code for "Z" is
/// the same on every layout, even though the physical key and printed glyph may differ).
/// </summary>
/// <param name="Modifiers">The modifier keys held alongside <paramref name="Key"/>.</param>
/// <param name="Key">The Win32 virtual-key code of the non-modifier key.</param>
public sealed record Hotkey(KeyModifiers Modifiers, uint Key)
{
    // The order modifiers appear in both the canonical string and the display tokens.
    private static readonly (KeyModifiers Flag, string Canonical, string Display)[] _modifierOrder =
    [
        (KeyModifiers.Ctrl, "ctrl", "Ctrl"),
        (KeyModifiers.Alt, "alt", "Alt"),
        (KeyModifiers.Shift, "shift", "Shift"),
        (KeyModifiers.Win, "win", "Win"),
    ];

    // Virtual-key codes that are themselves modifiers (generic plus left/right variants). A capture
    // holding only these isn't a usable hotkey, so they're rejected as the "key" of a combo.
    private static readonly HashSet<uint> _modifierVirtualKeys =
    [
        0x10, 0x11, 0x12,                   // Shift, Control, Menu (Alt)
        0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, // L/R Shift, Control, Menu
        0x5B, 0x5C,                          // Left/Right Windows
    ];

    private static readonly Dictionary<uint, (string Canonical, string Display)> _namedKeys = BuildNamedKeys();
    private static readonly Dictionary<string, uint> _canonicalToVk = BuildReverseLookup();

    /// <summary>
    /// Whether this is a usable global hotkey: it has a non-modifier key, with or without modifiers.
    /// A single key (e.g. F11) is allowed; a modifier-only press (just Ctrl/Alt/Shift/Win) is not.
    /// </summary>
    public bool IsValid => Key != 0 && !IsModifierVirtualKey(Key);

    /// <summary>
    /// Builds a hotkey from a captured modifier set and key, rejecting combinations that aren't
    /// usable — no key, or a modifier-only press (e.g. just Ctrl+Alt with no other key). A bare
    /// non-modifier key (e.g. F11 on its own) is accepted.
    /// </summary>
    /// <returns><c>true</c> and the hotkey when valid; otherwise <c>false</c>.</returns>
    public static bool TryCreate(KeyModifiers modifiers, uint key, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        var candidate = new Hotkey(modifiers, key);
        if (candidate.IsValid)
        {
            hotkey = candidate;
            return true;
        }

        hotkey = null;
        return false;
    }

    /// <summary>Whether <paramref name="vk"/> is itself a modifier key (Ctrl/Alt/Shift/Win).</summary>
    public static bool IsModifierVirtualKey(uint vk) => _modifierVirtualKeys.Contains(vk);

    /// <summary>
    /// The stable, persisted form, e.g. <c>ctrl+alt+arrowup</c>. Modifiers come first in a fixed
    /// order, then the key. Keys without a friendly name fall back to <c>vk{code}</c> so any
    /// virtual-key code round-trips through <see cref="TryParse"/>.
    /// </summary>
    public string ToCanonicalString()
    {
        IEnumerable<string> parts = _modifierOrder
            .Where(m => Modifiers.HasFlag(m.Flag))
            .Select(m => m.Canonical)
            .Append(_namedKeys.TryGetValue(Key, out (string Canonical, string Display) named)
                ? named.Canonical
                : $"vk{Key.ToString(CultureInfo.InvariantCulture)}");
        return string.Join('+', parts);
    }

    /// <summary>
    /// The layout-independent keycap tokens for display, e.g. <c>["Ctrl", "Alt", "↑"]</c>. The final
    /// token is anchored to the virtual key's US/invariant name; callers that want the glyph printed
    /// on the user's actual keyboard layout refine this last token from the active layout.
    /// </summary>
    public IReadOnlyList<string> ToDisplayTokens()
    {
        List<string> tokens = _modifierOrder
            .Where(m => Modifiers.HasFlag(m.Flag))
            .Select(m => m.Display)
            .ToList();
        tokens.Add(_namedKeys.TryGetValue(Key, out (string Canonical, string Display) named)
            ? named.Display
            : $"VK{Key.ToString(CultureInfo.InvariantCulture)}");
        return tokens;
    }

    /// <summary>
    /// Parses the canonical form produced by <see cref="ToCanonicalString"/>. Tolerant of casing and
    /// modifier order. Returns <c>false</c> for empty input, an unknown token, or no key.
    /// </summary>
    public static bool TryParse(string? canonical, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return false;
        }

        var modifiers = KeyModifiers.None;
        uint? key = null;
        foreach (string raw in canonical.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string token = raw.ToLowerInvariant();
            (KeyModifiers Flag, string Canonical, string Display) modifier =
                Array.Find(_modifierOrder, m => m.Canonical == token);
            if (modifier.Flag != KeyModifiers.None)
            {
                modifiers |= modifier.Flag;
                continue;
            }

            // A second key token means the string is malformed.
            if (key is not null)
            {
                return false;
            }

            if (_canonicalToVk.TryGetValue(token, out uint vk))
            {
                key = vk;
            }
            else if (token.StartsWith("vk", StringComparison.Ordinal)
                && uint.TryParse(token.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out uint rawVk))
            {
                key = rawVk;
            }
            else
            {
                return false;
            }
        }

        if (key is null)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, key.Value);
        return true;
    }

    private static Dictionary<uint, (string Canonical, string Display)> BuildNamedKeys()
    {
        var map = new Dictionary<uint, (string, string)>
        {
            [0x25] = ("arrowleft", "←"),
            [0x26] = ("arrowup", "↑"),
            [0x27] = ("arrowright", "→"),
            [0x28] = ("arrowdown", "↓"),
            [0x20] = ("space", "Space"),
            [0x0D] = ("enter", "Enter"),
            [0x09] = ("tab", "Tab"),
            [0x08] = ("backspace", "Backspace"),
            [0x2E] = ("delete", "Del"),
            [0x2D] = ("insert", "Ins"),
            [0x24] = ("home", "Home"),
            [0x23] = ("end", "End"),
            [0x21] = ("pageup", "PgUp"),
            [0x22] = ("pagedown", "PgDn"),
            [0x1B] = ("escape", "Esc"),
        };

        // Letters A–Z (VK == ASCII uppercase) and the top-row digits 0–9.
        for (uint vk = 'A'; vk <= 'Z'; vk++)
        {
            map[vk] = (((char)(vk + 32)).ToString(), ((char)vk).ToString());
        }

        for (uint vk = '0'; vk <= '9'; vk++)
        {
            string digit = ((char)vk).ToString();
            map[vk] = (digit, digit);
        }

        // Numpad digits VK_NUMPAD0..9 (0x60–0x69).
        for (uint i = 0; i <= 9; i++)
        {
            string n = i.ToString(CultureInfo.InvariantCulture);
            map[0x60 + i] = ($"num{n}", $"Num {n}");
        }

        // Function keys F1..F24 (VK_F1 0x70 .. VK_F24 0x87).
        for (uint i = 1; i <= 24; i++)
        {
            string f = i.ToString(CultureInfo.InvariantCulture);
            map[0x70 + (i - 1)] = ($"f{f}", $"F{f}");
        }

        return map;
    }

    private static Dictionary<string, uint> BuildReverseLookup()
    {
        var reverse = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach ((uint vk, (string canonical, _)) in _namedKeys)
        {
            reverse[canonical] = vk;
        }

        return reverse;
    }
}
