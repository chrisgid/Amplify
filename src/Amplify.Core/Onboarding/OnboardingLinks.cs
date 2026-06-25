namespace Amplify.Core.Onboarding;

/// <summary>
/// External links shown in the onboarding screen's guided Spotify-app setup and own-app note. These
/// are documentation/legal pages, not part of the OAuth wire protocol itself (see
/// <see cref="Amplify.Core.Auth.SpotifyOAuthConstants"/> for that), but are still centralised here
/// rather than scattered as string literals in XAML.
/// </summary>
public static class OnboardingLinks
{
    /// <summary>Where the user creates their own Spotify developer app and gets a Client ID.</summary>
    public const string DeveloperDashboardUrl = "https://developer.spotify.com/dashboard";

    /// <summary>Spotify's Developer Terms, referenced by the own-app note.</summary>
    public const string DeveloperTermsUrl = "https://developer.spotify.com/terms";
}
