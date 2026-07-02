using Amplify.App.Auth;
using Amplify.App.ConnectionStatus;
using Amplify.App.Hotkeys;
using Amplify.App.Logging;
using Amplify.App.Onboarding;
using Amplify.App.Settings;
using Amplify.App.Spotify;
using Amplify.App.Theming;
using Amplify.App.Tray;
using Amplify.App.ViewModels;
using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Amplify.Core.Navigation;
using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Amplify.Core.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Amplify.App;

/// <summary>
/// Application entry point and owner of the host/DI container. Other features register
/// services via their own <c>AddXxx()</c> extension and plug launch-time work in through
/// <see cref="IStartupInitializer"/> rather than editing this file.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private Window? _window;

    /// <summary>
    /// The application's service provider. Pages created by the navigation <c>Frame</c> have no
    /// constructor injection, so they resolve their dependencies from here.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Builds the host (DI, configuration, logging) before any window is created.</summary>
    public App()
    {
        InitializeComponent();
        _host = BuildHost();
        Services = _host.Services;
    }

    private static IHost BuildHost()
    {
        // ContentRootPath = the app's install/output dir so the packaged appsettings.json resolves.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Cross-cutting infrastructure the shell owns: a rolling file log under LocalFolder\logs\
        // plus the Debug provider for development. Never log tokens or PII. The file provider itself
        // drops HttpClient's per-request noise (see FileLogger) so the Debug provider stays verbose.
        builder.Logging.AddDebug();
        builder.Logging.AddFileLogging();

        // appsettings.json -> typed options (consumed by the authentication service).
        builder.Services.Configure<SpotifyOptions>(builder.Configuration.GetSection(SpotifyOptions.SectionName));

        // Feature DI registrations are appended here as each feature lands (AddSettings(), AddAuth(), ...).
        builder.Services.AddSettings();
        builder.Services.AddTheming();
        builder.Services.AddSpotifyAuth();
        builder.Services.AddSpotifyClient();
        builder.Services.AddPlayerState();
        builder.Services.AddVolumeControl();
        builder.Services.AddOnboarding();
        builder.Services.AddConnectionStatus();
        builder.Services.AddHotkeys();
        builder.Services.AddSystemTray();

        // Shell: the routing view-model and the window.
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    /// <summary>
    /// Runs the fixed launch sequence — load settings, restore the session, run the ordered
    /// <see cref="IStartupInitializer"/> set (theme → tray/window → hotkeys) — then shows the main
    /// window unless the user opted to start minimised to the tray. The window derives its initial
    /// screen from the connection state restored above.
    /// </summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // OnLaunched is necessarily async void, so any exception escaping here would crash the process
        // with no diagnostics. Catch it, log it, and surface it instead. (A richer in-app error screen
        // arrives with the shell UI; for now logging plus a fail-fast is the honest behaviour.)
        try
        {
            // Single-instancing is already decided in Program.Main (before the host is built).
            // Load settings first so features initialise against persisted preferences.
            await _host.Services.GetRequiredService<ISettingsService>().LoadAsync();

            // Silently restore a stored session before the window/initializers so the shell can open
            // straight on the main screen when a refresh token is present.
            await _host.Services.GetRequiredService<IAuthService>().RestoreSessionAsync();

            // then ordered initializers (theme 100 -> tray/window 200 -> hotkeys 400):
            await StartupInitializerRunner.RunAsync(
                _host.Services.GetServices<IStartupInitializer>(), CancellationToken.None);

            _window = _host.Services.GetRequiredService<MainWindow>();
            _window.Closed += OnMainWindowClosed;

            // A second launch redirects its activation here; surface the existing window in response.
            AppInstance.GetCurrent().Activated += OnAppInstanceActivated;

            // The tray icon is already up (tray initializer). Start hidden in the tray only when the
            // user asked to, the app was auto-started at sign-in, and it isn't showing onboarding — a
            // manual launch or the onboarding screen always shows the window.
            bool isOnboarding =
                _host.Services.GetRequiredService<ShellViewModel>().CurrentRoute == ShellRoute.Onboarding;
            bool startHidden = LaunchWindowPolicy.ShouldStartHidden(
                _host.Services.GetRequiredService<ISettingsService>().Current.StartMinimizedToTray,
                Program.LaunchedAtStartup,
                isOnboarding);

            if (!startHidden)
            {
                _window.Activate();
            }
        }
        catch (Exception ex)
        {
            ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
            LogStartupFailed(logger, ex);
            _host.Dispose();
            throw;
        }
    }

    // Raised on a background thread when another instance redirects its activation to us; marshal to the
    // UI thread and reopen the window via the tray service (which owns show/hide).
    private void OnAppInstanceActivated(object? sender, AppActivationArguments args) =>
        _window?.DispatcherQueue.TryEnqueue(() =>
            _host.Services.GetRequiredService<ITrayService>().ShowWindow());

    private void OnMainWindowClosed(object sender, WindowEventArgs args) => _host.Dispose();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Startup failed; the application cannot continue.")]
    private static partial void LogStartupFailed(ILogger logger, Exception exception);
}
