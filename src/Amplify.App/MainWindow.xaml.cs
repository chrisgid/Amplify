using System.Runtime.InteropServices;
using Amplify.App.Spotify;
using Amplify.App.Theming;
using Amplify.App.ViewModels;
using Amplify.App.Views;
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

    private bool _disposed;

    public MainWindow(ShellViewModel shell, PlayerStateProvider playerState, ThemeService theme)
    {
        _shell = shell;
        _playerState = playerState;
        _theme = theme;
        InitializeComponent();

        ConfigureWindowChrome();
        ApplyTheme();

        _shell.RouteChanged += OnShellRouteChanged;
        _theme.ThemeChanged += OnThemeChanged;
        VisibilityChanged += OnVisibilityChanged;
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
        _shell.RouteChanged -= OnShellRouteChanged;
        _theme.ThemeChanged -= OnThemeChanged;
        VisibilityChanged -= OnVisibilityChanged;
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetDpiForWindow(nint hwnd);
}
