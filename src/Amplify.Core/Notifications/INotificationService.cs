namespace Amplify.Core.Notifications;

/// <summary>
/// Owns the one-time "still running in the tray" hint: the first time the window hides to the tray,
/// it shows a single tray balloon explaining where the window went, then never shows it again. This
/// is the feature's <em>policy</em> (show once) and copy; the tray service owns detecting the hide
/// and displaying the balloon. There are no other notifications and no user-facing toggle.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows the first-minimise hint if it has not been shown before. On the first call while the
    /// persisted "shown" flag is unset, shows the balloon and persists the flag so it is never shown
    /// again (including across restarts); a no-op once the flag is set. If showing the balloon fails,
    /// the flag is left unset so the hint can still appear on a later hide. Wired at startup to fire
    /// on each hide-to-tray.
    /// </summary>
    void ShowFirstMinimizeHintIfNeeded();
}
