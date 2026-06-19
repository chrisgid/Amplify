namespace Amplify.Core.Configuration;

/// <summary>
/// App-wide Spotify constants bound from <c>appsettings.json</c> (the <c>Spotify</c> section). These
/// are the same for every user and are <em>not</em> secret. The per-user Spotify Client ID is captured
/// during onboarding and lives in <c>settings.json</c>, not here. Owned/bound by the application
/// shell and consumed by the authentication service.
/// </summary>
public sealed class SpotifyOptions
{
    /// <summary>Configuration section name in <c>appsettings.json</c>.</summary>
    public const string SectionName = "Spotify";

    /// <summary>Loopback port for the OAuth redirect URI (<c>http://127.0.0.1:{RedirectPort}/callback</c>).</summary>
    public int RedirectPort { get; set; }

    /// <summary>OAuth scopes requested at authorization — the minimum Amplify needs.</summary>
    public string[] Scopes { get; set; } = [];
}
