using System.Runtime.InteropServices;
using Amplify.App.Dev;
using Amplify.App.Interop;
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
    // The prototype's compact window: ~480 logical px wide, tall enough for the main screen, with a
    // floor that keeps the layout usable while staying resizable.
    private const int _initialWidth = 480;
    private const int _initialHeight = 760;
    private const int _minWidth = 420;
    private const int _minHeight = 480;

    private readonly ShellViewModel _shell;
    private readonly IAuthService _authService;
    private readonly DevPlaybackSlice _playback;
    private readonly DispatcherQueue _dispatcher;

    private GlobalHotkeyWindow? _hotkeys;
    private bool _disposed;

    public MainWindow(ShellViewModel shell, IAuthService authService, DevPlaybackSlice playback)
    {
        _shell = shell;
        _authService = authService;
        _playback = playback;
        InitializeComponent();
        _dispatcher = DispatcherQueue;

        ConfigureWindowChrome();

        _shell.RouteChanged += OnShellRouteChanged;
        _authService.ConnectionStateChanged += OnConnectionStateChanged;
        Closed += OnClosed;

        // Show the screen the shell picked for the current connection state.
        NavigateTo(_shell.CurrentRoute);
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
        if (state != ConnectionState.Connected)
        {
            return;
        }

        // Connection-state changes can arrive off the UI thread; arming hotkeys touches the window.
        _dispatcher.TryEnqueue(() =>
        {
            ArmHotkeys();
            _ = _playback.RefreshAsync();
        });
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
        _hotkeys?.Dispose();
        _hotkeys = null;
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint hwnd);
}
