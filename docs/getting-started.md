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
- Visual Studio 2022 (latest 17.x) with the Windows App SDK / WinUI workload, or `dotnet` CLI with
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

- **Build/run:** build `Amplify.App` (packaged) and deploy/run from Visual Studio, or use the
  Windows App SDK CLI flow. The packaged app is needed for toasts and `StartupTask` to work.
- **Test:** `dotnet test tests/Amplify.Tests`. Prioritise `Amplify.Core` logic (see each feature's
  *Testing* section). UI is verified manually.
- When verifying a Spotify-dependent feature end-to-end, ensure Spotify is open and playing on a
  device so there's an **active device** to control.

## 8. Build order

Per the feature index in [`specification.md` §7](./specification.md#7-feature-index): scaffold +
shell (01) + settings (10) + theming (11) first, then auth (03), then onboarding/status (04/05),
then hotkeys/volume (06/07), then tray/notifications (08/09), then reset/icon (12/13).
**PR-tests CI (feature 02)** is built **second** — right after the shell scaffolds the solution and
test project — so it guards every later feature; the **release** workflow (feature 14) is built last.
