using Amplify.Core.Auth;

namespace Amplify.Tests.Auth;

public sealed class OAuthCallbackEvaluatorTests
{
    private const string _expectedState = "expected-state";

    [Fact]
    public void ApprovedWhenStateMatchesAndCodePresent()
    {
        OAuthCallbackOutcome outcome =
            OAuthCallbackEvaluator.Evaluate("auth-code", _expectedState, error: null, _expectedState);

        Assert.Equal(OAuthCallbackOutcome.Approved, outcome);
    }

    [Theory]
    [InlineData("different-state")]
    [InlineData(null)]
    [InlineData("")]
    public void StateMismatchWhenStateDiffersOrAbsent(string? returnedState)
    {
        OAuthCallbackOutcome outcome =
            OAuthCallbackEvaluator.Evaluate("auth-code", returnedState, error: null, _expectedState);

        Assert.Equal(OAuthCallbackOutcome.StateMismatch, outcome);
    }

    [Fact]
    public void DeniedWhenErrorPresentAndStateMatches()
    {
        OAuthCallbackOutcome outcome =
            OAuthCallbackEvaluator.Evaluate(code: null, _expectedState, "access_denied", _expectedState);

        Assert.Equal(OAuthCallbackOutcome.Denied, outcome);
    }

    [Fact]
    public void StateCheckedBeforeErrorSoForgedDenialIsRejected()
    {
        OAuthCallbackOutcome outcome =
            OAuthCallbackEvaluator.Evaluate(code: null, "wrong-state", "access_denied", _expectedState);

        Assert.Equal(OAuthCallbackOutcome.StateMismatch, outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingCodeWhenApprovedWithoutCode(string? code)
    {
        OAuthCallbackOutcome outcome =
            OAuthCallbackEvaluator.Evaluate(code, _expectedState, error: null, _expectedState);

        Assert.Equal(OAuthCallbackOutcome.MissingCode, outcome);
    }
}
