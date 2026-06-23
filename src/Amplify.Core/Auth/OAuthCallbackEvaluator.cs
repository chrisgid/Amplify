namespace Amplify.Core.Auth;

/// <summary>How a redirect from Spotify's consent screen should be interpreted.</summary>
public enum OAuthCallbackOutcome
{
    /// <summary>The <c>state</c> echoed back did not match this attempt; the callback is rejected.</summary>
    StateMismatch,

    /// <summary>The user declined consent (<c>error=access_denied</c>); a normal, retryable outcome.</summary>
    Denied,

    /// <summary>Consent was approved but no authorization code was present; treated as a failure.</summary>
    MissingCode,

    /// <summary>Consent was approved and a code is present; proceed to the token exchange.</summary>
    Approved,
}

/// <summary>
/// Pure decision logic for an OAuth redirect, separated from the interactive flow so it can be
/// unit-tested without a browser or listener. <c>state</c> is validated <em>before</em> anything
/// else so a forged or replayed redirect — even one carrying an error — is rejected outright.
/// </summary>
public static class OAuthCallbackEvaluator
{
    /// <summary>Classifies a redirect from its query values and the state expected for this attempt.</summary>
    public static OAuthCallbackOutcome Evaluate(string? code, string? state, string? error, string expectedState)
    {
        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            return OAuthCallbackOutcome.StateMismatch;
        }

        if (error is not null)
        {
            return OAuthCallbackOutcome.Denied;
        }

        return string.IsNullOrEmpty(code) ? OAuthCallbackOutcome.MissingCode : OAuthCallbackOutcome.Approved;
    }
}
