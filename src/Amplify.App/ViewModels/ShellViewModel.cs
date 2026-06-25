using Amplify.Core.Auth;
using Amplify.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace Amplify.App.ViewModels;

/// <summary>
/// Drives which top-level screen the shell shows. All routing rules live in <see cref="ShellRouter"/>;
/// this view-model is the UI-facing adapter — it exposes the current route plus the navigation
/// commands the screens bind to, and forwards connection-state changes onto the UI thread so the
/// window can re-navigate safely.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ShellRouter _router;
    private readonly DispatcherQueue? _dispatcher;

    public ShellViewModel(IAuthService authService)
    {
        _router = new ShellRouter(authService.State);

        // Captured on the UI thread (the view-model is resolved during launch), so connection-state
        // changes raised on a background thread can be marshalled back before touching the route.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _router.RouteChanged += OnRouterRouteChanged;
        authService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>The screen the shell should currently show.</summary>
    public ShellRoute CurrentRoute => _router.CurrentRoute;

    /// <summary>Raised with the new route when navigation should occur.</summary>
    public event EventHandler<ShellRoute>? RouteChanged;

    [RelayCommand]
    private void GoToSettings() => _router.GoToSettings();

    [RelayCommand]
    private void GoBack() => _router.GoBack();

    private void OnRouterRouteChanged(object? sender, ShellRoute route)
    {
        OnPropertyChanged(nameof(CurrentRoute));
        RouteChanged?.Invoke(this, route);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        _dispatcher.RunOnUi(() => _router.OnConnectionStateChanged(state));
}
