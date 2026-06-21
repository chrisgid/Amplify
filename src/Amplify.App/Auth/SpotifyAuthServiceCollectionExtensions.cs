using Amplify.Core.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Auth;

/// <summary>DI registration for Spotify authentication.</summary>
internal static class SpotifyAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the authentication service, its refresh-token store, the typed <c>HttpClient</c>
    /// factory used for the token exchange, and the temporary Client ID source.
    /// </summary>
    public static IServiceCollection AddSpotifyAuth(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IRefreshTokenStore, PasswordVaultRefreshTokenStore>();
        services.AddSingleton<DevClientIdSource>();
        services.AddSingleton<IAuthService, SpotifyAuthService>();
        return services;
    }
}
