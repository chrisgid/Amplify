namespace Amplify.Tests.Auth;

/// <summary>
/// Test message handler that records every request (and its body, read before the response) and
/// returns a caller-supplied response. The response factory receives the 1-based attempt number so
/// tests can vary behaviour across retries (e.g. 429 then 200).
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(request);
        Bodies.Add(body);
        return respond(request, Requests.Count);
    }
}
