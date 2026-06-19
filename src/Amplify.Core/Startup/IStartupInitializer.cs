namespace Amplify.Core.Startup;

/// <summary>
/// A startup hook invoked by the application shell during the launch sequence.
/// Features implement this instead of editing <c>App.xaml.cs</c> directly, so independently-built
/// features compose without conflicting edits.
/// </summary>
/// <remarks>
/// The shell resolves all registered <see cref="IStartupInitializer"/> instances and runs them in
/// ascending <see cref="Order"/>, <em>after</em> it has loaded settings
/// (<c>ISettingsService.LoadAsync</c>) and restored the session (<c>IAuthService.RestoreSessionAsync</c>)
/// as explicit pre-steps. Lower <see cref="Order"/> runs earlier. Use the bands below so features
/// slot in deterministically:
/// <list type="bullet">
///   <item><description>100 — theme (apply before the first frame)</description></item>
///   <item><description>200 — tray + window (single-instance handled pre-window)</description></item>
///   <item><description>400 — hotkeys (register after settings are loaded)</description></item>
///   <item><description>900 — everything else</description></item>
/// </list>
/// </remarks>
public interface IStartupInitializer
{
    /// <summary>Ascending sort key; lower values run earlier. See the bands in the type remarks.</summary>
    int Order { get; }

    /// <summary>Performs this feature's launch-time work.</summary>
    /// <param name="ct">Cancels the launch sequence.</param>
    Task OnLaunchedAsync(CancellationToken ct);
}
