namespace Amplify.Core.Tray;

/// <summary>
/// Decides whether the main window should stay hidden (tray only) at launch, rather than being shown.
/// Keeping this as pure logic lets the shell make a single, testable decision from three facts about
/// the launch.
/// </summary>
public static class LaunchWindowPolicy
{
    /// <summary>
    /// The window starts hidden only when the user asked to start minimised to the tray, the app was
    /// launched automatically at sign-in (not opened by the user), and it isn't showing onboarding.
    /// A manual launch always shows the window (the user asked for it), and onboarding must always be
    /// visible (there is nothing to run in the background yet).
    /// </summary>
    public static bool ShouldStartHidden(bool startMinimized, bool launchedAtStartup, bool isOnboarding) =>
        startMinimized && launchedAtStartup && !isOnboarding;
}
