namespace Amplify.Core.Reset;

/// <summary>
/// Coordinates a full reset of Amplify back to its first-run state: restores every preference to its
/// default (clearing the stored Spotify Client ID and re-applying the default hotkeys and volume
/// step) and disconnects the Spotify account. The confirmation prompt that gates this is a UI
/// concern; by the time <see cref="ResetAsync"/> is called the user has already agreed to proceed.
/// </summary>
public interface IResetService
{
    /// <summary>
    /// Resets all settings to their defaults, then disconnects Spotify. Ordered so that if the
    /// disconnect fails the app is still left in the coherent defaults-applied state, and so the
    /// disconnect's connection-state change is the last thing observers see — letting the shell route
    /// back to onboarding once the reset has fully landed.
    /// </summary>
    Task ResetAsync();
}
