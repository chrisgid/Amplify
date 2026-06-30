using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Amplify.Core.Auth;

namespace Amplify.App.Auth;

/// <summary>The query values Spotify appends to the loopback redirect after the consent screen.</summary>
/// <param name="Code">The authorization code on approval, otherwise <c>null</c>.</param>
/// <param name="State">The opaque state echoed back for correlation.</param>
/// <param name="Error">The error code (e.g. <c>access_denied</c>) on denial, otherwise <c>null</c>.</param>
internal sealed record OAuthCallback(string? Code, string? State, string? Error);

/// <summary>
/// Listens on the registered loopback redirect URI for the single OAuth callback, serves a friendly
/// "safe to close this tab" page, and returns the parsed query. Bound to the explicit
/// <c>127.0.0.1</c> host so a non-elevated packaged app needs no URL reservation.
/// </summary>
internal sealed class LoopbackCallbackListener : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _redirectPath;

    public LoopbackCallbackListener(int port)
    {
        _redirectPath = SpotifyOAuthConstants.RedirectPath;
        // Explicit loopback prefix (not "+"/"localhost") so HTTP.sys grants it without a urlacl.
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    /// <summary>Begins listening. Call once before <see cref="WaitForCallbackAsync"/>.</summary>
    public void Start() => _listener.Start();

    /// <summary>
    /// Waits for the browser to hit the redirect path, responds with the success or denied page, and
    /// returns the parsed query. Cancellation (e.g. a timeout) stops the listener and surfaces as an
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<OAuthCallback> WaitForCallbackAsync(CancellationToken ct)
    {
        // GetContextAsync has no cancellation overload; stopping the listener aborts the pending wait.
        using CancellationTokenRegistration registration = ct.Register(_listener.Stop);

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            // Ignore incidental requests (e.g. favicon) so only the real callback completes the flow.
            if (!string.Equals(context.Request.Url?.AbsolutePath, _redirectPath, StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                continue;
            }

            System.Collections.Specialized.NameValueCollection query = context.Request.QueryString;
            var callback = new OAuthCallback(query["code"], query["state"], query["error"]);

            await WritePageAsync(context.Response, callback.Error is null).ConfigureAwait(false);
            return callback;
        }
    }

    // The pages are authored as standalone .html files under Auth/Pages and embedded at build time
    // so they ship with the assembly; edit those files to change the post-consent browser page.
    private const string _connectedPageResource = "Amplify.App.Auth.Pages.connected.html";
    private const string _accessDeniedPageResource = "Amplify.App.Auth.Pages.access-denied.html";
    private static readonly ConcurrentDictionary<string, byte[]> _pageCache = new();

    private static async Task WritePageAsync(HttpListenerResponse response, bool success)
    {
        byte[] body = LoadPage(success ? _connectedPageResource : _accessDeniedPageResource);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body).ConfigureAwait(false);
        response.Close();
    }

    private static byte[] LoadPage(string resourceName) => _pageCache.GetOrAdd(resourceName, static name =>
    {
        Assembly assembly = typeof(LoopbackCallbackListener).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded OAuth callback page '{name}' was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    });

    public void Dispose() => ((IDisposable)_listener).Dispose();
}
