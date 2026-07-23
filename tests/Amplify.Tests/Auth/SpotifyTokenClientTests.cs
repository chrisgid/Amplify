using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amplify.Core.Auth;

namespace Amplify.Tests.Auth;

public sealed class SpotifyTokenClientTests
{
    [Fact]
    public async Task RefreshSendsRefreshGrantWithClientIdAndToken()
    {
        StubHttpMessageHandler handler = Ok("""{ "access_token": "new-access", "expires_in": 3600 }""");
        SpotifyTokenClient client = CreateClient(handler);

        await client.RefreshAsync("client-123", "refresh-abc", TestContext.Current.CancellationToken);

        string body = Assert.Single(handler.Bodies)!;
        Assert.Equal(SpotifyOAuthConstants.TokenEndpoint, handler.Requests[0].RequestUri?.ToString());
        Assert.Contains("grant_type=refresh_token", body, StringComparison.Ordinal);
        Assert.Contains("client_id=client-123", body, StringComparison.Ordinal);
        Assert.Contains("refresh_token=refresh-abc", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshSurfacesRotatedRefreshToken()
    {
        SpotifyTokenClient client = CreateClient(
            Ok("""{ "access_token": "a", "refresh_token": "rotated", "expires_in": 3600 }"""));

        TokenSet tokens = await client.RefreshAsync("cid", "old", TestContext.Current.CancellationToken);

        Assert.Equal("a", tokens.AccessToken);
        Assert.Equal("rotated", tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshLeavesRefreshTokenNullWhenResponseOmitsIt()
    {
        SpotifyTokenClient client = CreateClient(Ok("""{ "access_token": "a", "expires_in": 3600 }"""));

        TokenSet tokens = await client.RefreshAsync("cid", "keep-me", TestContext.Current.CancellationToken);

        Assert.Null(tokens.RefreshToken);
    }

    [Fact]
    public async Task ExchangeCodeSendsAuthorizationCodeGrantWithVerifier()
    {
        StubHttpMessageHandler handler = Ok("""{ "access_token": "a", "expires_in": 3600 }""");
        SpotifyTokenClient client = CreateClient(handler);

        await client.ExchangeCodeAsync(
            "cid",
            "http://127.0.0.1:49737/callback",
            "the-code",
            "the-verifier",
            TestContext.Current.CancellationToken);

        string body = Assert.Single(handler.Bodies)!;
        Assert.Contains("grant_type=authorization_code", body, StringComparison.Ordinal);
        Assert.Contains("code=the-code", body, StringComparison.Ordinal);
        Assert.Contains("code_verifier=the-verifier", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshHonoursRetryAfterOn429ThenSucceeds()
    {
        var delays = new List<TimeSpan>();
        var handler = new StubHttpMessageHandler((_, attempt) => attempt == 1
            ? RateLimited(TimeSpan.FromSeconds(2))
            : JsonResponse("""{ "access_token": "after-retry", "expires_in": 3600 }"""));

        // Record delays instead of waiting so the backoff path runs instantly.
        var client = new SpotifyTokenClient(new HttpClient(handler), (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        TokenSet tokens = await client.RefreshAsync("cid", "rt", TestContext.Current.CancellationToken);

        Assert.Equal("after-retry", tokens.AccessToken);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(TimeSpan.FromSeconds(2), Assert.Single(delays));
    }

    [Fact]
    public async Task RefreshTreatsRetryAfterZeroAsRetryNow()
    {
        var delays = new List<TimeSpan>();
        var handler = new StubHttpMessageHandler((_, attempt) => attempt == 1
            ? RateLimited(TimeSpan.Zero)
            : JsonResponse("""{ "access_token": "after-retry", "expires_in": 3600 }"""));

        var client = new SpotifyTokenClient(new HttpClient(handler), (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        await client.RefreshAsync("cid", "rt", TestContext.Current.CancellationToken);

        // "Retry-After: 0" must be honoured as zero delay, not fall through to the exponential backoff.
        Assert.Equal(TimeSpan.Zero, Assert.Single(delays));
    }

    [Fact]
    public async Task GetAccountReadsDisplayNameAndDerivesInitials()
    {
        SpotifyTokenClient client = CreateClient(Ok("""{ "display_name": "Jane Doe" }"""));

        Account account = await client.GetAccountAsync("token", TestContext.Current.CancellationToken);

        Assert.Equal("Jane Doe", account.DisplayName);
        Assert.Equal("JD", account.Initials);
    }

    [Fact]
    public async Task GetAccountFallsBackWhenDisplayNameMissing()
    {
        SpotifyTokenClient client = CreateClient(Ok("""{ }"""));

        Account account = await client.GetAccountAsync("token", TestContext.Current.CancellationToken);

        Assert.Equal("Spotify user", account.DisplayName);
    }

    [Fact]
    public async Task GetAccountSendsBearerToken()
    {
        StubHttpMessageHandler handler = Ok("""{ "display_name": "A" }""");
        SpotifyTokenClient client = CreateClient(handler);

        await client.GetAccountAsync("tok-xyz", TestContext.Current.CancellationToken);

        Assert.Equal(SpotifyOAuthConstants.CurrentUserEndpoint, handler.Requests[0].RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("tok-xyz", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    private static SpotifyTokenClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler));

    private static StubHttpMessageHandler Ok(string json) =>
        new((_, _) => JsonResponse(json));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage RateLimited(TimeSpan retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        return response;
    }
}
