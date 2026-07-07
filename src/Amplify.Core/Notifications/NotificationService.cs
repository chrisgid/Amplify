using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Amplify.Core.Tray;
using Microsoft.Extensions.Logging;

namespace Amplify.Core.Notifications;

/// <summary>
/// Implements the one-time first-run tray hint: it subscribes to <see cref="ITrayService.HiddenToTray"/>
/// and, the first time the window hides to the tray, shows a single balloon explaining where the window
/// went, then persists a flag so it never shows again. Owns only the <em>policy</em> (show once) and the
/// copy; the tray service owns detecting the hide and rendering the balloon.
/// </summary>
/// <remarks>
/// Registered once and resolved as both <see cref="INotificationService"/> and an
/// <see cref="IStartupInitializer"/> (band 900 — the subscription only needs to exist after the tray is
/// set up at 200). The persist happens only after a non-throwing show so that a first hide with OS
/// notifications suppressed can still surface the hint on a later hide.
/// </remarks>
public sealed partial class NotificationService : INotificationService, IStartupInitializer, IDisposable
{
    private readonly ITrayService _tray;
    private readonly ISettingsService _settings;
    private readonly TrayHintCopy _copy;
    private readonly ILogger<NotificationService> _logger;
    private bool _disposed;

    /// <summary>Creates the service over the tray, settings, and the hint copy.</summary>
    public NotificationService(
        ITrayService tray,
        ISettingsService settings,
        TrayHintCopy copy,
        ILogger<NotificationService> logger)
    {
        _tray = tray;
        _settings = settings;
        _copy = copy;
        _logger = logger;
    }

    // The tray (200) exists by now; the subscription just needs to be live before the first hide.
    public int Order => 900;

    public Task OnLaunchedAsync(CancellationToken ct)
    {
        _tray.HiddenToTray += OnHiddenToTray;
        return Task.CompletedTask;
    }

    private void OnHiddenToTray(object? sender, EventArgs e) => ShowFirstMinimizeHintIfNeeded();

    public void ShowFirstMinimizeHintIfNeeded()
    {
        if (_settings.Current.TrayHintShown)
        {
            return;
        }

        try
        {
            _tray.ShowTrayNotification(_copy.Title, _copy.Message);
        }
        catch (Exception ex)
        {
            // Showing the balloon is a best-effort OS interaction whose failure modes aren't
            // enumerable; leave the flag unset so the hint can still appear on a later hide, and
            // never let a hide-to-tray crash over a missed hint.
            LogHintShowFailed(_logger, ex);
            return;
        }

        _settings.Update(s => s.TrayHintShown = true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tray.HiddenToTray -= OnHiddenToTray;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The first-run tray hint could not be shown; it will be retried on the next hide to the tray.")]
    private static partial void LogHintShowFailed(ILogger logger, Exception exception);
}
