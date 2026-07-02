using Amplify.Core.Tray;

namespace Amplify.Tests.Tray;

public class LaunchWindowPolicyTests
{
    [Theory]
    // Only "tray available + start-minimised + auto-started + not onboarding" stays hidden.
    [InlineData(true, true, false, true, true)]
    // No tray icon → the window is the only way back, so always show it.
    [InlineData(true, true, false, false, false)]
    // Manual launch always shows the window, even with start-minimised on.
    [InlineData(true, false, false, true, false)]
    // Onboarding always shows the window, even when auto-started with start-minimised on.
    [InlineData(true, true, true, true, false)]
    // Start-minimised off always shows the window.
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(false, false, true, true, false)]
    // Auto-started but not onboarding, start-minimised off — still shown.
    [InlineData(true, false, true, true, false)]
    public void ShouldStartHiddenOnlyWhenTrayAvailableStartMinimizedAutoStartedAndNotOnboarding(
        bool startMinimized, bool launchedAtStartup, bool isOnboarding, bool trayAvailable, bool expected) =>
        Assert.Equal(
            expected,
            LaunchWindowPolicy.ShouldStartHidden(startMinimized, launchedAtStartup, isOnboarding, trayAvailable));
}
