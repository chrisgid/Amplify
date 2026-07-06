namespace Amplify.Core.Tray;

/// <summary>
/// Owns the system-tray (notification-area) presence and the main window's background lifetime: the
/// tray icon and its menu, showing/hiding the window, and a full quit. The app process keeps running
/// (and global hotkeys keep working) while the window is hidden to the tray; only <see cref="Quit"/>
/// exits.
/// </summary>
public interface ITrayService
{
    /// <summary>
    /// Creates the tray icon and menu and wires the window's minimise/close-to-tray behaviour. Called
    /// once during the launch sequence, before the window is shown.
    /// </summary>
    void Initialize();

    /// <summary>Shows and focuses the main window, restoring it from the tray or a minimised state.</summary>
    void ShowWindow();

    /// <summary>Hides the main window to the tray, removing its taskbar button while the process runs on.</summary>
    void HideToTray();

    /// <summary>Exits the application fully: disposes the tray icon and releases hotkeys and other resources.</summary>
    void Quit();

    /// <summary>
    /// Raised each time the window hides to the tray — from both the minimise-to-tray and the
    /// close-to-tray paths. This service only signals the transition and owns no notification policy.
    /// </summary>
    event EventHandler HiddenToTray;

    /// <summary>
    /// Shows a balloon notification through the tray icon. A no-op when the tray icon is unavailable.
    /// Provides the balloon primitive only; the decision to show one (and its copy) belongs to callers.
    /// </summary>
    void ShowTrayNotification(string title, string message);
}
