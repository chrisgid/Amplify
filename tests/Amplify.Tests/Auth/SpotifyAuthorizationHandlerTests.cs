using System.Net;
using Amplify.Core.Auth;
using NSubstitute;

namespace Amplify.Tests.Auth;

public sealed class SpotifyAuthorizationHandlerTests
{
    [Fact]
    public async Task AttachesBearerTokenFromProvider()
    {
        ISpotifyTokenProvider provider = Substitute.For<ISpotifyTokenProvider>();
        provider.GetAccessTokenAsync().Returns("access-1");
        var inner = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

        using HttpMessageInvoker invoker = CreateInvoker(provider, inner);
        await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal("Bearer", inner.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("access-1", inner.Requests[0].Headers.Authorization?.Parameter);
        await provider.DidNotReceive().RefreshAccessTokenAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task RefreshesAndRetriesOnceOnUnauthorized()
    {
        ISpotifyTokenProvider provider = Substitute.For<ISpotifyTokenProvider>();
        provider.GetAccessTokenAsync().Returns("stale");
        provider.RefreshAccessTokenAsync("stale").Returns("fresh");
        var inner = new StubHttpMessageHandler((_, attempt) =>
            new HttpResponseMessage(attempt == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));

        using HttpMessageInvoker invoker = CreateInvoker(provider, inner);
        HttpResponseMessage response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("stale", inner.Requests[0].Headers.Authorization?.Parameter);
        Assert.Equal("fresh", inner.Requests[1].Headers.Authorization?.Parameter);
        await provider.Received(1).RefreshAccessTokenAsync("stale");
    }

    [Fact]
    public async Task DoesNotRetryWhenResponseIsNotUnauthorized()
    {
        ISpotifyTokenProvider provider = Substitute.For<ISpotifyTokenProvider>();
        provider.GetAccessTokenAsync().Returns("token");
        var inner = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        using HttpMessageInvoker invoker = CreateInvoker(provider, inner);
        HttpResponseMessage response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(inner.Requests);
        await provider.DidNotReceive().RefreshAccessTokenAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ReplaysRequestBodyOnUnauthorizedRetry()
    {
        ISpotifyTokenProvider provider = Substitute.For<ISpotifyTokenProvider>();
        provider.GetAccessTokenAsync().Returns("stale");
        provider.RefreshAccessTokenAsync("stale").Returns("fresh");
        var inner = new StubHttpMessageHandler((_, attempt) =>
            new HttpResponseMessage(attempt == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));

        using HttpMessageInvoker invoker = CreateInvoker(provider, inner);
        var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player")
        {
            Content = new StringContent("the-body"),
        };
        await invoker.SendAsync(request, CancellationToken.None);

        // The retry must carry the original body, not an empty one.
        Assert.Equal(2, inner.Bodies.Count);
        Assert.Equal("the-body", inner.Bodies[1]);
        Assert.Equal("fresh", inner.Requests[1].Headers.Authorization?.Parameter);
    }

    private static HttpMessageInvoker CreateInvoker(ISpotifyTokenProvider provider, HttpMessageHandler inner)
    {
        var handler = new SpotifyAuthorizationHandler(provider) { InnerHandler = inner };
        return new HttpMessageInvoker(handler);
    }

    private static HttpRequestMessage Request() => new(HttpMethod.Get, "https://api.spotify.com/v1/me/player");
}
