using Amplify.App.ViewModels;
using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Amplify.Core.Tray;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Amplify.App.Tray;

/// <summary>
/// Gives Amplify its background life: a notification-area icon with a menu, minimise- and close-to-tray
/// behaviour, and a full quit. The process (and the global hotkeys) keep running while the window is
/// hidden; the tray icon is the only presence until the window is reopened. Runs at launch as an
/// <see cref="IStartupInitializer"/> (tray/window band) and also reconciles the OS launch-at-startup
/// state into settings.
/// </summary>
public sealed partial class TrayService : ITrayService, IStartupInitializer, IDisposable
{
    private readonly MainWindow _window;
    private readonly ISettingsService _settings;
    private readonly IStartupTaskManager _startupTasks;
    private readonly ShellViewModel _shell;
    private readonly ILogger<TrayService> _logger;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();

    private TaskbarIcon? _trayIcon;
    private bool _quitting;
    private bool _disposed;

    public TrayService(
        MainWindow window,
        ISettingsService settings,
        IStartupTaskManager startupTasks,
        ShellViewModel shell,
        ILogger<TrayService> logger)
    {
        _window = window;
        _settings = settings;
        _startupTasks = startupTasks;
        _shell = shell;
        _logger = logger;

        // Captured on the UI thread (resolved during the launch sequence) so tray/menu callbacks that
        // may resume off-thread are marshalled back before touching the window.
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>Runs in the tray/window band, after the theme is applied and before hotkeys register.</summary>
    public int Order => 200;

    /// <inheritdoc />
    public async Task OnLaunchedAsync(CancellationToken ct)
    {
        Initialize();
        await ReconcileStartupStateAsync();
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = _strings.GetString("Tray_Tooltip"),
                IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
                ContextFlyout = BuildMenu(),
                // Double-clicking the icon is the quickest way back to the window.
                DoubleClickCommand = new RelayCommand(ShowWindow),
            };
            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            // The notification area can be unavailable (policy, a broken shell). The app still runs and
            // the window remains the access point, so log and carry on rather than failing startup.
            LogTrayUnavailable(_logger, ex);
            _trayIcon = null;
        }

        _window.AppWindow.Closing += OnWindowClosing;
        _window.VisibilityChanged += OnWindowVisibilityChanged;
    }

    /// <inheritdoc />
    public void ShowWindow() => _dispatcher.RunOnUi(() =>
    {
        _window.AppWindow.Show();
        if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        _window.AppWindow.MoveInZOrderAtTop();
        _window.Activate();
    });

    /// <inheritdoc />
    public void HideToTray()
    {
        // Hide() leaves the taskbar/switchers entirely (unlike a normal minimise), so the tray icon is
        // the app's only presence while hidden.
        _window.AppWindow.Hide();
    }

    /// <inheritdoc />
    public void Quit() => _dispatcher.RunOnUi(() =>
    {
        _quitting = true;
        Dispose();

        // Closing the (only) window runs the shell's Closed handler, which disposes the host — releasing
        // the hotkey hook, the HttpClient handlers and everything else — and ends the process.
        _window.Close();
    });

    private MenuFlyout BuildMenu()
    {
        MenuFlyout menu = new();
        menu.Items.Add(NewItem("Tray_Open", ShowWindow));
        menu.Items.Add(NewItem("Tray_Settings", OpenSettings));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(NewItem("Tray_Quit", Quit));
        return menu;

        MenuFlyoutItem NewItem(string key, Action action) =>
            new() { Text = _strings.GetString(key), Command = new RelayCommand(action) };
    }

    private void OpenSettings()
    {
        ShowWindow();
        // No-op when settings isn't reachable for the current route (e.g. still onboarding); the window
        // is shown regardless.
        if (_shell.GoToSettingsCommand.CanExecute(null))
        {
            _shell.GoToSettingsCommand.Execute(null);
        }
    }

    // Closing the window keeps the app alive in the tray unless the user opted for close-to-exit, or a
    // real Quit is in progress.
    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_quitting || !_settings.Current.MinimizeToTrayOnClose)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    // A user minimise arrives as a not-visible transition while the presenter reports Minimized; turn it
    // into a hide-to-tray so no minimised taskbar button lingers. A plain hide (close-to-tray) leaves the
    // presenter Restored, so it doesn't match here.
    private void OnWindowVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (!args.Visible
            && _window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
        {
            HideToTray();
        }
    }

    private async Task ReconcileStartupStateAsync()
    {
        // The OS is the source of truth for whether the app actually launches at sign-in (the user can
        // change it in Task Manager), so bring the stored preference in line with reality at launch.
        StartupState state = await _startupTasks.GetStateAsync();
        if (StartupTaskReconciler.ShouldPersist(state, _settings.Current.LaunchAtStartup))
        {
            _settings.Update(s => s.LaunchAtStartup = StartupTaskReconciler.ToToggleValue(state));
        }
    }

    /// <summary>Disposes the tray icon and detaches window handlers. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.AppWindow.Closing -= OnWindowClosing;
        _window.VisibilityChanged -= OnWindowVisibilityChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The system tray icon could not be created; the window remains the only access point.")]
    private static partial void LogTrayUnavailable(ILogger logger, Exception exception);
}
