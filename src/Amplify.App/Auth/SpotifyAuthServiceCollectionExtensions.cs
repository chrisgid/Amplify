using Amplify.Core.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Auth;

/// <summary>DI registration for Spotify authentication.</summary>
internal static class SpotifyAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the authentication service (exposed as both the public <see cref="IAuthService"/> and
    /// the internal token-provider seam, sharing one instance), its refresh-token store, the typed
    /// token <c>HttpClient</c>, and the Web API authorization handler.
    /// </summary>
    public static IServiceCollection AddSpotifyAuth(this IServiceCollection services)
    {
        services.AddHttpClient<SpotifyTokenClient>();
        services.AddSingleton<IRefreshTokenStore, PasswordVaultRefreshTokenStore>();

        services.AddSingleton<SpotifyAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<SpotifyAuthService>());
        services.AddSingleton<ISpotifyTokenProvider>(sp => sp.GetRequiredService<SpotifyAuthService>());

        services.AddTransient<SpotifyAuthorizationHandler>();
        return services;
    }
}
