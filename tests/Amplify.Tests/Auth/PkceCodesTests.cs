using Amplify.Core.Auth;

namespace Amplify.Tests.Auth;

public class PkceCodesTests
{
    private const string _unreservedChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    [Fact]
    public void GenerateVerifierLengthIsWithinRfcRange()
    {
        string verifier = PkceCodes.Generate().Verifier;

        Assert.InRange(verifier.Length, 43, 128);
    }

    [Fact]
    public void GenerateVerifierUsesOnlyUnreservedCharacters()
    {
        string verifier = PkceCodes.Generate().Verifier;

        Assert.All(verifier, c => Assert.Contains(c, _unreservedChars));
    }

    [Fact]
    public void GenerateProducesDistinctVerifiersAndState()
    {
        PkceCodes first = PkceCodes.Generate();
        PkceCodes second = PkceCodes.Generate();

        Assert.NotEqual(first.Verifier, second.Verifier);
        Assert.NotEqual(first.State, second.State);
    }

    [Fact]
    public void GenerateChallengeMatchesItsVerifier()
    {
        PkceCodes codes = PkceCodes.Generate();

        Assert.Equal(PkceCodes.ComputeChallenge(codes.Verifier), codes.Challenge);
    }

    [Fact]
    public void GenerateStateIsNonEmptyAndUrlSafe()
    {
        string state = PkceCodes.Generate().State;

        Assert.False(string.IsNullOrEmpty(state));
        Assert.DoesNotContain('+', state);
        Assert.DoesNotContain('/', state);
        Assert.DoesNotContain('=', state);
    }

    // RFC 7636 Appendix B worked example: an independent check that the challenge is the
    // base64url (unpadded) SHA-256 of the verifier.
    [Fact]
    public void ComputeChallengeMatchesRfc7636Vector()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.Equal(expectedChallenge, PkceCodes.ComputeChallenge(verifier));
    }

    [Fact]
    public void ComputeChallengeIsUrlSafeAndUnpadded()
    {
        string challenge = PkceCodes.ComputeChallenge(PkceCodes.Generate().Verifier);

        Assert.DoesNotContain('+', challenge);
        Assert.DoesNotContain('/', challenge);
        Assert.DoesNotContain('=', challenge);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ComputeChallengeRejectsNullOrEmptyVerifier(string? verifier)
    {
        Assert.ThrowsAny<ArgumentException>(() => PkceCodes.ComputeChallenge(verifier!));
    }
}
