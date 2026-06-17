# Amplify — Getting Started & Prerequisites

> Parent spec: [`specification.md`](./specification.md). **Read this before building any feature.**
> It pins the solution layout, exact tool/package versions, configuration, and the Spotify
> app-registration steps needed to run and test anything. Shared types are defined in
> [`contracts.md`](./contracts.md).

---

## 1. Toolchain & target

- **.NET SDK 10.x** (current **LTS**, 3-year support), C# 14.
- **Windows App SDK 1.8** (WinUI 3), self-contained, **MSIX-packaged** app.
- **Target framework:** `net10.0-windows10.0.19041.0` with
  `<WindowsSdkPackageVersion>10.0.19041.31</WindowsSdkPackageVersion>`; **min platform:**
  `10.0.17763.0` (WinUI 3 minimum — Windows 10 1809).
- Visual Studio 2026 (latest 18.x) with the Windows App SDK / WinUI workload, or `dotnet` CLI with
  the Windows App SDK templates.
- Verify current WinUI 3 / Windows App SDK APIs with the `microsoft-docs:winui3` skill when in
  doubt — do not guess.

## 2. Solution layout

```
Amplify.sln
  Directory.Build.props   # shared MSBuild: Nullable=enable, TreatWarningsAsErrors, analysers, TFM
  .editorconfig           # solution-wide formatting + naming rules (dotnet format)
  src/
    Amplify.Core/      # contracts, models, enums, settings, pure logic (no UI) — see contracts.md
    Amplify.App/       # WinUI 3 app: Views, ViewModels, service implementations, App.xaml.cs
  tests/
    Amplify.Tests/     # xUnit test project referencing Amplify.Core (+ App where needed)
                       #   targets net10.0; if it references the WinUI App project, its
                       #   TargetFramework must match (net10.0-windows10.0.19041.0) + set
                       #   RuntimeIdentifiers (win-x64;win-arm64)
```

- **`Amplify.Core`** holds everything in [`contracts.md`](./contracts.md) plus platform-agnostic
  logic (hotkey parsing, volume/step math, settings serialisation + migration, backoff). Keep it
  UI-free so it's easy to unit-test.
- **`Amplify.App`** owns `App.xaml.cs`, the DI host, Views/ViewModels, and the Windows-specific
  service implementations (Credential Locker, hotkeys, tray, toasts, theme).
