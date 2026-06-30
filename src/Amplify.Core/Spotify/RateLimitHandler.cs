using System.Net;
using Amplify.Core.Http;

namespace Amplify.Core.Spotify;

/// <summary>
/// Handles Spotify's <c>429 Too Many Requests</c> by waiting and replaying the request, honouring the
/// <c>Retry-After</c> header when present and falling back to exponential backoff otherwise. Sits at
/// the outer edge of the Web API client's handler pipeline so a retry re-runs the inner handlers
/// (re-attaching a fresh bearer token). Retries are bounded, so a persistently throttled call still
/// surfaces the final <c>429</c> rather than looping forever.
/// </summary>
/// <remarks>
/// Public so the typed-client registration in the app assembly can add it to the pipeline. The
/// injected <see cref="TimeProvider"/> makes the backoff delay unit-testable without real waits.
/// </remarks>
public sealed class RateLimitHandler(TimeProvider? timeProvider = null) : DelegatingHandler
{
    // A small ceiling: enough to ride out a brief burst limit, not so many that a stuck call hangs.
    private const int _maxRetries = 3;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            // An HttpRequestMessage can't be sent twice, so each retry goes out on a fresh clone.
            HttpResponseMessage response = await base
                .SendAsync(attempt == 0 ? request : request.Clone(), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _maxRetries)
            {
                return response;
            }

            TimeSpan delay = ComputeDelay(response, attempt, _time);
            response.Dispose();
            await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves how long to wait before a retry: the server's <c>Retry-After</c> (delta seconds or an
    /// HTTP date) when given, otherwise an exponential backoff (0.5s, 1s, 2s, …) by attempt number. The
    /// date branch reads "now" from <paramref name="timeProvider"/> so it shares the clock the retry
    /// delay waits on.
    /// </summary>
    internal static TimeSpan ComputeDelay(HttpResponseMessage response, int attempt, TimeProvider timeProvider)
    {
        System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan until = date - timeProvider.GetUtcNow();
            return until > TimeSpan.Zero ? until : TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
    }
}
