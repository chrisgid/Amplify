namespace Amplify.Core.Spotify;

/// <summary>
/// Turns the outcome of each player-state read into a single "is Spotify reachable" answer for the
/// status block. Deliberately <em>not</em> part of <see cref="Amplify.Core.Auth.ConnectionState"/>:
/// an auth failure latches until the user re-authorises, whereas being unreachable is transient and
/// must clear itself the moment a read succeeds, so conflating the two would invite a fix that
/// gates recovery behind a button press.
/// </summary>
/// <remarks>
/// Purely advisory — it records outcomes and reports a flag. It never asks the poller to stop, back
/// off, or wait for the user: the poll keeps running on its normal cadence while unreachable, which
/// is what lets the app heal on its own when the network returns.
/// Not thread-safe; the owner mutates it from one thread (the UI thread).
/// </remarks>
public sealed class ReachabilityTracker
{
    /// <summary>
    /// How many consecutive failed reads it takes to call Spotify unreachable. Long enough that a
    /// single blip or one dropped request doesn't flap the status block, short enough that a real
    /// outage is surfaced promptly.
    /// </summary>
    /// <remarks>
    /// Counted in reads, not in clock time: reads collapse onto one in-flight request, so a tick that
    /// arrives while a slow read is still running is swallowed rather than counted. With the poller's
    /// 5s cadence and the client's 10s connect timeout that puts the usual case at roughly 15s and the
    /// worst case at well under a minute — but it is not a deadline, and a request that hangs after
    /// connecting can stretch it further.
    /// </remarks>
    public const int UnreachableThreshold = 3;

    private int _consecutiveFailures;

    /// <summary>Whether reads have been failing for long enough to tell the user about it.</summary>
    public bool IsUnreachable { get; private set; }

    /// <summary>
    /// Records a failed read. Returns <c>true</c> only when this read flipped
    /// <see cref="IsUnreachable"/>, so the caller raises one event per transition rather than one
    /// per poll.
    /// </summary>
    public bool RecordFailure()
    {
        // Stop counting at the threshold: the count beyond it carries no meaning and an outage left
        // overnight would otherwise run the counter up indefinitely.
        if (_consecutiveFailures < UnreachableThreshold)
        {
            _consecutiveFailures++;
        }

        return SetUnreachable(_consecutiveFailures >= UnreachableThreshold);
    }

    /// <summary>
    /// Records a successful read, clearing the failure run. Returns <c>true</c> when this cleared
    /// <see cref="IsUnreachable"/>. This is the only recovery path the app needs — no user action.
    /// </summary>
    public bool RecordSuccess() => Reset();

    /// <summary>
    /// Forgets the current run entirely — used when the question stops applying (e.g. the account
    /// disconnected), so a later reconnect starts from a clean slate instead of inheriting an old
    /// outage. Returns <c>true</c> when this cleared <see cref="IsUnreachable"/>.
    /// </summary>
    public bool Reset()
    {
        _consecutiveFailures = 0;
        return SetUnreachable(false);
    }

    private bool SetUnreachable(bool unreachable)
    {
        if (unreachable == IsUnreachable)
        {
            return false;
        }

        IsUnreachable = unreachable;
        return true;
    }
}
