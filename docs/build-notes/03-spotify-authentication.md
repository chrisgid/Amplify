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

## 2026-06-24 — Code-review fixes · feat/03-spotify-authentication

Five review findings on the Phase 1 work, all addressed:

- **Restore must never crash startup (was high).** `RestoreSessionAsync` caught only
  `HttpRequestException`, but `ReadFromJsonAsync` can throw `JsonException` and the `HttpClient`
  timeout surfaces as `TaskCanceledException` — either would escape to `App.OnLaunched`, which logs
  and **rethrows** (fail-fast). Broadened the restore catch to `Exception` (best-effort path → fall
  back to onboarding); the dead-token clear is still scoped to a token-endpoint `400/401`.
- **Transient refresh error latched `Connected → Error` forever (was medium-high).**
  `RefreshCoreAsync` set `Error` on failure but never reset on success, so a single near-expiry blip
  left the status UI stuck on Error even though subsequent refreshes worked. It now `SetState(Connected)`
  after a successful refresh (idempotent).
- **Captive dependency (was medium).** The singleton auth service captures the transient typed
  `SpotifyTokenClient`, pinning one `HttpMessageHandler` for the process — defeating
  `IHttpClientFactory` rotation, so a days-long session could reuse stale connections. Both typed
  clients (`SpotifyTokenClient` and the `ISpotifyClient`/`SpotifyClient`, which is likewise captured
  by a singleton) now set `SocketsHttpHandler.PooledConnectionLifetime = 2 min` so pooled connections
  recycle regardless of capture.
- **401-replay dropped the request body (was latent).** `SpotifyAuthorizationHandler.Clone` copied
  headers but not `Content`; harmless today (GET / bodyless PUT) but a future body-carrying request
  would silently replay empty on a 401. Clone now reuses the original (buffered) `Content` and skips
  the stale `Authorization` header explicitly. New handler test asserts the body survives a retry.
- **`Retry-After: 0` treated as missing (was low).** `delta > Zero` rejected a literal zero and fell
  through to the 1/2/4s exponential backoff; changed to `>= Zero` so "retry now" is honoured. New
  token-client test covers it.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test … --filter "Category!=RequiresSpotify"` → **76 passed** (+2 new).
  - `dotnet format --verify-no-changes` → clean.

## 2026-06-25 — Add cancellation support to ConnectAsync · feat/04-onboarding

Driven by feature 04: onboarding wanted a Cancel button for the "Authorizing" state (e.g. the user
closed the browser tab) that actually aborts the wait, not just one that discards a stale result
while the underlying attempt keeps running until its own timeout.

- **Contract change (in `contracts.md`):** `IAuthService.ConnectAsync()` →
  `ConnectAsync(CancellationToken cancellationToken = default)`. No other signatures changed; no
  other call sites existed outside `OnboardingViewModel`.
- **Implementation:** `SpotifyAuthService.ConnectAsync` links the caller's token with its existing
  internal 5-minute consent timeout via `CancellationTokenSource.CreateLinkedTokenSource`, and passes
  the linked token through to both the loopback wait and the code-exchange call. The
  `OperationCanceledException` catch now distinguishes the two causes via
  `cancellationToken.IsCancellationRequested` so the returned `AuthResult.Error` message says
  "cancelled" vs "timed out" accurately — both still resolve to a normal (non-throwing) `AuthResult`,
  matching the existing behaviour for the internal timeout.
- **Deferred / known gaps:** None — this closes the gap noted when feature 04's Cancel button was
  first added (it previously only discarded results client-side).

## 2026-06-25 — Code-review fixes (credential leak + message accuracy) · feat/04-onboarding

Two findings from review of the cancellation support added above:

- **Cancelling after the token exchange leaked a connectable credential (was high).**
  `CompleteConnectAsync` calls `ApplyTokens` (which persists the refresh token to the Credential
  Locker immediately) before reading the account via `GetAccountAsync`. If the caller cancelled in
  that window, the outer `catch (OperationCanceledException)` reported the attempt as cancelled, but
  the refresh token was already on disk — the next launch's `RestoreSessionAsync` would silently
  reconnect using it, contradicting the "cancelled" outcome the user was shown. Fixed by wrapping
  `GetAccountAsync` in its own `try`/`catch (OperationCanceledException)` that calls `ClearTokens()`
  and `_refreshTokenStore.Clear()` before rethrowing, so a cancelled attempt never leaves a usable
  session behind. This path only became reachable once Cancel started actually cancelling the call
  (previously it only discarded the result while the attempt kept running to completion in the
  background, so the credential would always end up persisted regardless of "cancelling").
- **Cancel-vs-timeout message picked the wrong source on a near-simultaneous race (was low).**
  The `OperationCanceledException` catch used `cancellationToken.IsCancellationRequested` (the
  external/caller token) to decide between "cancelled" and "timed out", which could mislabel a
  timeout that fired in the same tick the caller also cancelled. Switched to checking the internal
  `timeout` `CancellationTokenSource`'s own flag directly — required moving its declaration (and the
  linked source's) above the `try` block it was previously scoped inside, since the `catch` needs to
  read it.
- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test tests/Amplify.Tests` → **91 passed**.
  - No `SpotifyAuthService`-specific unit tests cover the Credential-Locker rollback path directly
    (it requires a real `IRefreshTokenStore`/loopback/browser interaction, same as the rest of
    `ConnectAsync` — see the Phase 0/1 entries above on why this method's happy/cancel paths are
    manual-only). Re-verify via the feature 04 manual Cancel walk next time a desktop is available.

