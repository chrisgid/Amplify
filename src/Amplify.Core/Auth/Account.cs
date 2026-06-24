namespace Amplify.Core.Auth;

/// <summary>
/// The connected Spotify account, read once from <c>GET /v1/me</c> during connect and exposed via
/// <see cref="IAuthService.CurrentAccount"/>. The active-device label is not part of the profile;
/// it comes from playback state and is composed by the status UI. Subscription level is not exposed
/// by the Web API, and Amplify always requires Premium (Spotify enforces it on the user's own
/// developer app), so there is no plan/Premium flag here.
/// </summary>
/// <param name="DisplayName">The account's display name.</param>
/// <param name="Initials">Initials derived from <paramref name="DisplayName"/> for the avatar.</param>
public sealed record Account(
    string DisplayName,
    string Initials);
