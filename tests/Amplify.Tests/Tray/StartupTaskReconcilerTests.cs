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
}
