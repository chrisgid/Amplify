namespace Amplify.Core.Tray;

/// <summary>The action the app should take on the OS startup entry when reconciling it.</summary>
public enum StartupAction
{
    /// <summary>Leave the entry as it is.</summary>
    None,

    /// <summary>Enable the entry so the app launches at sign-in.</summary>
    Enable,

    /// <summary>Disable the entry so the app does not launch at sign-in.</summary>
    Disable,
}

/// <summary>
/// Pure logic that reconciles the OS startup-registration state with the app's stored preference and
/// the on-screen toggle. The OS is the source of truth for whether the app actually launches at
/// sign-in (the user can change it outside the app), so this maps a <see cref="StartupState"/> to what
/// the toggle should show, whether the user can change it, and whether the stored setting should be
/// rewritten to match reality.
/// </summary>
public static class StartupTaskReconciler
{
    /// <summary>Whether the toggle should read as on for <paramref name="state"/>.</summary>
    public static bool ToToggleValue(StartupState state) =>
        state is StartupState.Enabled or StartupState.EnabledByPolicy;

    /// <summary>
    /// Whether the user can change the startup preference from within the app. Policy-forced states are
    /// fixed, and a user-disabled entry cannot be re-enabled programmatically.
    /// </summary>
    public static bool IsUserConfigurable(StartupState state) =>
        state is StartupState.Enabled or StartupState.Disabled;

    /// <summary>
    /// Whether <see cref="Settings.AppSettings.LaunchAtStartup"/> should be rewritten to match the OS
    /// reality — true when the stored preference disagrees with the actual registration state.
    /// </summary>
    public static bool ShouldPersist(StartupState state, bool storedLaunchAtStartup) =>
        ToToggleValue(state) != storedLaunchAtStartup;

    /// <summary>
    /// While the app is onboarding it has no account and must not launch at sign-in. True when the OS
    /// entry is effectively on and the app is allowed to turn it off (not fixed on by policy). The
    /// stored preference is deliberately left untouched by this path so it can be restored on connect.
    /// </summary>
    public static bool ShouldDisableForOnboarding(StartupState state) =>
        ToToggleValue(state) && IsUserConfigurable(state);

    /// <summary>
    /// On becoming connected, restore the user's stored preference onto the OS entry: true when the
    /// preference is on, the entry is currently off, and the app is allowed to enable it (so a
    /// user/policy-disabled entry is respected rather than forced back on).
    /// </summary>
    public static bool ShouldEnableForPreference(StartupState state, bool storedLaunchAtStartup) =>
        storedLaunchAtStartup && !ToToggleValue(state) && IsUserConfigurable(state);

    /// <summary>
    /// The single decision for reconciling the OS startup entry from the current facts. While onboarding
    /// the app must not launch at sign-in, so an on-and-configurable entry is turned off (the stored
    /// preference is left alone by the caller so it can be restored later). Once onboarded the entry is
    /// brought in line with the stored preference where the OS allows it. Because it reads only the
    /// current state — not a transition — repeated or out-of-order calls converge on the same result.
    /// </summary>
    public static StartupAction DecideReconcile(bool isOnboarding, StartupState state, bool storedLaunchAtStartup)
    {
        if (isOnboarding)
        {
            return ShouldDisableForOnboarding(state) ? StartupAction.Disable : StartupAction.None;
        }

        return ShouldEnableForPreference(state, storedLaunchAtStartup) ? StartupAction.Enable : StartupAction.None;
    }
}
