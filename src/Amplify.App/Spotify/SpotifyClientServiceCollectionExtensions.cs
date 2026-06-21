using Amplify.Core.Spotify;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Spotify;

/// <summary>DI registration for the Spotify Web API client.</summary>
internal static class SpotifyClientServiceCollectionExtensions
{
    private static readonly Uri _apiBaseAddress = new("https://api.spotify.com/");

    /// <summary>
    /// Registers <see cref="ISpotifyClient"/> as a typed <c>HttpClient</c> (via
    /// <c>IHttpClientFactory</c>) pointed at the Spotify Web API base address.
    /// </summary>
    public static IServiceCollection AddSpotifyClient(this IServiceCollection services)
    {
        services.AddHttpClient<ISpotifyClient, SpotifyClient>(client =>
            client.BaseAddress = _apiBaseAddress);
        return services;
    }
}
