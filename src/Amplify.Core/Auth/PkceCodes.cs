using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Amplify.Core.Auth;

/// <summary>
/// A freshly generated PKCE secret set for one authorization attempt: the high-entropy
/// <see cref="Verifier"/> kept private until the token exchange, the public
/// <see cref="Challenge"/> sent on the authorize URL, and an opaque <see cref="State"/> used to
/// correlate the callback and reject forged/replayed redirects.
/// </summary>
/// <remarks>
/// Per RFC 7636: the verifier is a random string of unreserved characters; the challenge is the
/// base64url (unpadded) SHA-256 of the verifier's ASCII bytes.
/// </remarks>
public sealed record PkceCodes(string Verifier, string Challenge, string State)
{
    /// <summary>The unreserved character set permitted in a PKCE <c>code_verifier</c> (RFC 7636).</summary>
    private const string _verifierAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>Verifier length, comfortably inside the RFC's 43–128 character range.</summary>
    private const int _verifierLength = 64;

    /// <summary>Random bytes behind the opaque <c>state</c> value.</summary>
    private const int _stateByteLength = 32;

    /// <summary>Generates a new verifier, its derived challenge, and a random state.</summary>
    public static PkceCodes Generate()
    {
        string verifier = RandomNumberGenerator.GetString(_verifierAlphabet, _verifierLength);
        string state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(_stateByteLength));
        return new PkceCodes(verifier, ComputeChallenge(verifier), state);
    }

    /// <summary>Computes the base64url (unpadded) SHA-256 challenge for a given verifier.</summary>
    public static string ComputeChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(verifier);
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url.EncodeToString(hash);
    }
}
