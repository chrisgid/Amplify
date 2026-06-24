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

## 2026-06-23 — Phase 1 (completion) · feat/03-spotify-authentication

Scope: token refresh (proactive + reactive 401), single-flight, rotation persistence, silent
`RestoreSessionAsync`, `GET /v1/me` → Premium/Free, Client ID from settings, 429 backoff. Closes the
Phase 0 deferred list.

- **Deviations from spec/contracts:** None. `IAuthService` unchanged.

- **Contract changes:** None to `contracts.md`. Added **new public Core types** (not cross-feature
  service contracts, so not in `contracts.md`): `SpotifyTokenClient`, `TokenSet`,
  `OAuthCallbackEvaluator`/`OAuthCallbackOutcome`, `ISpotifyTokenProvider`,
  `SpotifyAuthorizationHandler`.

- **Assumptions / decisions:**
  - **Token HTTP extracted to `Amplify.Core` (`SpotifyTokenClient`).** The auth service lives in
    `Amplify.App` (touches `PasswordVault`/`Launcher`) and the test project references **Core only**,
    so the HTTP + JSON + 429 backoff was moved to a UI-free Core class (mirrors `SpotifyClient`) to be
    unit-testable with a fake handler. It uses **absolute URLs** so one `HttpClient` serves both the
    Accounts host and `api.spotify.com`. Registered via `AddHttpClient<SpotifyTokenClient>()`.
  - **Reactive-401 via a `DelegatingHandler` (`SpotifyAuthorizationHandler`), in Core.** Confirmed
    with the user. The bearer attach + refresh-and-retry-once moved **out of `SpotifyClient`** into the
    handler, added to the typed client pipeline with `.AddHttpMessageHandler<>()`. `SpotifyClient` no
    longer depends on `IAuthService`; its 3 bearer-assertion tests were **relocated** to
    `SpotifyAuthorizationHandlerTests` (coverage preserved, not weakened). The handler talks to auth
    through the internal-style seam `ISpotifyTokenProvider` (public in Core so the App registration and
    handler can name it) — kept separate from `IAuthService` so the handler depends only on get +
    force-refresh. `RefreshAccessTokenAsync(previousToken)` collapses a 401 burst: if the current token
    already differs from the one that failed, it returns the newer token without re-refreshing.
  - **Single-flight refresh** via `SemaphoreSlim`; double-checked freshness inside the lock. A 60s
    skew refreshes proactively before the stated expiry. `SpotifyAuthService` is now `IDisposable`
    (CA1001) to dispose the semaphore — the host disposes the singleton on shutdown.
  - **Client ID now read from `AppSettings.SpotifyClientId`** (via `ISettingsService`); empty →
    `ConnectAsync` returns an error / `RestoreSessionAsync` returns `false` (shell stays on
    onboarding). `DevClientIdSource` **deleted**; the temporary OnboardingPage box now persists the
    Client ID via `ISettingsService.Update` before connecting (full capture lands with onboarding 04).
  - **Restore failure on token-endpoint 400/401 clears the stored refresh token** (it's dead);
    transient failures keep it for a later attempt.
  - **`state` validation extracted to a pure `OAuthCallbackEvaluator`** (Core) so it's unit-testable
    without the browser/listener; `state` is checked **before** error/code so a forged denial is
    rejected.

- **Deferred / known gaps:** None for this feature. (Onboarding UI/copy is feature 04; the connected
  status card is 05; the throwaway OnboardingPage controls remain until 04 replaces them.)

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test … --filter "Category!=RequiresSpotify"` → **75 passed** (was 12; +18 new auth tests,
    −3 relocated bearer tests net into the handler suite).
  - `dotnet format --verify-no-changes` → clean.
  - **Live OAuth/refresh still NOT auto-verifiable here** (needs a desktop, system browser, a real
    per-user Client ID with `http://127.0.0.1:49737/callback` registered, packaged identity for
    `PasswordVault`, and a Premium account). Manual checks to run on a dev desktop: connect → success
    page → window shows Connected + refresh token in the locker; **relaunch opens on Main** (restore);
    Disconnect → relaunch lands on Onboarding; connect a Free account → connects, flagged non-Premium;
    leave running past expiry / revoke → next API call refreshes (or re-auths) without a crash.

