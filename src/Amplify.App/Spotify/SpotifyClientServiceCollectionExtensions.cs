using Amplify.App.ViewModels;
using Amplify.Core.Auth;
using Amplify.Core.Spotify;
using Amplify.Core.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace Amplify.App.Spotify;

/// <summary>DI registration for the Spotify Web API client and the volume controller over it.</summary>
internal static class SpotifyClientServiceCollectionExtensions
{
    private static readonly Uri _apiBaseAddress = new("https://api.spotify.com/");

    /// <summary>
    /// Registers <see cref="ISpotifyClient"/> as a typed <c>HttpClient</c> (via
    /// <c>IHttpClientFactory</c>) pointed at the Spotify Web API base address. The pipeline, outermost
    /// first, retries on a <c>429</c> (honouring <c>Retry-After</c>) and attaches the bearer token
    /// (refreshing-and-retrying on a 401) — so a rate-limit retry re-runs auth with a fresh token.
    /// </summary>
    public static IServiceCollection AddSpotifyClient(this IServiceCollection services)
    {
        services.AddTransient<RateLimitHandler>();

        services.AddHttpClient<ISpotifyClient, SpotifyClient>(client =>
                client.BaseAddress = _apiBaseAddress)
            .AddHttpMessageHandler<RateLimitHandler>()
            .AddHttpMessageHandler<SpotifyAuthorizationHandler>()
            // The client is captured by a long-lived singleton, so recycle pooled connections
            // ourselves (IHttpClientFactory's handler rotation can't apply to a captured instance).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });
        return services;
    }

    /// <summary>
    /// Registers the shared <see cref="IPlayerStateProvider"/> — the single Spotify player-state
    /// poller that the status card and the volume controller both observe. One instance backs the
    /// interface and the <see cref="IStartupInitializer"/> that starts polling after launch.
    /// </summary>
    public static IServiceCollection AddPlayerState(this IServiceCollection services)
    {
        services.AddSingleton<PlayerStateProvider>();
        services.AddSingleton<IPlayerStateProvider>(sp => sp.GetRequiredService<PlayerStateProvider>());
        services.AddSingleton<IStartupInitializer>(sp => sp.GetRequiredService<PlayerStateProvider>());
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IVolumeController"/> and its view-model. One instance backs the public
    /// controller interface and the <see cref="IStartupInitializer"/> that wires its subscriptions and
    /// seeds from the shared player-state provider.
    /// </summary>
    public static IServiceCollection AddVolumeControl(this IServiceCollection services)
    {
        services.AddSingleton<VolumeController>();
        services.AddSingleton<IVolumeController>(sp => sp.GetRequiredService<VolumeController>());
        services.AddSingleton<IStartupInitializer>(sp => sp.GetRequiredService<VolumeController>());
        services.AddSingleton<VolumeViewModel>();
        return services;
    }
}
