using Amplify.Core.Http;
using Amplify.Core.Spotify;

namespace Amplify.Tests.Http;

public class RequestTimeoutHandlerTests
{
    private const string _url = "https://api.spotify.com/v1/me/player";

    private static readonly TimeSpan _shortTimeout = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task APromptResponsePassesStraightThrough()
    {
        using HttpClient client = Build(_ => Task.FromResult(new HttpResponseMessage()));

        using HttpResponseMessage response =
            await client.GetAsync(_url, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AStalledRequestIsCutOffAtTheDeadline()
    {
        // The case ConnectTimeout can't catch: the connection is already established (pooled), so
        // nothing below this handler is bounded by anything but HttpClient's 100s default.
        using HttpClient client = Build(Stall);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetAsync(_url, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATrippedDeadlineLooksLikeATimeoutToCallers()
    {
        // Reported the way HttpClient reports its own Timeout — TaskCanceledException wrapping a
        // TimeoutException — so nothing downstream has to know this handler exists.
        using HttpClient client = Build(Stall);

        TaskCanceledException ex = await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetAsync(_url, TestContext.Current.CancellationToken));

        Assert.IsType<TimeoutException>(ex.InnerException);
    }

    [Fact]
    public async Task ATrippedDeadlineCountsAsSpotifyBeingUnreachable()
    {
        // The point of the whole handler: a stalled read has to reach the status block's outage
        // threshold, not sit there invisibly keeping the stale card up.
        using HttpClient client = Build(Stall);

        Exception ex = await Assert.ThrowsAnyAsync<Exception>(
            () => client.GetAsync(_url, TestContext.Current.CancellationToken));

        Assert.True(SpotifyRequestFailure.MeansUnreachable(ex));
    }

    [Fact]
    public async Task ACallerCancellationIsNotDisguisedAsATimeout()
    {
        // Only our own deadline is rewritten. A caller who gave up must still look cancelled, or a
        // genuine cancellation would be miscounted as an outage.
        using HttpClient client = Build(Stall, TimeSpan.FromMinutes(5));
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();

        TaskCanceledException ex =
            await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetAsync(_url, caller.Token));

        Assert.IsNotType<TimeoutException>(ex.InnerException);
    }

    private static async Task<HttpResponseMessage> Stall(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage();
    }

    private static HttpClient Build(
        Func<CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null) =>
        new(new RequestTimeoutHandler(timeout ?? _shortTimeout)
        {
            InnerHandler = new DelegatingStub(respond),
        });

    private sealed class DelegatingStub(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(cancellationToken);
    }
}
