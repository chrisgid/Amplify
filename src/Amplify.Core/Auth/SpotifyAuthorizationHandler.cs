using System.Net;
using System.Net.Http.Headers;
using Amplify.Core.Http;

namespace Amplify.Core.Auth;

/// <summary>
/// Attaches the Spotify bearer token to every Web API request and recovers from an expired token:
/// when Spotify replies <c>401 Unauthorized</c>, it forces a single token refresh and replays the
/// request once with the fresh token. Owning this here keeps the Web API client free of auth
/// concerns and the refresh in one place.
/// </summary>
/// <remarks>
/// Public so the typed-client registration in the app assembly can add it to the pipeline; it
/// depends only on <see cref="ISpotifyTokenProvider"/>.
/// </remarks>
public sealed class SpotifyAuthorizationHandler(ISpotifyTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string token = await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // The access token was rejected — refresh once and replay on a fresh request instance
        // (an HttpRequestMessage can't be sent twice). The clone carries the stale Authorization header,
        // which is immediately overwritten below with the freshly refreshed token.
        response.Dispose();
        string refreshed = await tokenProvider.RefreshAccessTokenAsync(token).ConfigureAwait(false);

        using HttpRequestMessage retry = request.Clone();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }
}
