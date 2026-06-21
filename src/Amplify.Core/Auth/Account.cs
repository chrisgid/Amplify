namespace Amplify.Core.Auth;

/// <summary>
/// The connected Spotify account, read once from <c>GET /v1/me</c> during connect and exposed via
/// <see cref="IAuthService.CurrentAccount"/>. The active-device label is not part of the profile;
/// it comes from playback state and is composed by the status UI.
/// </summary>
/// <param name="DisplayName">The account's display name.</param>
/// <param name="Plan">Human-readable plan label, e.g. "Premium".</param>
/// <param name="IsPremium"><c>true</c> when <c>product == "premium"</c>; gates volume control downstream.</param>
/// <param name="Initials">Initials derived from <paramref name="DisplayName"/> for the avatar.</param>
public sealed record Account(
    string DisplayName,
    string Plan,
    bool IsPremium,
    string Initials);
