using System.Runtime.InteropServices;
using Amplify.App.Dev;
using Amplify.App.Interop;
using Amplify.App.Theming;
using Amplify.App.ViewModels;
using Amplify.App.Views;
using Amplify.Core.Auth;
using Amplify.Core.Navigation;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Amplify.App;

/// <summary>
/// The single main window. It owns the window chrome (Mica backdrop, custom title bar, sensible
/// size/min-size) and the content <see cref="Frame"/>, and drives it from the shell view-model's
/// route. It also owns the lifetime of the global volume hotkeys, arming them once an account is
/// connected and releasing them when the window closes.
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
    private readonly IAuthService _authService;
    private readonly DevPlaybackSlice _playback;
    private readonly ThemeService _theme;
    private readonly DispatcherQueue _dispatcher;

    private GlobalHotkeyWindow? _hotkeys;
    private bool _disposed;

    public MainWindow(ShellViewModel shell, IAuthService authService, DevPlaybackSlice playback, ThemeService theme)
    {
        _shell = shell;
        _authService = authService;
        _playback = playback;
        _theme = theme;
        InitializeComponent();
        _dispatcher = DispatcherQueue;

        ConfigureWindowChrome();
        ApplyTheme();

        _shell.RouteChanged += OnShellRouteChanged;
        _authService.ConnectionStateChanged += OnConnectionStateChanged;
        _theme.ThemeChanged += OnThemeChanged;
        Closed += OnClosed;

        // Show the screen the shell picked for the current connection state.
        NavigateTo(_shell.CurrentRoute);

        // A session restored before this window existed won't re-raise ConnectionStateChanged, so the
        // initial connected state has to be handled here too — otherwise the global hotkeys would
        // never arm on a launch that opens straight on the main screen.
        if (_authService.State == ConnectionState.Connected)
        {
            OnConnected();
        }
    }

    private void ConfigureWindowChrome()
    {
        // Mica falls back to a solid themed colour automatically where it isn't supported.
        SystemBackdrop = new MicaBackdrop();

        // Replace the system title bar with our custom one (must be enabled in code, not XAML).
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(_initialWidth * scale), (int)(_initialHeight * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(_minWidth * scale);
            presenter.PreferredMinimumHeight = (int)(_minHeight * scale);
        }
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

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        if (state == ConnectionState.Connected)
        {
            // Connection-state changes can arrive off the UI thread; arming hotkeys touches the window.
            _dispatcher.TryEnqueue(OnConnected);
        }
    }

    // Arms the global hotkeys (idempotent) and refreshes the playback state once an account is
    // connected — whether that connection was just made or restored at launch. Runs on the UI thread.
    private void OnConnected()
    {
        ArmHotkeys();
        _ = _playback.RefreshAsync();
    }

    private void ArmHotkeys()
    {
        if (_hotkeys is not null)
        {
            return;
        }

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _hotkeys = new GlobalHotkeyWindow(hwnd);
        // Hotkey messages arrive on the window's UI thread, so the nudge can run directly.
        _hotkeys.VolumeNudged += (_, direction) => _ = _playback.NudgeAsync(direction);
        try
        {
            _hotkeys.Register();
        }
        catch (InvalidOperationException)
        {
            // Another app may own the combo; the on-screen buttons still work. A real hotkey service
            // surfaces conflicts to the user later.
            _hotkeys.Dispose();
            _hotkeys = null;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args) => Dispose();

    /// <summary>Releases the global hotkey registration and window hook.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shell.RouteChanged -= OnShellRouteChanged;
        _authService.ConnectionStateChanged -= OnConnectionStateChanged;
        _theme.ThemeChanged -= OnThemeChanged;
        _hotkeys?.Dispose();
        _hotkeys = null;
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint hwnd);
}