- See the **wiring & file-ownership convention** in
  [`specification.md` §4](./specification.md#wiring--file-ownership-avoid-cross-feature-collisions):
  each feature exposes an `AddXxx()` DI extension and (where needed) an `IStartupInitializer`;
  only the shell edits `App.xaml.cs`.

## 3. NuGet packages

| Package | Used for | Licence |
| --- | --- | --- |
| `Microsoft.WindowsAppSDK` (1.8.x) | WinUI 3 / windowing / toasts | MIT |
| `Microsoft.Extensions.Hosting` | generic host + DI + configuration | MIT |
| `Microsoft.Extensions.Http` | typed `HttpClient` via `IHttpClientFactory` (Spotify client) | MIT |
| `CommunityToolkit.Mvvm` | `ObservableObject`, `RelayCommand` | MIT |
| `CommunityToolkit.WinUI.Controls.SettingsControls` | `SettingsCard`/`SettingsExpander` | MIT |
| `H.NotifyIcon.WinUI` | system tray icon + menu | MIT |
| `xunit`, `xunit.runner.visualstudio` | unit tests | Apache-2.0 / MIT |
| `NSubstitute` | mocking in tests | BSD-3-Clause (pulls in Castle.Core, Apache-2.0) |

No third-party Spotify SDK and no third-party logging sink (see §5). Avoid adding dependencies
beyond this list without a reason.

### Dependency licensing policy (the app is MIT / open-source)

Amplify ships under the **MIT License**, so any dependency added during development **must** be
under an OSI-approved **permissive** licence compatible with MIT:

- **Allowed:** MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause (all current deps above qualify).
- **Not allowed:** copyleft (GPL, LGPL, AGPL, MPL) or source-available / non-OSI licences — these
  are incompatible with shipping under MIT.
- **Before adding a package,** check its licence (e.g. the NuGet package page / its repo
  `LICENSE`) and its transitive dependencies. If it isn't clearly permissive, don't add it —
  find an alternative or ask.
- **Attribution:** honour notice requirements (Apache-2.0 `NOTICE`, BSD copyright lines) by
  maintaining a bundled **third-party notices** file in the repo/package.
- Add a `LICENSE` (MIT) file at the repo root.

## 4. Configuration & the per-user Client ID model

**Amplify does not ship a Spotify Client ID.** A single shipped app would hit Spotify's
**development-mode 25-user limit**, so instead **each user registers their own Spotify app** and
provides their **own Client ID** (model "a"). The Client ID is **not a secret** under PKCE.

- The user enters their Client ID **during onboarding**; it is persisted in local settings
  (`settings.json`) — not committed to source, not in the Credential Locker (it isn't secret, but
  it is per-user so it belongs with user data).
- The redirect port and scopes are **app constants** shipped in `appsettings.json` (they're the
  same for every user):

```json
{
  "Spotify": {
    "RedirectPort": 49737,
    "Scopes": [ "user-read-playback-state", "user-modify-playback-state" ]
  }
}
```

- The redirect URI used at runtime is `http://127.0.0.1:49737/callback` (see
  [feature 03](./features/03-spotify-authentication.md)).
- **Refresh tokens are never stored here** — they go in the Windows Credential Locker.

> **Onboarding captures the Client ID.** [Feature 04](./features/04-onboarding.md) walks the user
> through creating their Spotify app (the steps in §6 below), presents the redirect URI as a
> copy-to-clipboard chip, and gates the Connect button until a Client ID is entered, persisting it
> to `settings.json` (`AppSettings.SpotifyClientId`) before the OAuth flow runs. It can be changed
> later only via Reset ([feature 12](./features/12-reset-and-account.md)).

## 5. Local data, logs, secrets

- **Settings:** `ApplicationData.Current.LocalFolder\settings.json` (+ `settings.v{old}.bak`
  backups) — [feature 10](./features/10-settings-persistence.md).
- **Logs:** `ApplicationData.Current.LocalFolder\logs\` via `Microsoft.Extensions.Logging` with a
  minimal custom file `ILoggerProvider` (no third-party sink). Never log tokens or PII.
- **Refresh token:** Windows Credential Locker (`PasswordVault`), resource `"Amplify"` —
  [feature 03](./features/03-spotify-authentication.md).
- **App version** (for the settings footer/about): `Package.Current.Id.Version`.

## 6. Spotify app registration (each user does this once)

Because of the per-user model (§4), **every user — including each developer — registers their own
Spotify app**:

1. Sign in at the **Spotify Developer Dashboard** (`https://developer.spotify.com/dashboard`) and
   **Create app**.
2. Note the **Client ID** — you'll enter it into Amplify during onboarding. PKCE needs **no client
   secret**.
3. Add the **Redirect URI** — register the single loopback URI:
   - `http://127.0.0.1:49737/callback`
   (Loopback IP only — not `localhost`, no wildcards; see the redirect-URI rule in
   [`specification.md` §6](./specification.md#6-spotify-web-api-client-standards).)
4. Under API access, the app uses the **Web API**; request only the two scopes in §4.
5. A **Spotify Premium** account is required to actually change volume; have one available for
   end-to-end testing.

> Because each user supplies their own Client ID, Amplify is never bound by another app's
> development-mode user quota.

## 7. Build, run, test

- **Pre-flight (do this first):** before writing code, confirm the pinned SDK/package versions in
  §1/§3 actually resolve — run `dotnet restore` on a stub solution (or check the versions on
  NuGet). These versions are forward-looking; if any package id or version doesn't exist or has
  moved, **verify the correct one via the `microsoft-docs:winui3`/`microsoft-docs` skills and
  update this file** before building on top of it. A wrong version fails the very first restore.
- **Build/run:** build `Amplify.App` (packaged) and deploy/run from Visual Studio, or use the
  Windows App SDK CLI flow. The packaged app is needed for toasts and `StartupTask` to work.
- **Test:** `dotnet test tests/Amplify.Tests`. Prioritise `Amplify.Core` logic (see each feature's
  *Testing* section). UI is verified manually.
- When verifying a Spotify-dependent feature end-to-end, ensure Spotify is open and playing on a
  device so there's an **active device** to control.

## 8. Build order

Build in three phases: a thin **end-to-end slice** first to prove the risky platform plumbing,
then fill out each feature in dependency order, then **verify the whole app runs** before release.

### Phase 0 — walking skeleton (vertical slice)

Before building any feature in full, stand up the **smallest path that actually changes Spotify's
volume from the app**, using only a sliver of a few features:

- a packaged (MSIX) app that launches with the DI host — minimal
  [01](./features/01-application-shell.md) (no Mica/title-bar polish yet);
- happy-path [03](./features/03-spotify-authentication.md): PKCE connect on the real
  `127.0.0.1:49737` loopback, store the token, `GetAccessTokenAsync()` — no refresh/rotation or
  Free-state nuance yet;
- happy-path [07](./features/07-volume-control.md): `GET /v1/me/player` + one `PUT .../volume`
  wired to a button, then to a single hard-coded `RegisterHotKey`.

This deliberately exercises the integration points most likely to fight you — packaged identity,
the OAuth loopback redirect, the typed `HttpClient` + bearer, and Win32 hotkey/HWND plumbing —
while the surface is tiny. **Done when you can see Spotify's volume move from a hotkey.** Defer
everything else (settings persistence, theming, tray, onboarding UI, notifications, reset) to
Phase 1.

### Phase 1 — complete the features (dependency order)

Flesh out each feature to its full doc: shell (01) + settings (10) + theming (11), then finish auth
(03: refresh/rotation, Premium/Free), onboarding/status (04/05), hotkeys/volume (06/07: recording
UI, gating, optimistic UI), tray/notifications (08/09), reset/icon (12/13).

**PR-tests CI (feature 02)** is built **right after the Phase 0 skeleton** so it guards everything
after.

### Phase 2 — integration & smoke test

Once the features are in, do an explicit **end-to-end assembly + manual smoke test** — this is the
first time the whole app is exercised as a user would, and it's where the integration bugs the
unit tests can't catch (window/tray/hotkey/OAuth) surface. Follow the checklist in
[`integration-smoke-test.md`](./integration-smoke-test.md). Only after it passes do you cut the
**release** ([feature 14](./features/14-release.md)) — the last step.
