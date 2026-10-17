using Amplify.Core.Spotify;

namespace Amplify.Tests.Spotify;

public class ReachabilityTrackerTests
{
    [Fact]
    public void StartsReachable()
    {
        var tracker = new ReachabilityTracker();

        Assert.False(tracker.IsUnreachable);
    }

    [Fact]
    public void OneFailureShortOfTheThresholdStaysReachable()
    {
        // A single dropped request or a brief blip must not flap the status block.
        var tracker = new ReachabilityTracker();

        for (int i = 0; i < ReachabilityTracker.UnreachableThreshold - 1; i++)
        {
            Assert.False(tracker.RecordFailure());
            Assert.False(tracker.IsUnreachable);
        }
    }

    [Fact]
    public void ReachingTheThresholdTurnsUnreachableAndReportsTheTransition()
    {
        var tracker = new ReachabilityTracker();

        for (int i = 0; i < ReachabilityTracker.UnreachableThreshold - 1; i++)
        {
            tracker.RecordFailure();
        }

        Assert.True(tracker.RecordFailure());
        Assert.True(tracker.IsUnreachable);
    }

    [Fact]
    public void FailuresMustBeConsecutiveToCount()
    {
        var tracker = new ReachabilityTracker();

        // Fail right up to the edge, succeed, then fail up to the edge again: the run restarts, so
        // intermittent failures never accumulate into a false outage.
        for (int round = 0; round < 2; round++)
        {
            for (int i = 0; i < ReachabilityTracker.UnreachableThreshold - 1; i++)
            {
                tracker.RecordFailure();
            }

            tracker.RecordSuccess();
            Assert.False(tracker.IsUnreachable);
        }
    }

    [Fact]
    public void ASingleSuccessClearsTheUnreachableState()
    {
        // The regression guard for the whole fix: recovery is the next successful poll, full stop.
        // Nothing here latches, so there is no state only a button press can leave — if this ever
        // needs a reset/reconnect call to pass, the app has stopped healing itself.
        var tracker = new ReachabilityTracker();
        Fail(tracker, ReachabilityTracker.UnreachableThreshold);
        Assert.True(tracker.IsUnreachable);

        Assert.True(tracker.RecordSuccess());
        Assert.False(tracker.IsUnreachable);
    }

    [Fact]
    public void RecoveryIsImmediateHoweverLongTheOutageRan()
    {
        // A long outage must not take an equally long run of successes to climb back out of.
        var tracker = new ReachabilityTracker();
        Fail(tracker, ReachabilityTracker.UnreachableThreshold * 20);

        Assert.True(tracker.RecordSuccess());
        Assert.False(tracker.IsUnreachable);
    }

    [Fact]
    public void FurtherFailuresWhileUnreachableReportNoTransition()
    {
        // The caller raises an event per transition, not per poll — a five-minute outage should not
        // notify sixty times.
        var tracker = new ReachabilityTracker();
        Fail(tracker, ReachabilityTracker.UnreachableThreshold);

        Assert.False(tracker.RecordFailure());
        Assert.True(tracker.IsUnreachable);
    }

    [Fact]
    public void SuccessWhileAlreadyReachableReportsNoTransition()
    {
        var tracker = new ReachabilityTracker();

        Assert.False(tracker.RecordSuccess());
        Assert.False(tracker.IsUnreachable);
    }

    [Fact]
    public void ResetClearsAnOutageAndTheFailureRunBeneathIt()
    {
        // Used on disconnect: a reconnect starts from a clean slate, and the partial run that was
        // building up must go too, or the first failure afterwards would tip it straight over.
        var tracker = new ReachabilityTracker();
        Fail(tracker, ReachabilityTracker.UnreachableThreshold);

        Assert.True(tracker.Reset());
        Assert.False(tracker.IsUnreachable);

        for (int i = 0; i < ReachabilityTracker.UnreachableThreshold - 1; i++)
        {
            Assert.False(tracker.RecordFailure());
        }
    }

    [Fact]
    public void ResetWhileReachableReportsNoTransition()
    {
        var tracker = new ReachabilityTracker();
        tracker.RecordFailure();

        Assert.False(tracker.Reset());
    }

    private static void Fail(ReachabilityTracker tracker, int times)
    {
        for (int i = 0; i < times; i++)
        {
            tracker.RecordFailure();
        }
    }
}
