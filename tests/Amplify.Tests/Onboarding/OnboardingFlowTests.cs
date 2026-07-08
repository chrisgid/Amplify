using Amplify.Core.Auth;
using Amplify.Core.Onboarding;

namespace Amplify.Tests.Onboarding;

public class OnboardingFlowTests
{
    [Fact]
    public void StartsOnWelcome()
    {
        var flow = new OnboardingFlow();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("abc123", true)]
    public void CanBeginConnectRequiresNonBlankClientId(string? clientId, bool expected)
    {
        var flow = new OnboardingFlow();

        Assert.Equal(expected, flow.CanBeginConnect(clientId));
    }

    [Fact]
    public void CanBeginConnectIsFalseOnceAuthorizing()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();

        Assert.False(flow.CanBeginConnect("abc123"));
    }

    [Fact]
    public void BeginConnectMovesToAuthorizingAndRaisesChanged()
    {
        var flow = new OnboardingFlow();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.BeginConnect();

        Assert.Equal(OnboardingPhase.Authorizing, flow.Phase);
        Assert.True(raised);
    }

    [Fact]
    public void BeginConnectClearsPriorDeniedAndError()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        flow.OnConnectResult(new AuthResult(Success: false, Denied: true, Error: null));

        flow.BeginConnect();

        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
    }

    [Fact]
    public void SuccessLeavesPhaseAtAuthorizing()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();

        flow.OnConnectResult(new AuthResult(Success: true, Denied: false, Error: null));

        Assert.Equal(OnboardingPhase.Authorizing, flow.Phase);
    }

    [Fact]
    public void DenialReturnsToWelcomeAndSetsDenied()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.OnConnectResult(new AuthResult(Success: false, Denied: true, Error: null));

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.True(flow.Denied);
        Assert.Null(flow.ErrorMessage);
        Assert.True(raised);
        Assert.True(flow.CanBeginConnect("abc123"));
    }

    [Fact]
    public void OtherFailureReturnsToWelcomeAndSetsErrorMessage()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();

        flow.OnConnectResult(new AuthResult(Success: false, Denied: false, Error: "Network unreachable."));

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Equal("Network unreachable.", flow.ErrorMessage);
    }

    [Fact]
    public void CancelWhileAuthorizingReturnsToWelcomeAndRaisesChanged()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.Cancel();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
        Assert.True(raised);
        Assert.True(flow.CanBeginConnect("abc123"));
    }

    [Fact]
    public void ResetAfterSuccessReturnsToWelcome()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        flow.OnConnectResult(new AuthResult(Success: true, Denied: false, Error: null));
        // Success deliberately leaves the phase at Authorizing; a later re-entry must clear it.
        Assert.Equal(OnboardingPhase.Authorizing, flow.Phase);

        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.Reset();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
        Assert.True(raised);
        Assert.True(flow.CanBeginConnect("abc123"));
    }

    [Fact]
    public void ResetClearsDeniedAndError()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        flow.OnConnectResult(new AuthResult(Success: false, Denied: false, Error: "Network unreachable."));

        flow.Reset();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
    }

    [Fact]
    public void ResetOnCleanWelcomeIsANoOp()
    {
        var flow = new OnboardingFlow();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.Reset();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(raised);
    }

    [Fact]
    public void CancelWhileWelcomeIsANoOp()
    {
        var flow = new OnboardingFlow();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        flow.Cancel();

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(raised);
    }

    [Fact]
    public void OnConnectResultIsIgnoredWhenNotAuthorizing()
    {
        var flow = new OnboardingFlow();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        // Never began an attempt, so Phase is already Welcome — a result arriving here is stale.
        flow.OnConnectResult(new AuthResult(Success: false, Denied: true, Error: null));

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.False(raised);
    }

    [Fact]
    public void OnConnectResultAfterCancelIsDiscarded()
    {
        var flow = new OnboardingFlow();
        flow.BeginConnect();
        flow.Cancel();
        var raised = false;
        flow.Changed += (_, _) => raised = true;

        // The abandoned attempt's result arrives after Cancel already reset the phase; it must not
        // resurrect Denied/ErrorMessage the user already dismissed.
        flow.OnConnectResult(new AuthResult(Success: false, Denied: false, Error: "Network unreachable."));

        Assert.Equal(OnboardingPhase.Welcome, flow.Phase);
        Assert.False(flow.Denied);
        Assert.Null(flow.ErrorMessage);
        Assert.False(raised);
    }
}
