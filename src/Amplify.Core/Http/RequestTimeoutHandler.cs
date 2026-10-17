namespace Amplify.Core.Http;

/// <summary>
/// Bounds how long one attempt at a request may take, independently of
/// <see cref="HttpClient.Timeout"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HttpClient.Timeout"/> can't be used for this: it bounds the whole send, which for the
/// Spotify client includes <c>RateLimitHandler</c>'s <c>Retry-After</c> wait — a delay the server
/// asked for, legitimately long and deliberately uncapped. Placing the deadline on an inner handler
/// bounds each network attempt while leaving that wait alone.
/// </para>
/// <para>
/// <c>SocketsHttpHandler.ConnectTimeout</c> doesn't cover this either: it applies only to
/// establishing a new connection. A request sent on a pooled connection — the common case after a
/// laptop sleeps or roams off its network — can sit in TCP retransmits with nothing to cut it short,
/// which is exactly when the status block most needs to notice.
/// </para>
/// <para>
/// A tripped deadline surfaces the way <see cref="HttpClient"/> reports its own timeout — a
/// <see cref="TaskCanceledException"/> wrapping a <see cref="TimeoutException"/> — so callers can
/// tell it from a real cancellation without knowing this handler exists.
/// </para>
/// </remarks>
public sealed class RequestTimeoutHandler(TimeSpan timeout) : DelegatingHandler
{
    /// <summary>
    /// The deadline used for Spotify requests. Generously above any healthy response (the API answers
    /// well inside a second) while keeping a dead connection from swallowing poll ticks: the status
    /// block's outage threshold counts failed reads, so an unbounded attempt delays the warning for
    /// as long as it hangs.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource attempt =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(timeout);

        try
        {
            return await base.SendAsync(request, attempt.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            attempt.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Our deadline, not the caller's cancellation — report it as a timeout.
            throw new TaskCanceledException(
                $"The request did not complete within {timeout.TotalSeconds:0.#}s.",
                new TimeoutException());
        }
    }
}
