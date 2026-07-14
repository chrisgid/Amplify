using Amplify.Core.Tray;

namespace Amplify.Tests.Tray;

public class StartupTaskReconcilerTests
{
    [Theory]
    [InlineData(StartupState.Enabled, true)]
    [InlineData(StartupState.EnabledByPolicy, true)]
    [InlineData(StartupState.Disabled, false)]
    [InlineData(StartupState.DisabledByUser, false)]
    [InlineData(StartupState.DisabledByPolicy, false)]
    public void ToToggleValueReflectsWhetherStartupIsEffectivelyOn(StartupState state, bool expected) =>
        Assert.Equal(expected, StartupTaskReconciler.ToToggleValue(state));

    [Theory]
    [InlineData(StartupState.Enabled, true)]
    [InlineData(StartupState.Disabled, true)]
    [InlineData(StartupState.DisabledByUser, false)]
    [InlineData(StartupState.DisabledByPolicy, false)]
    [InlineData(StartupState.EnabledByPolicy, false)]
    public void IsUserConfigurableIsFalseWhenFixedByUserOrPolicy(StartupState state, bool expected) =>
        Assert.Equal(expected, StartupTaskReconciler.IsUserConfigurable(state));

    [Theory]
    // Stored preference already agrees with reality: nothing to persist.
    [InlineData(StartupState.Enabled, true, false)]
    [InlineData(StartupState.Disabled, false, false)]
    // Stored preference disagrees with the OS: persist to match reality.
    [InlineData(StartupState.Enabled, false, true)]
    [InlineData(StartupState.Disabled, true, true)]
    // A user disabling it in Task Manager while settings still say "on" must be reconciled to off.
    [InlineData(StartupState.DisabledByUser, true, true)]
    // Policy forcing it on while settings say "off" reconciles to on.
    [InlineData(StartupState.EnabledByPolicy, false, true)]
    public void ShouldPersistWhenStoredPreferenceDisagreesWithOsState(
        StartupState state, bool storedLaunchAtStartup, bool expected) =>
        Assert.Equal(expected, StartupTaskReconciler.ShouldPersist(state, storedLaunchAtStartup));

    [Theory]
    // Effectively on and the app may change it: disable so onboarding can't launch at sign-in.
    [InlineData(StartupState.Enabled, true)]
    // Already off, or fixed by the user/policy: nothing to (or nothing we may) turn off.
    [InlineData(StartupState.Disabled, false)]
    [InlineData(StartupState.DisabledByUser, false)]
    [InlineData(StartupState.DisabledByPolicy, false)]
    [InlineData(StartupState.EnabledByPolicy, false)]
    public void ShouldDisableForOnboardingOnlyWhenOnAndConfigurable(StartupState state, bool expected) =>
        Assert.Equal(expected, StartupTaskReconciler.ShouldDisableForOnboarding(state));

    [Theory]
    // Preference on, entry currently off, and the app may change it: re-enable on reconnect.
    [InlineData(StartupState.Disabled, true, true)]
    // Preference off: leave the entry alone.
    [InlineData(StartupState.Disabled, false, false)]
    // Already on: nothing to enable.
    [InlineData(StartupState.Enabled, true, false)]
    // Fixed off by the user or policy: respect that rather than forcing it back on.
    [InlineData(StartupState.DisabledByUser, true, false)]
    [InlineData(StartupState.DisabledByPolicy, true, false)]
    // Forced on by policy: already effectively on, nothing to do.
    [InlineData(StartupState.EnabledByPolicy, false, false)]
    public void ShouldEnableForPreferenceOnlyWhenWantedAndConfigurablyOff(
        StartupState state, bool storedLaunchAtStartup, bool expected) =>
        Assert.Equal(expected, StartupTaskReconciler.ShouldEnableForPreference(state, storedLaunchAtStartup));

    [Theory]
    // Onboarding: disable only an on-and-configurable entry; the stored preference is irrelevant here.
    [InlineData(true, StartupState.Enabled, true, StartupAction.Disable)]
    [InlineData(true, StartupState.Enabled, false, StartupAction.Disable)]
    [InlineData(true, StartupState.EnabledByPolicy, false, StartupAction.None)]
    [InlineData(true, StartupState.Disabled, true, StartupAction.None)]
    [InlineData(true, StartupState.DisabledByUser, true, StartupAction.None)]
    [InlineData(true, StartupState.DisabledByPolicy, false, StartupAction.None)]
    // Onboarded: enable when the preference is on and the entry is configurably off; otherwise leave it.
    [InlineData(false, StartupState.Disabled, true, StartupAction.Enable)]
    [InlineData(false, StartupState.Disabled, false, StartupAction.None)]
    [InlineData(false, StartupState.Enabled, true, StartupAction.None)]
    [InlineData(false, StartupState.Enabled, false, StartupAction.None)]
    [InlineData(false, StartupState.DisabledByUser, true, StartupAction.None)]
    [InlineData(false, StartupState.EnabledByPolicy, false, StartupAction.None)]
    public void DecideReconcileForcesOffWhileOnboardingAndAppliesPreferenceWhenConnected(
        bool isOnboarding, StartupState state, bool storedLaunchAtStartup, StartupAction expected) =>
        Assert.Equal(expected, StartupTaskReconciler.DecideReconcile(isOnboarding, state, storedLaunchAtStartup));
}
