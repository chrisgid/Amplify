using System.Net;
using System.Text;
using Amplify.Core.Spotify;

namespace Amplify.Tests.Spotify;

public sealed class SpotifyClientTests
{
    [Fact]
    public async Task GetPlayerStateNoContentReportsNoActiveDevice()
    {
        (SpotifyClient client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.False(state.HasActiveDevice);
        Assert.Equal(0, state.VolumePercent);
        Assert.Null(state.DeviceName);
        Assert.False(state.SupportsVolume);
    }

    [Fact]
    public async Task GetPlayerStateWithDeviceMapsVolumeAndName()
    {
        const string json = """
            {
              "device": {
                "is_active": true,
                "name": "Kitchen speaker",
                "volume_percent": 42,
                "supports_volume": true,
                "is_restricted": false
              },
              "is_playing": true
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.True(state.HasActiveDevice);
        Assert.Equal(42, state.VolumePercent);
        Assert.Equal("Kitchen speaker", state.DeviceName);
        Assert.True(state.SupportsVolume);
    }

    [Fact]
    public async Task GetPlayerStateReportsADeviceThatCannotSetVolume()
    {
        // The device is active and reports a level (typically 100), but won't accept volume commands —
        // the control has to know that before it tries to write.
        const string json = """
            {
              "device": {
                "is_active": true,
                "name": "Living room TV",
                "volume_percent": 100,
                "supports_volume": false,
                "is_restricted": false
              }
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.True(state.HasActiveDevice);
        Assert.Equal(100, state.VolumePercent);
        Assert.False(state.SupportsVolume);
    }

    [Fact]
    public async Task GetPlayerStateReportsARestrictedDeviceAsUncontrollable()
    {
        // A restricted device refuses every Web API command, so a volume write would fail the same way.
        const string json = """
            {
              "device": {
                "is_active": true,
                "name": "Some receiver",
                "volume_percent": 55,
                "supports_volume": true,
                "is_restricted": true
              }
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.True(state.HasActiveDevice);
        Assert.False(state.SupportsVolume);
    }

    [Fact]
    public async Task GetPlayerStateReportsAnInactiveDeviceAsNoDevice()
    {
        // Spotify still describes the last-used device when playback is idle. None of its detail
        // survives: an inactive device maps to the same empty state as a 204, so no consumer has to
        // remember to re-check HasActiveDevice before trusting the other fields.
        const string json = """
            {
              "device": {
                "is_active": false,
                "name": "Phone",
                "volume_percent": 40,
                "supports_volume": true,
                "is_restricted": false
              }
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.False(state.HasActiveDevice);
        Assert.Equal(0, state.VolumePercent);
        Assert.Null(state.DeviceName);
        Assert.False(state.SupportsVolume);
    }

    [Fact]
    public async Task GetPlayerStateAssumesControllableWhenTheFlagsAreAbsent()
    {
        // Missing booleans deserialise to false, which would wrongly disable the control for any
        // payload that omits them — absent means "assume controllable" and let a rejected write decide.
        const string json = """
            {
              "device": { "is_active": true, "name": "Kitchen speaker", "volume_percent": 42 }
            }
            """;
        (SpotifyClient client, _) = CreateClient(_ => JsonResponse(json));

        PlayerState? state = await client.GetPlayerStateAsync();

        Assert.NotNull(state);
        Assert.True(state.SupportsVolume);
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

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SetVolumeMapsNoControllableDeviceToTypedException(HttpStatusCode status)
    {
        (SpotifyClient client, _) = CreateClient(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<DeviceNotControllableException>(() => client.SetVolumeAsync(40));
    }

    private static (SpotifyClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.spotify.com/") };
        return (new SpotifyClient(http), handler);
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
