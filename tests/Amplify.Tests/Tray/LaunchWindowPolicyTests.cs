using Amplify.Core.Tray;

namespace Amplify.Tests.Tray;

public class LaunchWindowPolicyTests
{
    [Theory]
    // Only the "start-minimised + auto-started + not onboarding" combination stays hidden.
    [InlineData(true, true, false, true)]
    // Manual launch always shows the window, even with start-minimised on.
    [InlineData(true, false, false, false)]
    // Onboarding always shows the window, even when auto-started with start-minimised on.
    [InlineData(true, true, true, false)]
    // Start-minimised off always shows the window.
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, true, false)]
    // Auto-started but not onboarding, start-minimised off — still shown.
    [InlineData(true, false, true, false)]
    public void ShouldStartHiddenOnlyWhenStartMinimizedAndAutoStartedAndNotOnboarding(
        bool startMinimized, bool launchedAtStartup, bool isOnboarding, bool expected) =>
        Assert.Equal(expected, LaunchWindowPolicy.ShouldStartHidden(startMinimized, launchedAtStartup, isOnboarding));
}
