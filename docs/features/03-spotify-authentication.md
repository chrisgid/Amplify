# Feature 03 — Spotify Authentication (OAuth PKCE)

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md) · Required by: 04, 05, 07, 12.
> **Read [`../specification.md` §6 (Spotify Web API client standards)](../specification.md#6-spotify-web-api-client-standards) before implementing — it is binding.**

## Summary

Handles connecting a user's Spotify account using the **Authorization Code with PKCE** flow,
securely storing and refreshing tokens, recording whether the account is **Premium**, and
disconnecting. This is the gateway for every Spotify-backed feature. There is **no client secret**
(PKCE). A **Free account connects successfully** — it is flagged non-Premium so downstream features
gate volume control and show an upgrade notice; it is not a connection failure.

## User stories

- As a user, I want to sign in to Spotify securely in my own browser, not inside the app.
- As a user, I want to stay signed in across restarts without re-authorising every time.
- As a user, I want a clear message if I decline the permission; and if my account is Free, I want
  to still connect but understand that volume control needs Premium.
- As a user, I want to disconnect my account and have my tokens removed.

## UX / behaviour

- **Connect** is triggered from onboarding ([04](./04-onboarding.md)) or settings reconnect.
- Amplify generates a PKCE `code_verifier`/`code_challenge` and a random `state`, starts a local
  redirect listener, then opens the **system browser** to the Spotify authorize URL.
- The browser shows Spotify's consent screen:
  - **Approve** → Spotify redirects to the local callback with `?code=…&state=…`; the app
    exchanges the code for tokens, validates `state`, records Premium status (Free accounts
    connect too, flagged non-Premium), and transitions to connected.
  - **Deny** → redirect carries `?error=access_denied&state=…`; the app shows the
    "access wasn't granted" notice and lets the user retry.
    *Reference:* `design/project/components/success.jsx` (success + denied pages),
    `components/onboarding.jsx` (denied notice), `components/app.jsx` (`oauthOutcome`).
- A small local **success/denied page** is served at the callback so the browser tab shows a
  friendly "You're connected / Access not granted — safe to close this tab" message.
- While exchanging/verifying, the UI shows a transient "Waiting for Spotify…" /
  "Verifying connection…" state.

## Acceptance criteria

- [ ] Uses **Authorization Code with PKCE**; no client secret anywhere in the app.
- [ ] Requests only `user-read-playback-state` and `user-modify-playback-state`.
- [ ] Redirect URI is a **loopback IP** on a single, fixed, pre-registered port —
      `http://127.0.0.1:49737/callback`, not `localhost`, not a wildcard (see Implementation
      guidance / port rationale below).
- [ ] `state` is random per attempt and validated on return; mismatches are rejected.
- [ ] On approval, tokens are obtained and the **refresh token is stored in the Credential
      Locker**; access token kept in memory with its expiry.
- [ ] The account's `product` is read via `GET /v1/me` and recorded as `Account.IsPremium`. A
      **Free account still connects** (`AuthResult.Success == true`, `NotPremium == true`); volume
      control is gated downstream (features 05/07) — non-Premium is **not** a connection failure.
- [ ] On denial (`error=access_denied`), the user sees the denied state and can retry.
- [ ] Access tokens refresh automatically before/at expiry (and on 401) without user action.
- [ ] Disconnect clears in-memory tokens and removes the stored refresh token.
- [ ] On next launch, a stored refresh token silently restores the session.

## Implementation guidance

- **Flow** (per the PKCE tutorial in §6):
  1. Create `code_verifier` (43–128 chars) and `code_challenge = base64url(SHA256(verifier))`.
  2. Start `HttpListener` on the registered loopback port `http://127.0.0.1:49737/`; use the
     matching `redirect_uri`.
  3. Open the authorize URL (`https://accounts.spotify.com/authorize`) with
     `response_type=code`, `client_id`, `redirect_uri`, `scope`, `state`,
     `code_challenge_method=S256`, `code_challenge`.
  4. On callback, validate `state`, then `POST https://accounts.spotify.com/api/token` with
     `grant_type=authorization_code`, `code`, `redirect_uri`, `client_id`, `code_verifier`.
  5. Store `refresh_token`; keep `access_token` + `expires_in` in memory.
- **Refresh:** `grant_type=refresh_token` (tutorial in §6). Refresh proactively when near
  expiry and reactively on a `401`. Spotify may return a new refresh token — persist it if so.
- **Service shape:** `IAuthService` with `Task<AuthResult> ConnectAsync()`,
  `Task<string> GetAccessTokenAsync()` (auto-refreshing), `Task DisconnectAsync()`,
  `bool IsConnected`, and an event/observable for state changes. `ISpotifyClient`
  ([07](./07-volume-control.md)) calls `GetAccessTokenAsync()`.
- **Port choice & registration:** there is **no standard OAuth callback port**. RFC 8252
  recommends an ephemeral OS-assigned loopback port, but **Spotify requires every redirect URI
  (including its port) to be pre-registered and matched exactly**, so a runtime-random port can't
  be used. Therefore we pin a **single fixed port** and the user registers just **one** redirect
  URI in the Spotify dashboard: **`49737`** — chosen from the IANA **dynamic/private range
  (49152–65535)** to minimise collisions, deliberately avoiding common dev ports
  (`3000/5000/5173/8000/8080/8888`). At runtime, bind to `49737` and use its matching
  `redirect_uri`. To change the port later, register the new one in the dashboard first.
- **Client ID (per-user model):** Amplify does **not** ship a Client ID. **Each user registers
  their own Spotify app** and supplies their **own Client ID** (avoids Spotify's development-mode
  25-user limit). The Client ID is **not secret** under PKCE; it is entered during onboarding and
  persisted in local settings (`settings.json`), **not** in source or the Credential Locker. Only
  the redirect port + scopes are shipped in `appsettings.json`. Registration steps are in
  [`../getting-started.md` §4/§6](../getting-started.md#4-configuration--the-per-user-client-id-model).
  > [Onboarding (feature 04)](./04-onboarding.md) captures the Client ID (guiding app registration)
  > and persists it to settings before connecting; **auth reads the Client ID from settings
  > (`AppSettings.SpotifyClientId`), never a shipped constant.** If it is missing/empty, route the
  > user back to onboarding rather than attempting the flow.
- **Contracts:** implement `IAuthService` exactly as defined in
  [`../contracts.md`](../contracts.md) (including the `ConnectionStateChanged` event consumed by
  features 04/05/12).
- **Local pages:** serve minimal HTML for success and `access_denied` (mirror the prototype's
  copy). After responding, stop the listener.
- **Confirm all request/response shapes against the OpenAPI spec** (§6); do not invent fields.

## Data & persistence

- **Refresh token** → Windows Credential Locker (`PasswordVault`), resource = "Amplify".
- **Access token + expiry** → in memory only.
- **Cached profile** (name, `product`, device label) → kept only as long as needed for the
  session UI; not persisted long-term (respect Spotify ToS on caching).

## Edge cases & error handling

- Port `49737` already in use → surface a clear error explaining the callback port is occupied
  and allow retry once it's free (there is no fallback port — only `49737` is registered).
- User closes the browser without deciding → timeout the listener; return to a retryable state.
- `state` mismatch / unexpected callback → reject and ask the user to try again.
- Token exchange/refresh failure → map to the **error** status in
  [feature 05](./05-connection-status.md); never crash.
- Free (non-Premium) account → connects successfully, flagged `NotPremium`; volume control stays
  gated and the user sees the upgrade message ([feature 05](./05-connection-status.md)). Not treated
  as an error or a half-connected state.
- Honour **429** with exponential backoff + `Retry-After` on token endpoints too.

## Dependencies

- Requires the shell ([01](./01-application-shell.md)). Drives onboarding
  ([04](./04-onboarding.md)) and status ([05](./05-connection-status.md)). Consumed by
  [07](./07-volume-control.md). Disconnect path shared with [12](./12-reset-and-account.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- PKCE generation: verifier length/charset; challenge = base64url(SHA256(verifier)).
- `state` validation accepts matching and rejects mismatched/absent state.
- Token refresh: refreshes when expired/near-expiry; retries once on 401; persists rotated
  refresh tokens. (Mock the token HTTP endpoint.)
- Premium check maps `product` values correctly (premium vs free).
- Disconnect removes the stored refresh token. (Mock the Credential Locker behind an interface.)
- 429 backoff honours `Retry-After`.

## Out of scope

- The onboarding screen visuals (feature 04) and the connected status card (feature 05).
- Actual volume API calls (feature 07).

## Standards reminder

PKCE only (never Implicit/Client-Credentials); loopback `127.0.0.1` redirect; minimum scopes;
secure token storage; 429 backoff; OpenAPI as source of truth; comply with Spotify ToS.
