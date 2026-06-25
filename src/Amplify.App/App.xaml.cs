using Amplify.App.Auth;
using Amplify.App.Dev;
using Amplify.App.Logging;
using Amplify.App.Onboarding;
using Amplify.App.Settings;
using Amplify.App.Spotify;
using Amplify.App.Theming;
using Amplify.App.ViewModels;
using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

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
        builder.Services.AddOnboarding();

        // Shell: the routing view-model, the window, and the temporary playback slice the window and
        // the main screen share to keep the end-to-end volume flow working.
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<DevPlaybackSlice>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    /// <summary>
    /// Runs the fixed launch sequence, then shows the main window. The window derives its initial
    /// screen from the connection state restored above. Pre-steps that other features own
    /// (single-instance redirect, settings load) slot in where marked as those features land; for now
    /// the sequence only runs the (currently empty) ordered <see cref="IStartupInitializer"/> set.
    /// </summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // OnLaunched is necessarily async void, so any exception escaping here would crash the process
        // with no diagnostics. Catch it, log it, and surface it instead. (A richer in-app error screen
        // arrives with the shell UI; for now logging plus a fail-fast is the honest behaviour.)
        try
        {
            // Future pre-step: single-instance redirect before the window is created.
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
            _window.Activate();
        }
        catch (Exception ex)
        {
            ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
            LogStartupFailed(logger, ex);
            _host.Dispose();
            throw;
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args) => _host.Dispose();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Startup failed; the application cannot continue.")]
    private static partial void LogStartupFailed(ILogger logger, Exception exception);
}
