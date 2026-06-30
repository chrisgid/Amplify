namespace Amplify.Core.Http;

/// <summary>
/// Helpers for replaying an <see cref="HttpRequestMessage"/>. A request instance can only be sent
/// once, so a handler that retries (refresh-on-401, backoff-on-429) must send a copy.
/// </summary>
internal static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Creates a re-sendable copy of <paramref name="request"/> — method, target, version, headers, and
    /// content. The original content instance is reused (Amplify's request bodies are buffered, so they
    /// can be sent again), and its content headers travel with it.
    /// </summary>
    public static HttpRequestMessage Clone(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            Content = request.Content,
        };

        foreach ((string name, IEnumerable<string> values) in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(name, values);
        }

        return clone;
    }
}
