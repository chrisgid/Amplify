using System.Net;

namespace Amplify.Core.Spotify;

/// <summary>
/// Classifies a failed Spotify request by what it actually says about Spotify's availability. Two
/// different decisions hang off this, and both go wrong if every <see cref="HttpRequestException"/>
/// is treated alike: whether to latch <c>ConnectionState.Error</c> (which costs the user a trip
/// through the browser), and whether to tell them their internet is down.
/// </summary>
public static class SpotifyRequestFailure
{
    /// <summary>
    /// Whether the request never reached Spotify at all — DNS, TCP, TLS or proxy. The access token
    /// was never presented, so nothing can be concluded about whether it is still valid; the caller
    /// must not treat this as an authorisation failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HttpRequestException.StatusCode"/> is <c>null</c> for every transport failure, but
    /// also for an exception thrown by hand after a response arrived, so the error code is checked
    /// too. It is checked as a deny-list rather than a list of known transport errors, because the
    /// two ways of being wrong are not symmetric: calling a transport failure an auth failure stops
    /// the poll and sends the user to the browser, while the reverse merely keeps polling through
    /// something that won't recover. So anything unrecognised — including error codes added to the
    /// framework after this was written — is treated as transport.
    /// </para>
    /// <para>
    /// The three exclusions are the cases where Spotify did answer: <see cref="HttpRequestError.Unknown"/>
    /// is what an exception constructed by hand carries (<c>SpotifyTokenClient</c> throws one when a
    /// <c>200</c> omits the access token), and <see cref="HttpRequestError.InvalidResponse"/> and
    /// <see cref="HttpRequestError.ConfigurationLimitExceeded"/> both describe a response that arrived
    /// and was unusable.
    /// </para>
    /// </remarks>
    public static bool IsTransport(HttpRequestException exception) =>
        exception.StatusCode is null && exception.HttpRequestError is not (
            HttpRequestError.Unknown or
            HttpRequestError.InvalidResponse or
            HttpRequestError.ConfigurationLimitExceeded);

    /// <summary>
    /// Whether this failure means "Spotify isn't answering, and may well answer again shortly" — the
    /// only kind of failure worth showing as an outage, because it is the only kind that clears
    /// itself. Transport failures and timeouts qualify, as do <c>408</c> and the <c>5xx</c> range.
    /// </summary>
    /// <remarks>
    /// A persistent <c>4xx</c> deliberately does not: a <c>403</c> from a missing
    /// <c>user-read-playback-state</c> scope would otherwise sit behind "check your internet
    /// connection" forever, blaming the wrong thing and promising a recovery that can never come.
    /// <c>401</c> (replayed with a fresh token) and <c>429</c> (retried) are handled in the client's
    /// handler pipeline and don't reach here in the normal case.
    /// </remarks>
    public static bool MeansUnreachable(Exception exception) => exception switch
    {
        HttpRequestException http =>
            IsTransport(http) ||
            http.StatusCode is HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError,

        // HttpClient's own timeout. The player-state and volume calls pass no caller token, so a
        // cancellation here is never the user giving up — it is always the request giving up.
        TaskCanceledException => true,

        _ => false,
    };
}