## 2026-06-30 — OAuth callback pages restyled to match the design (dark only)

The Phase 0 success/denied browser pages were a bare `<h1>`/`<p>` placeholder built inline as a C#
string in `LoopbackCallbackListener`. Reworked them to match the prototype's OAuth result pages.

- **Moved out of code:** the two pages are now standalone files under
  `src/Amplify.App/Auth/Pages/` (`connected.html`, `access-denied.html`), embedded via
  `<EmbeddedResource>` with explicit `LogicalName`s (`Amplify.App.Auth.Pages.connected.html` /
  `…access-denied.html`). `LoopbackCallbackListener.WritePageAsync` reads the matching resource
  through `Assembly.GetManifestResourceStream` and caches the bytes in a static
  `ConcurrentDictionary`. Editing the `.html` files (previewable directly in a browser) is now the
  way to change the pages — no C# edit needed.
- **Dark-mode only, by request.** The pages are static HTML served into the *system* browser, which
  can't read the app's live WinUI theme/accent, so a single variant was chosen. Tokens are hardcoded
  from the design's dark theme. A light variant was explicitly out of scope.
- **No accent colour, pared back to essentials.** Because these static pages can't follow the user's
  real Windows accent (the design's default `#0078D4` would just be a fixed guess), the accent was
  dropped entirely — and the design's accent-tinted top glow and the Amplify-mark footer were removed
  too. Each page is now a minimal centred card on a flat dark background (`#202020`): just the status
  badge, title, and message, with no accent-coloured chrome.
- **Based on `design/project/components/success.jsx`:** badge circle (success-green check /
  warning-yellow ✕), large display-font title, and body copy. The prototype's "Return to Amplify"
  button was intentionally omitted — a served web page can't reliably focus the desktop app, so a dead
  control was avoided.
- **No contract/behaviour change:** the listener still serves success vs denied off
  `callback.Error is null`; no tests depend on page markup.
- **Checks:** `dotnet build src/Amplify.App` → **0 warnings, 0 errors**; `dotnet test
  tests/Amplify.Tests` → **170 passed**; verified both resource names are present in the built
  assembly via reflection. Visual rendering is manual (open the `.html` files in a browser).
