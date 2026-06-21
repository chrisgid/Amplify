using System.Runtime.InteropServices;

namespace Amplify.App.Interop;

/// <summary>
/// Registers a fixed pair of system-wide volume hotkeys (<c>Ctrl+Alt+Up</c> / <c>Ctrl+Alt+Down</c>)
/// against a window and raises <see cref="VolumeNudged"/> when one is pressed — even while the app is
/// in the background, which is what <c>RegisterHotKey</c> provides. It hooks the window's message loop
/// with a comctl32 subclass so the hotkey messages can be observed from managed code.
/// </summary>
/// <remarks>
/// Throwaway scaffolding for the end-to-end volume slice: the hotkeys are hard-coded and there is no
/// rebinding, conflict handling, persistence, or low-level-hook fallback. A real hotkey service
/// replaces this later. Construct, subscribe, then <see cref="Register"/> on the window's UI thread;
/// <see cref="Dispose"/> on close to unregister and unhook.
/// </remarks>
internal sealed class GlobalHotkeyWindow : IDisposable
{
    private const int _wmHotkey = 0x0312;
    private const uint _modAlt = 0x0001;
    private const uint _modControl = 0x0002;
    private const uint _modNoRepeat = 0x4000;   // collapse keyboard auto-repeat into a single message
    private const uint _vkUp = 0x26;
    private const uint _vkDown = 0x28;
    private const int _hotkeyVolumeUpId = 1;
    private const int _hotkeyVolumeDownId = 2;
    private static readonly nuint _subclassId = 1;

    private readonly nint _hwnd;
    // Kept in a field so the GC can't collect the delegate while the native subclass still calls it.
    private readonly SubclassProc _subclassProc;
    private bool _registered;
    private bool _disposed;

    public GlobalHotkeyWindow(nint hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = HandleMessage;
    }

    /// <summary>Raised when a volume hotkey fires: <c>+1</c> for volume up, <c>-1</c> for volume down.</summary>
    public event EventHandler<int>? VolumeNudged;

    /// <summary>
    /// Hooks the window and registers the hotkeys. Must run on the thread that owns the window.
    /// Idempotent — a second call is a no-op.
    /// </summary>
    public void Register()
    {
        if (_registered)
        {
            return;
        }

        if (!SetWindowSubclass(_hwnd, _subclassProc, _subclassId, 0))
        {
            throw new InvalidOperationException("Couldn't hook the window message loop for global hotkeys.");
        }

        bool up = RegisterHotKey(_hwnd, _hotkeyVolumeUpId, _modControl | _modAlt | _modNoRepeat, _vkUp);
        bool down = RegisterHotKey(_hwnd, _hotkeyVolumeDownId, _modControl | _modAlt | _modNoRepeat, _vkDown);
        if (!up || !down)
        {
            // Roll back so we don't leak a half-registered state (e.g. another app owns one combo).
            RemoveWindowSubclass(_hwnd, _subclassProc, _subclassId);
            throw new InvalidOperationException(
                "Couldn't register the volume hotkeys (Ctrl+Alt+Up / Ctrl+Alt+Down may be in use).");
        }

        _registered = true;
    }

    private nint HandleMessage(nint hWnd, uint msg, nint wParam, nint lParam, nuint idSubclass, nuint refData)
    {
        if (msg == _wmHotkey)
        {
            int direction = (int)wParam switch
            {
                _hotkeyVolumeUpId => 1,
                _hotkeyVolumeDownId => -1,
                _ => 0,
            };
            if (direction != 0)
            {
                VolumeNudged?.Invoke(this, direction);
            }
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            UnregisterHotKey(_hwnd, _hotkeyVolumeUpId);
            UnregisterHotKey(_hwnd, _hotkeyVolumeDownId);
            RemoveWindowSubclass(_hwnd, _subclassProc, _subclassId);
            _registered = false;
        }
    }

    private delegate nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint idSubclass, nuint refData);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint DefSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam);
}
