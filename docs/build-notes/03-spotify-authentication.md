# Build notes — Feature 03: Spotify Authentication

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-21 — Phase 0 (walking-skeleton sliver) · feat/03-spotify-authentication

Scope: happy-path PKCE connect on the real `127.0.0.1:49737` loopback, store the refresh token in
the Credential Locker, expose the access token via `GetAccessTokenAsync()`. Refresh/rotation,
single-flight, silent restore, and Premium/Free handling are deferred to Phase 1.

- **Deviations from spec/contracts:** None. `IAuthService`, `ConnectionState`, `Account`, and
  `AuthResult` implemented verbatim from `contracts.md`.

- **Contract changes:** None.

- **Assumptions / decisions:**
  - **Client ID source is temporary.** Onboarding (04) and `ISettingsService` (10) don't exist yet,
    so the Phase 0 connect reads the per-user Client ID from a throwaway App-local singleton
    (`DevClientIdSource`), fed by a temporary Client ID `TextBox` + Connect button + status text in
    `MainWindow`. All of this is throwaway scaffolding to be removed when onboarding lands and auth
    reads `AppSettings.SpotifyClientId`. `MainWindow` now takes ctor deps (`IAuthService`,
    `DevClientIdSource`); `DevClientIdSource` is `public` only so the public `MainWindow` ctor
    doesn't trip CS0051 (less-accessible-parameter) — revisit when the temp UI is removed.
  - **Refresh-token store behind `IRefreshTokenStore` (Core).** Introduced an internal seam over the
    Credential Locker (not a cross-feature contract, so not added to `contracts.md`) so the
    disconnect/refresh logic is mockable in the Phase 1 tests. Phase 0 implements `DisconnectAsync`
    (clear in-memory + `Clear()`) even though Phase 0 scope didn't strictly require it — it's trivial
    and keeps the seam honest.
  - **`HttpListener` (not `TcpListener`)** for the loopback callback, bound to the explicit
    `http://127.0.0.1:{port}/` prefix; ignores non-`/callback` requests (e.g. favicon). 5-minute
    cancellation so a closed browser doesn't hang the listener.
  - **Token endpoints are constants, not from the OpenAPI spec.** `authorize`/`api/token` live on
    `accounts.spotify.com` (the Accounts service), which is not in the Web API OpenAPI document; they
    come from the PKCE tutorial and are centralised in `SpotifyOAuthConstants`. (Web API endpoints
    will still be taken from the OpenAPI spec in feature 07.)
  - **No manifest/capability change.** A full-trust packaged app needs no network capability for
    loopback listening or `PasswordVault` (see Verified facts).
  - Added `Microsoft.Extensions.Http` 10.0.9 (MIT) for `IHttpClientFactory` — the only new dependency.

- **Deferred / known gaps (→ Phase 1):** proactive/reactive token refresh + rotation persistence,
  single-flight refresh guard (`SemaphoreSlim`), silent `RestoreSessionAsync` (currently returns
  `false` — needs the refresh exchange), reading `GET /v1/me` to populate `CurrentAccount` /
  Premium-vs-Free (`CurrentAccount` stays `null` in Phase 0; `AuthResult.NotPremium` always `false`),
  429 backoff on the token endpoints, and `GetAccessTokenAsync` expiry/refresh (it returns the
  in-memory token or throws if not connected).

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test … --filter "Category!=RequiresSpotify"` → **12 passed** (3 existing + 9 new PKCE).
  - `dotnet format --verify-no-changes` → clean.
  - **Live OAuth flow is NOT auto-verifiable here** (needs a desktop session, the system browser, a
    real per-user Client ID with redirect URI `http://127.0.0.1:49737/callback` registered, and
    packaged identity for `PasswordVault`). The end-to-end connect (paste Client ID → Connect →
    browser consent → success page → window shows `Connected` → refresh token in the locker) is a
    manual check to run on a dev desktop; the listener/`PasswordVault`/browser-launch paths carry no
    unit tests by design (they can't run headless / in CI).

- **Verified facts (Microsoft Learn):**
  - A default packaged WinUI 3 app runs **full trust / medium IL** (not AppContainer). So
    `HttpListener` on the explicit `http://127.0.0.1:{port}/` prefix needs **no urlacl, no admin, no
    `CheckNetIsolation`, and no manifest network capability**. (The "loopback blocked for packaged
    apps" rule is AppContainer-only — do **not** switch the app to AppContainer or the OAuth listener
    breaks.) A strong-wildcard prefix (`http://+:port/`) *would* need a reservation — we avoid it.
  - `Windows.System.Launcher.LaunchUriAsync(uri)` opens the system default browser from a packaged
    desktop app with no HWND interop; call it on a user gesture.
  - `Windows.Security.Credentials.PasswordVault` is scoped to package identity; `Password` is lazy
    (`RetrievePassword()` before reading), and the locker raises HRESULT `0x80070490`
    (ERROR_NOT_FOUND) when a credential is absent — caught to treat "not stored" as `null`/no-op.
    Note: locker entries roam via the user's Microsoft account (acceptable for a refresh token).
  - .NET 10 BCL: `RandomNumberGenerator.GetString(choices, length)` (crypto-random string) and
    `System.Buffers.Text.Base64Url.EncodeToString` (unpadded, URL-safe) both exist — used for the
    PKCE verifier and the base64url(SHA-256) challenge.
  - `.editorconfig` enforces `_camelCase` for **all** private fields including `const`/`static
    readonly` (no const exclusion) — private constants are underscore-prefixed in this repo.
