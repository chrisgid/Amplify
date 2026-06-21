namespace Amplify.Core.Auth;

/// <summary>
/// Outcome of an interactive connect attempt (<see cref="IAuthService.ConnectAsync"/>).
/// </summary>
/// <remarks>
/// A Free account is a <em>success</em>: <see cref="Success"/> is <c>true</c> and
/// <see cref="NotPremium"/> is <c>true</c>. The failure cases are <see cref="Denied"/> (the user
/// declined consent) and a non-null <see cref="Error"/>.
/// </remarks>
/// <param name="Success">The account connected (Premium or Free).</param>
/// <param name="Denied">The user declined the authorization request.</param>
/// <param name="NotPremium">The connected account is not Premium; volume control is gated downstream.</param>
/// <param name="Error">A user-readable failure message, or <c>null</c> on success/denial.</param>
public sealed record AuthResult(
    bool Success,
    bool Denied,
    bool NotPremium,
    string? Error);
