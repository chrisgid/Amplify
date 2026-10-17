using Amplify.Core.Auth;
using Amplify.Core.Http;
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
        // The token client is captured by the singleton auth service, so IHttpClientFactory's handler
        // rotation never applies. Recycle pooled connections ourselves so a days-long session doesn't
        // refresh tokens over stale connections to the Accounts service.
        services.AddHttpClient<SpotifyTokenClient>()
            // Token calls sit on the critical path of every player-state read (the authorization
            // handler refreshes through them), so an unbounded one would stall the poll just as
            // effectively as an unbounded read. Same deadline, same reasoning.
            .AddHttpMessageHandler(() => new RequestTimeoutHandler(RequestTimeoutHandler.DefaultTimeout))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(5),
            });
        services.AddSingleton<IRefreshTokenStore, PasswordVaultRefreshTokenStore>();

        services.AddSingleton<SpotifyAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<SpotifyAuthService>());
        services.AddSingleton<ISpotifyTokenProvider>(sp => sp.GetRequiredService<SpotifyAuthService>());

        services.AddTransient<SpotifyAuthorizationHandler>();
        return services;
    }
}
