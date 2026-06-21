using System.Net;
using System.Text;
using Amplify.Core.Auth;
using Amplify.Core.Spotify;
using NSubstitute;

namespace Amplify.Tests.Spotify;

public sealed class SpotifyClientTests
{
    private const string _accessToken = "tok-123";

    [Fact]
    public async Task GetPlayerStateNoContentReportsNoActiveDevice()
    {
        (SpotifyClient client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.False(state.HasActiveDevice);
        Assert.Equal(0, state.VolumePercent);
        Assert.Null(state.DeviceName);
    }

    [Fact]
    public async Task GetPlayerStateWithDeviceMapsVolumeAndName()
    {
        const string json = """
            {
              "device": { "is_active": true, "name": "Kitchen speaker", "volume_percent": 42 },
              "is_playing": true
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.True(state.HasActiveDevice);
        Assert.Equal(42, state.VolumePercent);
        Assert.Equal("Kitchen speaker", state.DeviceName);
    }

    [Fact]
    public async Task GetPlayerStateSendsBearerTokenFromAuthService()
    {
        (SpotifyClient client, RecordingHandler handler) =
            CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.GetPlayerStateAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal(_accessToken, handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SetVolumeSendsPutToVolumeEndpointWithPercent()
    {
        (SpotifyClient client, RecordingHandler handler) =
            CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetVolumeAsync(50);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);
        Assert.Equal("/v1/me/player/volume", handler.LastRequest.RequestUri?.AbsolutePath);
        Assert.Equal("?volume_percent=50", handler.LastRequest.RequestUri?.Query);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-5, 0)]
    [InlineData(37, 37)]
    public async Task SetVolumeClampsPercentToValidRange(int requested, int expected)
    {
        (SpotifyClient client, RecordingHandler handler) =
            CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetVolumeAsync(requested);

        Assert.Equal($"?volume_percent={expected}", handler.LastRequest?.RequestUri?.Query);
    }

    private static (SpotifyClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.spotify.com/") };

        IAuthService auth = Substitute.For<IAuthService>();
        auth.GetAccessTokenAsync().Returns(_accessToken);

        return (new SpotifyClient(http, auth), handler);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}
