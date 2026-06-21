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
    /// advances to the main screen; other transitions leave the current route untouched so the user
    /// isn't yanked away from settings or the main screen by a transient state change.
    /// </summary>
    public void OnConnectionStateChanged(ConnectionState state)
    {
        if (state == ConnectionState.Connected && CurrentRoute == ShellRoute.Onboarding)
        {
            SetRoute(ShellRoute.Main);
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
