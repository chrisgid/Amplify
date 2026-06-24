using System.Net;
using System.Net.Http.Headers;

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
        // (an HttpRequestMessage can't be sent twice).
        response.Dispose();
        string refreshed = await tokenProvider.RefreshAccessTokenAsync(token).ConfigureAwait(false);

        using HttpRequestMessage retry = Clone(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    // Copies method, target, version, headers, and body so the replay is faithful. The original
    // content instance is reused (Amplify's content is buffered, so it can be sent again); its
    // content headers travel with it. Authorization is skipped — the caller sets it fresh.
    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            Content = request.Content,
        };
        foreach ((string name, IEnumerable<string> values) in request.Headers)
        {
            if (!string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(name, values);
            }
        }

        return clone;
    }
}
