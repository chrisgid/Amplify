using Amplify.Core.Auth;
using Amplify.Core.Navigation;

namespace Amplify.Tests.Navigation;

public class ShellRouterTests
{
    [Theory]
    [InlineData(ConnectionState.Disconnected, ShellRoute.Onboarding)]
    [InlineData(ConnectionState.Connecting, ShellRoute.Onboarding)]
    [InlineData(ConnectionState.Error, ShellRoute.Onboarding)]
    [InlineData(ConnectionState.Connected, ShellRoute.Main)]
    public void InitialRouteDerivesFromConnectionState(ConnectionState state, ShellRoute expected)
    {
        var router = new ShellRouter(state);

        Assert.Equal(expected, router.CurrentRoute);
    }

    [Fact]
    public void GoToSettingsFromMainSetsSettingsAndRaises()
    {
        var router = new ShellRouter(ConnectionState.Connected);
        ShellRoute? raised = null;
        router.RouteChanged += (_, route) => raised = route;

        router.GoToSettings();

        Assert.Equal(ShellRoute.Settings, router.CurrentRoute);
        Assert.Equal(ShellRoute.Settings, raised);
    }

    [Fact]
    public void GoBackFromSettingsReturnsToMain()
    {
        var router = new ShellRouter(ConnectionState.Connected);
        router.GoToSettings();

        router.GoBack();

        Assert.Equal(ShellRoute.Main, router.CurrentRoute);
    }

    [Fact]
    public void ConnectedFromOnboardingAdvancesToMain()
    {
        var router = new ShellRouter(ConnectionState.Disconnected);
        ShellRoute? raised = null;
        router.RouteChanged += (_, route) => raised = route;

        router.OnConnectionStateChanged(ConnectionState.Connected);

        Assert.Equal(ShellRoute.Main, router.CurrentRoute);
        Assert.Equal(ShellRoute.Main, raised);
    }

    [Fact]
    public void ConnectedWhileInSettingsDoesNotNavigateAway()
    {
        var router = new ShellRouter(ConnectionState.Connected);
        router.GoToSettings();
        bool raisedAfterSettings = false;
        router.RouteChanged += (_, _) => raisedAfterSettings = true;

        router.OnConnectionStateChanged(ConnectionState.Connected);

        Assert.Equal(ShellRoute.Settings, router.CurrentRoute);
        Assert.False(raisedAfterSettings);
    }

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Error)]
    [InlineData(ConnectionState.Disconnected)]
    public void NonConnectedStateLeavesOnboarding(ConnectionState state)
    {
        var router = new ShellRouter(ConnectionState.Disconnected);

        router.OnConnectionStateChanged(state);

        Assert.Equal(ShellRoute.Onboarding, router.CurrentRoute);
    }

    [Fact]
    public void SettingTheSameRouteDoesNotRaise()
    {
        var router = new ShellRouter(ConnectionState.Connected);
        int raisedCount = 0;
        router.RouteChanged += (_, _) => raisedCount++;

        router.GoBack(); // already on Main

        Assert.Equal(ShellRoute.Main, router.CurrentRoute);
        Assert.Equal(0, raisedCount);
    }
}