- **Verified facts:**
  - `Retry-After` is read from `HttpResponseHeaders.RetryAfter` (`RetryConditionHeaderValue`): seconds
    populate `.Delta`, an HTTP-date populates `.Date` — both handled, with an exponential fallback
    (1/2/4s, capped 8s) when the header is absent.
  - `IHttpClientFactory.AddHttpMessageHandler<T>()` resolves the handler from DI per request; the
    handler is registered `Transient`. The token client's own `HttpClient` is a **separate** typed
    client with **no** auth handler, so refresh/`GET /v1/me` can't recurse into the handler.
  - `AddHttpClient<SpotifyTokenClient>()` activates the **public** `(HttpClient)` constructor;
    the second `(HttpClient, Func<…>)` constructor is `internal` (test-only delay seam, reachable via
    the existing `InternalsVisibleTo("Amplify.Tests")` on Core) and is not chosen by DI.

## 2026-06-24 — Remove Premium detection (Spotify Feb-2026 API change) · feat/03-spotify-authentication

Scope: strip in-app Premium/Free detection entirely (code + contracts + docs). Found during manual
testing that `Account.IsPremium` was always `false`.

- **Why:** Spotify's **February 2026** Web API changes **removed the `product` field** from
  `GET /v1/me` (along with `country`/`email`/`explicit_content`/`followers`), with **no replacement**
  — there is no API way to read subscription tier anymore. Separately, the same release now **requires
  the owner of a Development-mode app to have active Premium** (the app stops working otherwise).
  Because Amplify uses the per-user self-registration model (each user owns their own dev app), the
  user *is* the owner, so **every connectable account is necessarily Premium** and a Free user can't
  run a working app at all. The Free-account branch the contracts/docs described was therefore both
  undetectable *and* unreachable. Verified via the [Feb-2026 migration guide](https://developer.spotify.com/documentation/web-api/tutorials/february-2026-migration-guide)
  and [changelog](https://developer.spotify.com/documentation/web-api/references/changes/february-2026).

- **Contract changes (in `contracts.md`):**
  - `Account` dropped `Plan` and `IsPremium` → now `(string DisplayName, string Initials)`.
  - `AuthResult` dropped `NotPremium` → now `(bool Success, bool Denied, string? Error)`.
  - `IVolumeController.CanControl` comment: "connected + Premium + HasActiveDevice" → "connected +
    HasActiveDevice".
  - The `ConnectionState` note that "a Free account is `Connected` + `IsPremium == false`" replaced
    with "there is no Free-vs-Premium distinction in the app".

- **Decisions:**
  - **Premium requirement is retained, enforcement is delegated to Spotify.** The app no longer
    pre-checks tier; a rejected volume call (`403`) is the only signal, handled reactively by feature
    07 (it already maps `403`/`404` to the no-controllable-device guidance). Documented this reframing
    in spec §1/§6, getting-started §4/§6, and features 03/04/05/07/12.
  - **`GetAccountAsync` simplified** to read only `display_name` (+ derived initials); the `product`
    JSON field and the premium-mapping test were removed, replaced by display-name/initials tests.
  - **Settings account subtitle** now a static `Settings_Account_PremiumSubtitle` = "Premium"
    (renamed from `Settings_Account_FreeSubtitle`); feature 12's account row shows the same.
  - Feature 05 rewritten: the connected state no longer has Premium/Free variants — the only
    "connected but can't control" presentation is **no active device** (driven by
    `PlayerState.HasActiveDevice`), unchanged in ownership.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test … --filter "Category!=RequiresSpotify"` → **74 passed** (was 75; the 3-case premium
    theory replaced by two display-name facts).
  - `dotnet format --verify-no-changes` → clean.

- **Note:** earlier entries above still describe the Premium-detection implementation — they are
  left intact (append-only log); this entry supersedes them.
