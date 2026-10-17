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

    [Theory]
    [InlineData(HttpRequestError.ResponseEnded)]
    [InlineData(HttpRequestError.HttpProtocolError)]
    [InlineData(HttpRequestError.VersionNegotiationError)]
    [InlineData(HttpRequestError.UserAuthenticationError)]
    [InlineData(HttpRequestError.ExtendedConnectNotSupported)]
    public void OtherConnectionLevelFailuresAreTransportToo(HttpRequestError error)
    {
        // These all mean the exchange broke down before a usable answer came back — a response that
        // ended early, a protocol or version failure, a proxy refusing to authenticate us. None of
        // them says anything about whether the Spotify session is still valid.
        var exception = new HttpRequestException(error, "connection level");

        Assert.True(SpotifyRequestFailure.IsTransport(exception));
        Assert.True(SpotifyRequestFailure.MeansUnreachable(exception));
    }

    [Fact]
    public void AnUnrecognisedErrorCodeFailsSafeTowardsTransport()
    {
        // The two ways of being wrong are not symmetric. Calling a transport failure an auth failure
        // stops the poll and sends the user to the browser for a session that never expired; calling
        // an auth failure a transport one just keeps polling through something that won't recover.
        // So a code this build has never heard of — including ones added to the framework later —
        // has to land on the transport side.
        var exception = new HttpRequestException((HttpRequestError)9999, "from the future");

        Assert.True(SpotifyRequestFailure.IsTransport(exception));
    }

    [Theory]
    [InlineData(HttpRequestError.InvalidResponse)]
    [InlineData(HttpRequestError.ConfigurationLimitExceeded)]
    public void AResponseThatArrivedButWasUnusableIsNotTransport(HttpRequestError error)
    {
        // Spotify answered; the answer was just no good. That is not an outage, and it must not stop
        // a genuinely broken session from latching.
        var exception = new HttpRequestException(error, "unusable response");

        Assert.False(SpotifyRequestFailure.IsTransport(exception));
        Assert.False(SpotifyRequestFailure.MeansUnreachable(exception));
    }
}
