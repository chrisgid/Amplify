using System.Runtime.InteropServices;
using Amplify.Core.Hotkeys;
using Microsoft.UI.Dispatching;

namespace Amplify.App.Hotkeys;

/// <summary>
/// Raises <see cref="HotkeyPressed"/> for registered global shortcuts using a low-level keyboard
/// hook (<c>WH_KEYBOARD_LL</c>). Unlike <c>RegisterHotKey</c>, the hook observes a key without
/// consuming it — every press is passed on to the foreground application — so a shortcut that is also
/// meaningful in another app keeps working there while still driving Amplify. The hook is global, so
/// shortcuts fire regardless of focus, including while Amplify is in the tray.
/// </summary>
/// <remarks>
/// The hook is installed on, and its callback runs on, the thread that first registers a hotkey (the
/// UI thread during startup) — that thread owns the message pump the system uses to deliver hook
/// calls. The callback must return quickly or Windows drops it, so it does the minimum (track
/// modifier state, match, defer the event) and never blocks. Modifier state is tracked from the
/// hook's own key events rather than queried, because a low-level hook runs before the async key
/// state is updated. A non-elevated process can't observe input destined for an elevated window
/// (Windows UIPI); shortcuts won't fire while such a window is focused.
/// </remarks>
public sealed class KeyboardHookHotkeyService : IHotkeyService, IDisposable
{
    private const int _whKeyboardLl = 13;
    private const int _hcAction = 0;
    private const int _wmKeyDown = 0x0100;
    private const int _wmKeyUp = 0x0101;
    private const int _wmSysKeyDown = 0x0104;   // generated for key presses while Alt is held
    private const int _wmSysKeyUp = 0x0105;

    private readonly DispatcherQueue _dispatcher;
    // Kept in a field so the GC can't collect the delegate while the hook still calls into it.
    private readonly HookProc _hookProc;
    private readonly Dictionary<HotkeyAction, Hotkey> _registered = [];
    // The specific modifier keys (incl. left/right variants) currently held, so the combo's modifier
    // set can be reconstructed when a non-modifier key is pressed.
    private readonly HashSet<uint> _modifiersDown = [];
    // Non-modifier keys currently held that already matched, so auto-repeat fires the action only once.
    private readonly HashSet<uint> _heldMatched = [];

    private nint _hookHandle;
    private bool _disposed;

    public KeyboardHookHotkeyService()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "The hotkey service must be created on a UI thread with a dispatcher queue.");
        _hookProc = HookCallback;
    }

    /// <inheritdoc />
    public event EventHandler<HotkeyAction>? HotkeyPressed;

    /// <inheritdoc />
    public void Register(HotkeyAction action, Hotkey combo)
    {
        if (!TryRegister(action, combo))
        {
            throw new InvalidOperationException(
                $"Couldn't register the hotkey '{combo.ToCanonicalString()}'.");
        }
    }

    /// <inheritdoc />
    public bool TryRegister(HotkeyAction action, Hotkey combo)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The hook doesn't reserve the combination from the OS, so registration only fails if the hook
        // itself can't be installed (a catastrophic, all-or-nothing condition). Keep the previous
        // binding in that case.
        if (!EnsureHook())
        {
            return false;
        }

        _registered[action] = combo;
        return true;
    }

    /// <inheritdoc />
    public void Unregister(HotkeyAction action) => _registered.Remove(action);

    private bool EnsureHook()
    {
        if (_hookHandle != 0)
        {
            return true;
        }

        _hookHandle = SetWindowsHookExW(_whKeyboardLl, _hookProc, GetModuleHandleW(null), 0);
        return _hookHandle != 0;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode == _hcAction)
        {
            int message = (int)wParam;
            uint vk = (uint)Marshal.ReadInt32(lParam);  // KBDLLHOOKSTRUCT.vkCode is the first field
            bool isModifier = Hotkey.IsModifierVirtualKey(vk);

            if (message is _wmKeyDown or _wmSysKeyDown)
            {
                if (isModifier)
                {
                    _modifiersDown.Add(vk);
                }
                else
                {
                    HandleKeyDown(vk);
                }
            }
            else if (message is _wmKeyUp or _wmSysKeyUp)
            {
                if (isModifier)
                {
                    _modifiersDown.Remove(vk);
                }
                else
                {
                    _heldMatched.Remove(vk);
                }
            }
        }

        // Always pass the key on so the foreground application still receives it.
        return CallNextHookEx(0, nCode, wParam, lParam);
    }

    private void HandleKeyDown(uint vk)
    {
        // Collapse auto-repeat so a held combo fires once.
        if (_heldMatched.Contains(vk))
        {
            return;
        }

        var pressed = new Hotkey(CurrentModifiers(), vk);
        foreach ((HotkeyAction action, Hotkey combo) in _registered)
        {
            if (combo == pressed)
            {
                _heldMatched.Add(vk);
                // Defer so the hook callback returns immediately; the handler runs on the next UI tick.
                _dispatcher.TryEnqueue(() => HotkeyPressed?.Invoke(this, action));
                break;
            }
        }
    }

    private KeyModifiers CurrentModifiers()
    {
        KeyModifiers modifiers = KeyModifiers.None;
        foreach (uint vk in _modifiersDown)
        {
            modifiers |= ModifierFlagFor(vk);
        }

        return modifiers;
    }

    // Maps a modifier virtual-key code (generic or left/right variant) to its flag.
    private static KeyModifiers ModifierFlagFor(uint vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => KeyModifiers.Shift,    // Shift, L/R Shift
        0x11 or 0xA2 or 0xA3 => KeyModifiers.Ctrl,     // Control, L/R Control
        0x12 or 0xA4 or 0xA5 => KeyModifiers.Alt,      // Menu (Alt), L/R Menu
        0x5B or 0x5C => KeyModifiers.Win,              // L/R Windows
        _ => KeyModifiers.None,
    };

    /// <summary>Removes the keyboard hook.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        _registered.Clear();
        _modifiersDown.Clear();
        _heldMatched.Clear();
    }

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetModuleHandleW(string? lpModuleName);
}
