using Amplify.Core.Auth;

namespace Amplify.Core.Onboarding;

/// <summary>
/// The onboarding screen's platform-agnostic state machine: which phase is showing, whether the
/// last attempt was denied, and the gating rule for starting a new connect attempt. The view-model
/// wraps this (mirroring how <c>ShellViewModel</c> wraps <c>ShellRouter</c>) so every transition can
/// be unit-tested without WinUI.
/// </summary>
/// <remarks>
/// A successful <see cref="AuthResult"/> deliberately leaves <see cref="Phase"/> at
/// <see cref="OnboardingPhase.Authorizing"/> rather than advancing it to
/// <see cref="OnboardingPhase.Verifying"/> — the shell's router already navigates away as soon as
/// <c>IAuthService</c> raises <c>Connected</c>, so a distinct verifying state would only ever flash.
/// </remarks>
public sealed class OnboardingFlow
{
    /// <summary>The screen currently being shown.</summary>
    public OnboardingPhase Phase { get; private set; } = OnboardingPhase.Welcome;

    /// <summary>Whether the most recent attempt ended in the user declining consent.</summary>
    public bool Denied { get; private set; }

    /// <summary>A user-readable message from the most recent non-denied failure, or <c>null</c>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Raised whenever any of the properties above change.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Whether a connect attempt can start: only from <see cref="OnboardingPhase.Welcome"/> and only
    /// with a non-blank Client ID.
    /// </summary>
    public bool CanBeginConnect(string? clientId) =>
        Phase == OnboardingPhase.Welcome && !string.IsNullOrWhiteSpace(clientId);

    /// <summary>Moves to <see cref="OnboardingPhase.Authorizing"/> and clears prior failure state.</summary>
    public void BeginConnect()
    {
        Phase = OnboardingPhase.Authorizing;
        Denied = false;
        ErrorMessage = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies the outcome of <see cref="IAuthService.ConnectAsync"/>. Success leaves the phase
    /// unchanged (see remarks); denial and other failures both return to
    /// <see cref="OnboardingPhase.Welcome"/> so the user can retry.
    /// </summary>
    public void OnConnectResult(AuthResult result)
    {
        if (result.Success)
        {
            return;
        }

        Phase = OnboardingPhase.Welcome;
        Denied = result.Denied;
        ErrorMessage = result.Denied ? null : result.Error;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Abandons an in-flight connect attempt (e.g. the user closed the browser tab) and returns to
    /// <see cref="OnboardingPhase.Welcome"/> so they can retry immediately, without waiting for
    /// <see cref="IAuthService.ConnectAsync"/> to eventually time out on its own. A no-op outside
    /// <see cref="OnboardingPhase.Authorizing"/>.
    /// </summary>
    public void Cancel()
    {
        if (Phase != OnboardingPhase.Authorizing)
        {
            return;
        }

        Phase = OnboardingPhase.Welcome;
        Denied = false;
        ErrorMessage = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
