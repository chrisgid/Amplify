using Amplify.Core.Auth;
using Amplify.Core.Spotify;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Spotify;

/// <summary>DI registration for the Spotify Web API client.</summary>
internal static class SpotifyClientServiceCollectionExtensions
{
    private static readonly Uri _apiBaseAddress = new("https://api.spotify.com/");

    /// <summary>
    /// Registers <see cref="ISpotifyClient"/> as a typed <c>HttpClient</c> (via
    /// <c>IHttpClientFactory</c>) pointed at the Spotify Web API base address, with the authorization
    /// handler that attaches the bearer token and refreshes-and-retries on a 401.
    /// </summary>
    public static IServiceCollection AddSpotifyClient(this IServiceCollection services)
    {
        services.AddHttpClient<ISpotifyClient, SpotifyClient>(client =>
                client.BaseAddress = _apiBaseAddress)
            .AddHttpMessageHandler<SpotifyAuthorizationHandler>()
            // The client is captured by a long-lived singleton, so recycle pooled connections
            // ourselves (IHttpClientFactory's handler rotation can't apply to a captured instance).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });
        return services;
    }
}
