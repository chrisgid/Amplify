using System.Collections.Generic;
using System.Runtime.InteropServices;
using Amplify.App.Spotify;
using Amplify.App.Theming;
using Amplify.App.ViewModels;
using Amplify.App.Views;
using Amplify.Core.Navigation;
using Amplify.Core.Settings;
using Amplify.Core.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Amplify.App;

/// <summary>
/// The single main window. It owns the window chrome (Mica backdrop, custom title bar, sensible
/// size/min-size) and the content <see cref="Frame"/>, and drives it from the shell view-model's
/// route. Global hotkeys are owned by the hotkey service, independent of this window.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    // A compact, tall window. The width is sized so the settings list's native cards
    // (CommunityToolkit SettingsCard) stay in their single-row layout — those cards drop the action
    // control below the text once they're narrower than ~476px, so the content area is kept above
    // that, and the minimum width holds the same floor while staying resizable.
    private const int _initialWidth = 600;
    private const int _initialHeight = 760;
    private const int _minWidth = 560;
    private const int _minHeight = 480;

    private readonly ShellViewModel _shell;
    private readonly PlayerStateProvider _playerState;
    private readonly ThemeService _theme;
    private readonly ISettingsService _settings;

    // The window's last normal footprint (device pixels), captured as the user moves/resizes and
    // written back to settings when the window is put away or closed. Null until the first change.
    private WindowState? _pendingWindow;

    // Coalesces the stream of move/resize changes into a single save shortly after the user stops, so
    // the position also survives a hard kill (e.g. stopping the debugger) — not just a graceful close.
    private readonly DispatcherQueueTimer _persistDebounce;
    private bool _disposed;

    public MainWindow(
        ShellViewModel shell, PlayerStateProvider playerState, ThemeService theme, ISettingsService settings)
    {
        _shell = shell;
        _playerState = playerState;
        _theme = theme;
        _settings = settings;
        InitializeComponent();

        ConfigureWindowChrome();
        ApplyTheme();

        _persistDebounce = DispatcherQueue.CreateTimer();
        _persistDebounce.Interval = TimeSpan.FromSeconds(1);
        _persistDebounce.IsRepeating = false;
        _persistDebounce.Tick += OnPersistDebounceTick;

        _shell.RouteChanged += OnShellRouteChanged;
        _theme.ThemeChanged += OnThemeChanged;
        VisibilityChanged += OnVisibilityChanged;
        // Subscribed after the initial placement so only user-driven moves/resizes are remembered.
        AppWindow.Changed += OnAppWindowChanged;
        Closed += OnClosed;

        // Show the screen the shell picked for the current connection state. Player-state polling
        // (which the status card and volume controller both consume) is paused/resumed with visibility.
        NavigateTo(_shell.CurrentRoute);
    }

    private void ConfigureWindowChrome()
    {
        // Mica falls back to a solid themed colour automatically where it isn't supported.
        SystemBackdrop = new MicaBackdrop();

        // Replace the system title bar with our custom one (must be enabled in code, not XAML).
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        PositionWindow();
    }

    // Restore the remembered window footprint, or — on first run or when it's no longer on-screen —
    // open at the default size centred on the current display. WinUI never centres new windows itself:
    // without an explicit placement the OS cascade-places the top-left corner, which reads as the
    // window opening "off to the left". Sizes/coordinates are device pixels (the AppWindow space); the
    // logical min/default constants are scaled by the DPI of the monitor the window will actually land
    // on — NOT the monitor it was constructed on — so restoring onto a different-DPI display doesn't
    // mis-size it (the presenter minimum enforces on programmatic resizes too, so it must match).
    private void PositionWindow()
    {
        if (_settings.Current.Window is { } saved)
        {
            double targetScale = ScaleForPoint(saved.X, saved.Y);
            int minWidth = (int)(_minWidth * targetScale);
            int minHeight = (int)(_minHeight * targetScale);
            if (WindowPlacement.TryGetRestoreBounds(saved, ReadWorkAreas(), minWidth, minHeight, out PixelRect restored))
            {
                SetMinimumSize(minWidth, minHeight);
                AppWindow.MoveAndResize(new RectInt32(restored.X, restored.Y, restored.Width, restored.Height));
                return;
            }
        }

        // First run or off-screen placement: open on the monitor the window was created on.
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        SetMinimumSize((int)(_minWidth * scale), (int)(_minHeight * scale));

        RectInt32 primary = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        PixelRect centred = WindowPlacement.Center(
            (int)(_initialWidth * scale),
            (int)(_initialHeight * scale),
            new PixelRect(primary.X, primary.Y, primary.Width, primary.Height));
        AppWindow.MoveAndResize(new RectInt32(centred.X, centred.Y, centred.Width, centred.Height));
    }

    private void SetMinimumSize(int width, int height)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = width;
            presenter.PreferredMinimumHeight = height;
        }
    }

    // The effective-DPI scale of the monitor containing a screen point, so a window's min/default
    // sizes track the display it will occupy rather than the one it was constructed on. Falls back to
    // the nearest monitor for an off-screen point, and to 1.0 if the DPI query fails.
    private static double ScaleForPoint(int x, int y)
    {
        nint monitor = MonitorFromPoint(new POINT { X = x, Y = y }, _monitorDefaultToNearest);
        return GetDpiForMonitor(monitor, _mdtEffectiveDpi, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
    }

    // Snapshot each display's work area. DisplayArea.FindAll() returns a projected WinRT
    // IReadOnlyList; enumerating it with LINQ/foreach makes CsWinRT QueryInterface the underlying
    // object for IEnumerable<T>, which fails with InvalidCastException ("Specified cast is not valid")
    // on this Windows App SDK. Indexing by position goes through the list's own interface and marshals
    // cleanly, so read it with a plain indexed loop into a managed list.
    private static List<PixelRect> ReadWorkAreas()
    {
        IReadOnlyList<DisplayArea> displays = DisplayArea.FindAll();
        var areas = new List<PixelRect>(displays.Count);
        for (int i = 0; i < displays.Count; i++)
        {
            RectInt32 area = displays[i].WorkArea;
            areas.Add(new PixelRect(area.X, area.Y, area.Width, area.Height));
        }

        return areas;
    }

    // The theme service raises ThemeChanged on the UI thread (it owns marshalling its off-thread
    // settings/OS sources), so the appearance can be applied directly.
    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme();

    // Drive the content root's theme from the resolved preference. ElementTheme.Default follows the
    // OS live; Light/Dark pin it. The root carries the Mica backdrop and title bar along, and system
    // brushes pick up the OS accent automatically.
    private void ApplyTheme()
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = _theme.CurrentTheme;
        }
    }

    private void OnShellRouteChanged(object? sender, ShellRoute route) => NavigateTo(route);

    private void NavigateTo(ShellRoute route)
    {
        switch (route)
        {
            case ShellRoute.Onboarding:
                ContentFrame.Navigate(typeof(OnboardingPage));
                ContentFrame.BackStack.Clear();
                break;

            case ShellRoute.Main:
                // Returning from settings reuses the cached main page so its state is preserved;
                // a top-level switch (e.g. just connected) navigates fresh and drops the back stack.
                if (ContentFrame.CurrentSourcePageType == typeof(SettingsPage) && ContentFrame.CanGoBack)
                {
                    ContentFrame.GoBack();
                }
                else
                {
                    ContentFrame.Navigate(typeof(MainPage));
                    ContentFrame.BackStack.Clear();
                }

                break;

            case ShellRoute.Settings:
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
        }
    }

    // Minimising fires this with Visible=false (and restoring with true) — used to pause the shared
    // Spotify player-state polling while nobody can see it, and to read once on the way back so the
    // status card and volume meter catch up on anything changed while hidden. Note: this only covers
    // OS-minimise; once feature 08 adds minimise-to-tray, a fully tray-hidden window is a separate
    // non-visible state that this same Suspend()/Resume() pair will need to cover too (check whether
    // VisibilityChanged already fires for that case before adding new plumbing).
    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _playerState.Resume();
        }
        else
        {
            _playerState.Suspend();
            // Putting the window away (minimise or hide-to-tray) is a natural, low-frequency save point,
            // and persisting here means a later crash still keeps the last placement.
            PersistWindowState();
        }
    }

    // Remember the window's last normal footprint. Minimised/maximised states report placeholder
    // coordinates, so only the Restored state is captured; the value is persisted later (hide/close).
    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((args.DidPositionChange || args.DidSizeChange)
            && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            _pendingWindow = new WindowState(
                sender.Size.Width, sender.Size.Height, sender.Position.X, sender.Position.Y);

            // Restart the countdown: the save fires once the moves/resizes stop, not on every step.
            _persistDebounce.Stop();
            _persistDebounce.Start();
        }
    }

    private void OnPersistDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        PersistWindowState();
    }

    private void PersistWindowState()
    {
        if (_pendingWindow is { } state && state != _settings.Current.Window)
        {
            _settings.Update(s => s.Window = state);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    /// <summary>Detaches the window's event subscriptions.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Final flush before teardown: on the Quit path the window closes while still Restored, so any
        // move/resize not yet saved by the debounce, a hide, or a minimise is captured here. Settings
        // outlive the window (the host is disposed after this Closed handler), so the write is safe.
        _persistDebounce.Stop();
        _persistDebounce.Tick -= OnPersistDebounceTick;
        PersistWindowState();
        _shell.RouteChanged -= OnShellRouteChanged;
        _theme.ThemeChanged -= OnThemeChanged;
        VisibilityChanged -= OnVisibilityChanged;
        AppWindow.Changed -= OnAppWindowChanged;
    }

    private const uint _monitorDefaultToNearest = 2;  // MONITOR_DEFAULTTONEAREST
    private const int _mdtEffectiveDpi = 0;            // MDT_EFFECTIVE_DPI

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
