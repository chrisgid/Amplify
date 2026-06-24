namespace Amplify.Core.Auth;

/// <summary>
/// Outcome of an interactive connect attempt (<see cref="IAuthService.ConnectAsync"/>).
/// </summary>
/// <remarks>
/// The failure cases are <see cref="Denied"/> (the user declined consent) and a non-null
/// <see cref="Error"/>.
/// </remarks>
/// <param name="Success">The account connected.</param>
/// <param name="Denied">The user declined the authorization request.</param>
/// <param name="Error">A user-readable failure message, or <c>null</c> on success/denial.</param>
public sealed record AuthResult(
    bool Success,
    bool Denied,
    string? Error);
