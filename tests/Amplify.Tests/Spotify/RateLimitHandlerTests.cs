using System.Net;
using System.Net.Http.Headers;
using Amplify.Core.Spotify;
using Amplify.Tests.Auth;

namespace Amplify.Tests.Spotify;

public sealed class RateLimitHandlerTests
{
    [Fact]
    public async Task RetriesOnceThenReturnsSuccess()
    {
        // A past Retry-After date makes the computed delay zero, so the retry is immediate.
        var inner = new StubHttpMessageHandler((_, attempt) =>
            attempt == 1 ? Throttled(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1)) : Ok());

        using HttpMessageInvoker invoker = CreateInvoker(inner);
        HttpResponseMessage response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task StopsAfterBoundedRetriesAndReturnsLastResponse()
    {
        var inner = new StubHttpMessageHandler((_, _) => Throttled(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1)));

        using HttpMessageInvoker invoker = CreateInvoker(inner);
        HttpResponseMessage response = await invoker.SendAsync(Request(), CancellationToken.None);

        // The final 429 is surfaced rather than looping forever: 1 initial attempt + 3 retries.
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(4, inner.Requests.Count);
    }

    [Fact]
    public void ComputeDelayHonoursRetryAfterSeconds()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        Assert.Equal(
            TimeSpan.FromSeconds(7),
            RateLimitHandler.ComputeDelay(response, attempt: 0, TimeProvider.System));
    }

    [Fact]
    public void ComputeDelayHonoursRetryAfterDateAgainstTheProvidedClock()
    {
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(now);
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now + TimeSpan.FromSeconds(3));

        // Uses the injected clock's "now", not DateTimeOffset.UtcNow, so the delta is exactly 3s.
        Assert.Equal(TimeSpan.FromSeconds(3), RateLimitHandler.ComputeDelay(response, attempt: 0, time));
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(1, 1000)]
    [InlineData(2, 2000)]
    public void ComputeDelayFallsBackToExponentialBackoff(int attempt, int expectedMilliseconds)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMilliseconds),
            RateLimitHandler.ComputeDelay(response, attempt, TimeProvider.System));
    }

    private static HttpResponseMessage Throttled(DateTimeOffset retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        return response;
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private static HttpMessageInvoker CreateInvoker(HttpMessageHandler inner)
    {
        var handler = new RateLimitHandler { InnerHandler = inner };
        return new HttpMessageInvoker(handler);
    }

    private static HttpRequestMessage Request() => new(HttpMethod.Get, "https://api.spotify.com/v1/me/player");
}
