namespace Amplify.App.Auth;

/// <summary>
/// TEMPORARY scaffolding for the walking-skeleton connect flow. Holds the per-user Spotify Client ID
/// that the authentication service reads when no real source exists yet.
/// </summary>
/// <remarks>
/// The Client ID is per-user and entered by the user — Amplify ships none. Once onboarding and the
/// settings service land, the Client ID will be captured during onboarding and read from persisted
/// settings; this holder (and the temporary entry field that feeds it) is removed at that point.
/// </remarks>
public sealed class DevClientIdSource
{
    /// <summary>The Client ID to use for the next connect attempt; empty until the user enters one.</summary>
    public string ClientId { get; set; } = "";
}
