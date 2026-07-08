using Amplify.Core.Auth;

namespace Amplify.Core.Navigation;

/// <summary>
/// Decides which top-level screen the shell shows and encodes the allowed transitions between
/// them. This is the platform-agnostic heart of the shell's navigation: the view-model wraps it and
/// projects <see cref="CurrentRoute"/> to the window, but every routing rule lives here so it can be
/// unit-tested without a UI.
/// </summary>
/// <remarks>
/// The initial route is derived from the connection state: a connected account opens on
/// <see cref="ShellRoute.Main"/>, anything else on <see cref="ShellRoute.Onboarding"/>. The router
/// raises <see cref="RouteChanged"/> only when the route actually changes.
/// </remarks>
public sealed class ShellRouter
{
    /// <summary>Creates a router whose initial route reflects the given connection state.</summary>
    public ShellRouter(ConnectionState initialState) => CurrentRoute = RouteFor(initialState);

    /// <summary>The screen the shell is currently showing.</summary>
    public ShellRoute CurrentRoute { get; private set; }

    /// <summary>Raised with the new route whenever <see cref="CurrentRoute"/> changes.</summary>
    public event EventHandler<ShellRoute>? RouteChanged;

    /// <summary>Navigates from the main screen to settings.</summary>
    public void GoToSettings() => SetRoute(ShellRoute.Settings);

    /// <summary>Returns from settings to the main screen.</summary>
    public void GoBack() => SetRoute(ShellRoute.Main);

    /// <summary>
    /// Reacts to a connection-state change. Becoming connected while still on the onboarding screen
    /// advances to the main screen; losing the session entirely (a disconnect or a reset) returns to
    /// onboarding from wherever the user is. A transient <see cref="ConnectionState.Connecting"/> or
    /// <see cref="ConnectionState.Error"/> leaves the current route untouched, so a background refresh
    /// hiccup doesn't yank the user away from the main screen or settings — those are surfaced in
    /// place with a reconnect option instead.
    /// </summary>
    public void OnConnectionStateChanged(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.Connected when CurrentRoute == ShellRoute.Onboarding:
                SetRoute(ShellRoute.Main);
                break;

            case ConnectionState.Disconnected:
                SetRoute(ShellRoute.Onboarding);
                break;
        }
    }

    private static ShellRoute RouteFor(ConnectionState state) =>
        state == ConnectionState.Connected ? ShellRoute.Main : ShellRoute.Onboarding;

    private void SetRoute(ShellRoute route)
    {
        if (route == CurrentRoute)
        {
            return;
        }

        CurrentRoute = route;
        RouteChanged?.Invoke(this, route);
    }
}
