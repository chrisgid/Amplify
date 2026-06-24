namespace Amplify.Core.Onboarding;

/// <summary>The first-run screen's state, driven by <see cref="OnboardingFlow"/>.</summary>
public enum OnboardingPhase
{
    /// <summary>Showing the setup guide and Client ID field; the user can start connecting.</summary>
    Welcome,

    /// <summary>The interactive connect attempt is in flight (browser open or token exchange).</summary>
    Authorizing,

    /// <summary>The browser returned success and the token exchange is finishing up.</summary>
    Verifying,
}
