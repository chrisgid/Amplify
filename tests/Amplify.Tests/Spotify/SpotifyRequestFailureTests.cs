using System.Net;
using Amplify.Core.Spotify;

namespace Amplify.Tests.Spotify;

public class SpotifyRequestFailureTests
{
    [Theory]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    [InlineData(HttpRequestError.ProxyTunnelError)]
    public void RequestsThatNeverReachedSpotifyAreTransportFailures(HttpRequestError error)
    {
        var exception = new HttpRequestException(error, "no route");

        Assert.True(SpotifyRequestFailure.IsTransport(exception));
        Assert.True(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Fact]
    public void AStatusCodeFailureIsNotATransportFailure()
    {
        // The request reached Spotify and got an answer, so the token was presented and the caller
        // can draw conclusions about it — the opposite of a transport failure.
        var exception = new HttpRequestException(
            HttpRequestError.Unknown, "forbidden", null, HttpStatusCode.Forbidden);

        Assert.False(SpotifyRequestFailure.IsTransport(exception));
    }

    [Fact]
    public void AHandThrownExceptionIsNotMistakenForATransportFailure()
    {
        // SpotifyTokenClient throws one of these by hand when a 200 response omits the access token.
        // StatusCode is null there too, so the error code is what keeps it out of the transport set —
        // treating it as an outage would stop a genuinely broken session from ever latching Error.
        var exception = new HttpRequestException("Spotify's token response did not include an access token.");

        Assert.False(SpotifyRequestFailure.IsTransport(exception));
        Assert.False(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public void ServerSideAndTimeoutStatusesCountAsUnreachable(HttpStatusCode status)
    {
        // Spotify isn't answering usefully, but it will again — the same shape of problem as a dead
        // network, and it clears itself the same way.
        var exception = new HttpRequestException(HttpRequestError.Unknown, "server error", null, status);

        Assert.True(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void PersistentClientErrorsAreNotAnOutage(HttpStatusCode status)
    {
        // The regression guard for the false-promise case: a user whose Spotify app registration is
        // missing user-read-playback-state gets a 403 on every single poll. Calling that an outage
        // would hide their account behind "check your internet connection" permanently, blaming the
        // wrong thing and promising a recovery that can never arrive.
        var exception = new HttpRequestException(HttpRequestError.Unknown, "client error", null, status);

        Assert.False(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Fact]
    public void AClientTimeoutCountsAsUnreachable()
    {
        // HttpClient surfaces its own timeout as TaskCanceledException wrapping TimeoutException,
        // not as HttpRequestException. The read paths pass no caller token, so a cancellation here
        // is always the request giving up, never the user.
        var exception = new TaskCanceledException("timed out", new TimeoutException());

        Assert.True(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Fact]
    public void AnUnrelatedExceptionIsNotAnOutage()
    {
        Assert.False(SpotifyRequestFailure.MeansUnreachable(new InvalidOperationException("Not connected.")));
    }
}
