# Feature 04 — Onboarding / First-Run Experience

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md), [03 auth](./03-spotify-authentication.md),
> [10 settings](./10-settings-persistence.md) · Required by: 05.

## Summary

The first-run experience shown when no Spotify account is connected. It introduces Amplify and,
because Amplify uses a **per-user Spotify Client ID** model (see
[`getting-started.md` §4](../getting-started.md#4-configuration--the-per-user-client-id-model)),
walks the user through **creating their own Spotify developer app**: a numbered setup guide, the
redirect URI as a copy-to-clipboard chip, and a **Client ID** field that **gates** the primary
**Connect Spotify account** action. The Client ID is persisted to settings before the OAuth flow
runs. It also handles the in-progress and "access wasn't granted" states returned by the auth flow.

## User stories

- As a new user, I want to understand what Amplify does before connecting anything.
- As a new user, I want clear step-by-step guidance to create my own Spotify app and get a
  Client ID, with the exact redirect URI I can copy.
- As a user, I want a single obvious button to connect my Spotify account.
- As a user, I want reassurance about what permissions are requested and that I can revoke them.
- As a user, if I decline on Spotify, I want a clear way to try again.

## UX / behaviour

*Reference:* `design/project/components/onboarding.jsx`.

- **Welcome (default):** Amplify logo, the name "Amplify", and the tagline
  "Control Spotify's volume with hotkeys of your choice".
- **Set up your Spotify connection:** a Fluent **card** with a numbered list (`ol`) of **6 setup
  steps** the user follows once to create their own Spotify app:
  1. Open the **[Spotify Developer Dashboard](https://developer.spotify.com/dashboard)** (a
     hyperlink) and sign in.
  2. Click **Create app**.
  3. Give it any name and description you like (e.g. **Amplify**).
  4. Add this **redirect URI** — shown in a **copy-to-clipboard chip**:
     `http://127.0.0.1:49737/callback`. Clicking copy shows a ✓/"Copied" confirmation for ~1.5s,
     then reverts.
  5. Under **Which API/SDKs are you planning to use?** tick **Web API**, then **Save**.
  6. Copy the app's **Client ID** and paste it below.
- **Own-app / terms note:** brief copy near the setup card clarifying the user is creating their
  **own** private Spotify developer app — so Amplify runs entirely on their device with no shared
  servers — and that setting it up means agreeing to
  [Spotify's Developer Terms](https://developer.spotify.com/terms) (rendered as a caption with the
  Terms as a hyperlink). *Exact visual copy is owned by Claude Design; keep this doc in sync.*
- **Client ID field:** a labelled `TextBox` ("Client ID") with a placeholder example value. What
  the user types here is the value persisted to settings and used by [feature 03](./03-spotify-authentication.md).
- **Connect (gated):** the primary **Connect Spotify account** button is **disabled until the
  Client ID field is non-empty** (trimmed). While disabled, helper text reads
  **"Paste your Client ID above to continue."** Once a value is present, helper text becomes
  "Opens your browser to sign in securely with Spotify. An account with Spotify Premium is
  required."
- **Privacy note:** an info bar (use `InfoBar`) stating Amplify only requests permission to read
  and change playback state, and access can be revoked from the Spotify account at any time.
  Amplify runs locally and stores nothing off-device (see [`PRIVACY.md`](../../PRIVACY.md)).
- **Authorizing:** button becomes disabled with a spinner — "Waiting for Spotify…"; helper text:
  "Finish signing in on the page that opened in your browser." The Client ID field is disabled
  while authorizing/verifying.
- **Verifying:** after the browser returns success — spinner "Verifying connection…"; then the
  shell routes to **Main**.
- **Denied:** a warning `InfoBar` — "Access wasn't granted. You declined the permission request
  on Spotify. Connect again and choose Agree to continue." The Connect button returns so the
  user can retry.

## Acceptance criteria

- [ ] Shown automatically when not connected; replaced by Main once connected.
- [ ] The 6 setup steps are shown; the Developer Dashboard is a working link.
- [ ] Onboarding states the user is creating their **own** Spotify developer app and links
      [Spotify's Developer Terms](https://developer.spotify.com/terms).
- [ ] The redirect URI `http://127.0.0.1:49737/callback` is presented in a copy-to-clipboard chip
      that gives ✓/"Copied" feedback on copy.
- [ ] The Connect button is **disabled until the Client ID field is non-empty**; while disabled the
      helper text reads "Paste your Client ID above to continue."
- [ ] The entered Client ID is **trimmed and persisted to `settings.json`** ([feature 10](./10-settings-persistence.md))
      before the OAuth flow starts; [feature 03](./03-spotify-authentication.md) reads it from there.
- [ ] Connect button triggers the PKCE flow in [feature 03](./03-spotify-authentication.md).
- [ ] Authorizing/verifying states disable the button (and the Client ID field) and show progress
      with accurate copy.
- [ ] Denial shows the warning info bar and restores a working Connect button.
- [ ] Premium requirement is communicated up front (helper text). (Premium is enforced by Spotify
      on the user's own developer app, so a Free user cannot connect a working app — see
      [feature 03](./03-spotify-authentication.md); the app does not detect or branch on tier.)
- [ ] Privacy/permissions info bar is present.

## Implementation guidance

- `OnboardingViewModel` exposes `ConnectCommand`, an `OnboardingPhase`
  (`Welcome | Authorizing | Verifying`), a `Denied` flag, and a bound **`ClientId`** string; it
  delegates to `IAuthService` and reads/writes the Client ID via `ISettingsService`
  ([feature 10](./10-settings-persistence.md)).
- **Client ID gating:** `ConnectCommand.CanExecute` is false while `ClientId` is null/whitespace;
  raise can-execute changes as the field updates. On execute, **trim and persist** `ClientId` to
  settings **before** invoking `IAuthService.ConnectAsync()` (auth reads it from settings).
- **Setup card:** a `FontIcon`-free numbered list; the Dashboard entry is a `HyperlinkButton`/
  `Hyperlink` to `https://developer.spotify.com/dashboard`. The redirect URI uses a small copy
  control (a `Button` with a copy `FontIcon`) that writes `http://127.0.0.1:49737/callback` to the
  clipboard via `Windows.ApplicationModel.DataTransfer.Clipboard` and briefly swaps to a check glyph
  / "Copied" tooltip (~1.5s) for feedback. Keep the redirect URI and Dashboard URL as constants
  (no magic strings — [spec §5](../specification.md#5-design-principles--engineering-standards)).
- Use native controls: a primary `Button` (accent style), a `TextBox` for the Client ID,
  `ProgressRing` for the spinner, `InfoBar` for privacy + denied notices, `FontIcon` glyphs (lock,
  open-in-browser, copy/check) — no custom iconography.
- On `ConnectAsync` success → raise app state change so the shell routes to Main
  ([01](./01-application-shell.md)); on `access_denied` → set `Denied` and reset to Welcome.
- Reuse the prototype's copy strings as defaults.

## Data & persistence

- Persists the **Spotify Client ID** to `settings.json` (`AppSettings.SpotifyClientId`) via
  `ISettingsService` ([feature 10](./10-settings-persistence.md)) before connecting. The connection
  result (tokens) is owned by [feature 03](./03-spotify-authentication.md). The Client ID is
  per-user and **not** a secret; it is **not** stored in the Credential Locker.

## Edge cases & error handling

- Auth errors other than denial (network/token failure) → show a non-blocking error message and
  let the user retry; do not get stuck in Authorizing/Verifying (use a timeout).
- User returns to the app without completing the browser step → remain in Authorizing until
  timeout, then revert to Welcome.

## Dependencies

- Drives and reflects [feature 03](./03-spotify-authentication.md); persists the Client ID via
  [feature 10](./10-settings-persistence.md); routes via [feature 01](./01-application-shell.md).
  Once connected, the user lands on
  [feature 05](./05-connection-status.md)/[07](./07-volume-control.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- ViewModel phase transitions: Welcome → Authorizing → Verifying → (routes to Main); and
  Authorizing → Denied → Welcome on `access_denied`.
- Timeout path reverts to Welcome.
- `ConnectCommand` is disabled while `ClientId` is empty/whitespace and while Authorizing/Verifying;
  it becomes enabled once a non-empty Client ID is entered.
- The Client ID is **trimmed and persisted** to settings before `ConnectAsync` is invoked
  (verify ordering with a mocked `ISettingsService`/`IAuthService`).

## Out of scope

- The OAuth mechanics and browser pages (feature 03).
- The connected account card (feature 05).

## Standards reminder

Native controls (`InfoBar`, `Button`, `TextBox`, `HyperlinkButton`, `ProgressRing`) + Fluent icons;
clipboard via `Windows.ApplicationModel.DataTransfer`; follow Windows theme; no magic strings;
concise code.
