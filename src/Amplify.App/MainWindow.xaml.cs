using System.Collections.Generic;
using System.Runtime.InteropServices;
using Amplify.App.Spotify;
using Amplify.App.Theming;
using Amplify.App.ViewModels;
using Amplify.App.Views;
using Amplify.Core.Navigation;
using Amplify.Core.Settings;
using Amplify.Core.Theming;
using Amplify.Core.Windowing;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Graphics;
using Windows.UI;
using ThemeSettings = Microsoft.UI.System.ThemeSettings;

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
    private const int _initialHeight = 564;
    private const int _minWidth = 600;
    private const int _minHeight = 564;

    private readonly ShellViewModel _shell;
    private readonly PlayerStateProvider _playerState;
    private readonly ThemeService _theme;
    private readonly ISettingsService _settings;

    // Watches the OS contrast-theme setting for the caption buttons (see ApplyCaptionButtonColors).
    // It needs a WindowId, which is why it lives here rather than in ThemeService — that service
    // deliberately holds no UI reference. Kept in a field for the same reason as the service's
    // UISettings: the Changed event stops firing once the object is collected.
    private readonly ThemeSettings _themeSettings;

    // The default title-bar identity (app name + logo), captured from XAML so it can be swapped for the
    // Settings screen's own title + back button and restored on the way back.
    private readonly IconSource? _appTitleBarIcon;
    private readonly string _appTitle;
    private readonly string _settingsTitle;

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

        // Capture the default title-bar identity before a route can swap it for the Settings variant.
        _appTitleBarIcon = AppTitleBar.IconSource;
        _appTitle = AppTitleBar.Title;
        _settingsTitle = new ResourceLoader().GetString("Settings_Title/Text");

        ConfigureWindowChrome();
        _themeSettings = ThemeSettings.CreateForWindowId(AppWindow.Id);
        ApplyTheme();

        _persistDebounce = DispatcherQueue.CreateTimer();
        _persistDebounce.Interval = TimeSpan.FromSeconds(1);
        _persistDebounce.IsRepeating = false;
        _persistDebounce.Tick += OnPersistDebounceTick;

        _shell.RouteChanged += OnShellRouteChanged;
        _theme.ThemeChanged += OnThemeChanged;
        _themeSettings.Changed += OnThemeSettingsChanged;
        VisibilityChanged += OnVisibilityChanged;
        // The title bar's built-in back button drives the same back navigation as the on-screen route.
        AppTitleBar.BackRequested += OnTitleBarBackRequested;
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

        // Give the window its own HICON. The custom title bar draws its glyph from the
        // TitleBar.IconSource, and the taskbar button + jump list are drawn by the shell from the
        // packaged Square44x44Logo — but the taskbar thumbnail preview, Alt+Tab, and Task View read the
        // window's HICON, which otherwise falls back to the generic framework icon. SetIcon wants a
        // fully-qualified path to a .ico shipped as content.
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

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

    // Turning a contrast theme on or off changes which colours the caption buttons should use. Unlike
    // ThemeService's event this one arrives straight from the OS, so it marshals itself to the UI
    // thread rather than assuming it is already there.
    private void OnThemeSettingsChanged(ThemeSettings sender, object args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyTheme();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyTheme);
        }
    }

    // Drive the content root's theme from the resolved preference. ElementTheme.Default follows the
    // OS live; Light/Dark pin it. The root carries the Mica backdrop and the TitleBar control's own
    // title/icon along, and system brushes pick up the OS accent automatically — but NOT the system
    // caption buttons, which are outside the XAML tree and are coloured separately below.
    private void ApplyTheme()
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = _theme.CurrentTheme;
        }

        ApplyCaptionButtonColors();
    }

    // Colour the Minimise/Maximise/Close buttons to match the app's theme. The TitleBar control only
    // reserves space for them; they are drawn by the system via AppWindowTitleBar, so RequestedTheme
    // never reaches them and they otherwise keep the OS's own light/dark glyphs. Called from
    // ApplyTheme so it re-runs on every theme change, not just at window creation.
    //
    // Backgrounds stay transparent so the Mica backdrop shows through — the alpha channel is honoured
    // only because the content is extended into the title bar. The Close button's hover and pressed
    // backgrounds are system-defined (the red) and can't be overridden; that's by design.
    private void ApplyCaptionButtonColors()
    {
        // False only on Windows 10 builds that predate title bar customisation, where the system
        // draws its own themed buttons anyway.
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        AppWindowTitleBar titleBar = AppWindow.TitleBar;

        // Under a contrast theme, hand the buttons back to the system — null resets each property to
        // its system colour. The OS palette is the accessible one and the user may have customised it,
        // so our own colours would be actively harmful: a Light override under a dark contrast theme
        // would paint near-black glyphs onto a black title bar. This resets rather than skips, so
        // switching a contrast theme *on* while running clears colours set before it.
        if (_themeSettings.HighContrast)
        {
            titleBar.ButtonBackgroundColor = null;
            titleBar.ButtonInactiveBackgroundColor = null;
            titleBar.ButtonForegroundColor = null;
            titleBar.ButtonHoverForegroundColor = null;
            titleBar.ButtonPressedForegroundColor = null;
            titleBar.ButtonInactiveForegroundColor = null;
            titleBar.ButtonHoverBackgroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            return;
        }

        bool dark = _theme.EffectiveTheme == ResolvedTheme.Dark;

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        Color glyph = dark ? _darkGlyph : _lightGlyph;
        titleBar.ButtonForegroundColor = glyph;
        titleBar.ButtonHoverForegroundColor = glyph;
        titleBar.ButtonPressedForegroundColor = glyph;
        titleBar.ButtonInactiveForegroundColor = dark ? _darkInactiveGlyph : _lightInactiveGlyph;

        titleBar.ButtonHoverBackgroundColor = dark ? _darkHover : _lightHover;
        titleBar.ButtonPressedBackgroundColor = dark ? _darkPressed : _lightPressed;
    }

    private void OnShellRouteChanged(object? sender, ShellRoute route) => NavigateTo(route);

    private void NavigateTo(ShellRoute route)
    {
        ApplyTitleBar(route);

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

    // The Settings screen shows its title and a back button in the window title bar (replacing the app
    // name + logo); every other route restores the default identity and hides the back button.
    private void ApplyTitleBar(ShellRoute route)
    {
        bool isSettings = route == ShellRoute.Settings;
        AppTitleBar.IsBackButtonVisible = isSettings;
        AppTitleBar.Title = isSettings ? _settingsTitle : _appTitle;
        AppTitleBar.IconSource = isSettings ? null : _appTitleBarIcon;
    }

    private void OnTitleBarBackRequested(TitleBar sender, object args) => _shell.GoBackCommand.Execute(null);

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
        _themeSettings.Changed -= OnThemeSettingsChanged;
        VisibilityChanged -= OnVisibilityChanged;
        AppTitleBar.BackRequested -= OnTitleBarBackRequested;
        AppWindow.Changed -= OnAppWindowChanged;
    }

    // Caption button colours, per theme. The glyph tones mirror the Fluent primary/disabled text
    // fills flattened onto their backdrop (the foreground properties ignore the alpha channel, so
    // they have to be opaque), and the hover/pressed washes follow the Fluent subtle-fill roles:
    // a translucent black over light, white over dark, with pressed lighter than hover.
    private static readonly Color _lightGlyph = Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B);
    private static readonly Color _darkGlyph = Colors.White;
    private static readonly Color _lightInactiveGlyph = Color.FromArgb(0xFF, 0x99, 0x99, 0x99);
    private static readonly Color _darkInactiveGlyph = Color.FromArgb(0xFF, 0x76, 0x76, 0x76);
    private static readonly Color _lightHover = Color.FromArgb(0x19, 0x00, 0x00, 0x00);
    private static readonly Color _darkHover = Color.FromArgb(0x19, 0xFF, 0xFF, 0xFF);
    private static readonly Color _lightPressed = Color.FromArgb(0x0D, 0x00, 0x00, 0x00);
    private static readonly Color _darkPressed = Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF);

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
