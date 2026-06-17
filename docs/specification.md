# Amplify — Application Specification

> Control Spotify's volume with global keyboard hotkeys, from a small native Windows app.

This document is the top-level specification for **Amplify**. It gives a brief, whole-app
overview and links out to detailed, self-contained **feature documents** under
[`features/`](./features). Each feature doc can be handed to Claude Code on its own to build
that feature.

**Before building any feature, read these two companion documents** (they are the shared
contract every feature depends on):
- [`getting-started.md`](./getting-started.md) — solution/project layout, exact SDK & package
  versions, and the Spotify app-registration / Client-ID setup needed to run and test anything.
- [`contracts.md`](./contracts.md) — canonical service interfaces, enums, models, events, and the
  full `AppSettings` shape. Implement against these exactly.

---

## 1. Overview

Amplify is a lightweight **WinUI 3** desktop application for Windows 11 that lets a user raise
and lower the volume of their **Spotify** playback using **global keyboard hotkeys** — shortcuts
that work anywhere in Windows, even while Amplify is minimised to the system tray.

The user signs in once with their Spotify account (OAuth). Amplify then talks to the **Spotify
Web API** to read and change the playback volume of the user's active Spotify device. The app
lives quietly in the background/tray; its small window is only needed to bind shortcuts and
change settings.

- **Audience:** Spotify **Premium** users who want quick, system-wide volume control without
  alt-tabbing to Spotify.
- **Requirement:** Volume control via the Web API is a **Premium-only** capability and needs an
  active playback device. A **Free account can still sign in** — it reaches the main screen and is
  shown a clear "upgrade to Premium" notice, with volume control disabled rather than hard-blocked.
- **Affiliation:** Amplify is **not affiliated with Spotify**. Spotify is a trademark of
  Spotify AB.

---

## 2. Goals & non-goals

**Goals**
- Bind two global hotkeys (volume up / volume down) and have them change Spotify's volume.
- Feel like a built-in Windows 11 utility: native controls, native icons, native theming.
- Stay out of the way — run in the tray, optionally launch at startup.
- Make connection state obvious and recoverable (connect / reconnect / errors).

**Non-goals**
- Not a full Spotify client (no browsing, search, playback transport beyond volume).
- No media-key remapping of the OS volume; Amplify changes **Spotify's** volume specifically.
- No accounts, telemetry, or cloud sync of our own — all state is local to the machine.
- **No device picker** — Amplify always targets Spotify's active device; device switching is done
  in Spotify itself.
- Not aiming for 100% automated test coverage (see §5).

---

## 3. Primary user journey

1. **First run / onboarding** — welcome screen explains the app and prompts *Connect Spotify*.
2. **Authorise** — the system browser opens for Spotify sign-in (PKCE). On approval the browser
   lands on a local success page; the user returns to Amplify.
3. **Main window** — shows the connected account, the two hotkey bindings (defaults
   `Ctrl+Alt+↑` / `Ctrl+Alt+↓`), and a live volume meter.
4. **Bind shortcuts** — the user can re-record either hotkey by pressing a new combination.
5. **Background use** — the user minimises to the tray; hotkeys keep working globally. Optional
   toast on each volume change.
6. **Settings** — startup, tray, notifications, theme, step size, account, and reset.

---

## 4. Recommended tech stack & architecture

| Concern | Choice |
| --- | --- |
| Language / runtime | C# on **.NET 10** (current LTS; TFM `net10.0-windows10.0.19041.0`, min Windows `10.0.17763.0`) |
| UI framework | **WinUI 3** (Windows App SDK **1.8**), **MSIX-packaged** |
| Window backdrop / chrome | `MicaBackdrop`; custom title bar via `ExtendsContentIntoTitleBar` + `AppWindow` |
| UI controls & icons | **Built-in WinUI controls only**; **Segoe Fluent Icons** via `FontIcon`. Amplify logo is the sole custom asset |
| Pattern | **MVVM** with **CommunityToolkit.Mvvm** (`ObservableObject`, `RelayCommand`) |
| Dependency injection | `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting` |
| Spotify auth | **Authorization Code with PKCE** (no client secret) |
| Spotify client | **`HttpClient`** (typed client via `IHttpClientFactory`) against the Web API — no third-party SDK; see §6 |
| OAuth redirect listener | `System.Net.HttpListener` on the registered loopback port (`http://127.0.0.1:49737/callback`) |
| Token storage | **Windows Credential Locker** (`PasswordVault`) for refresh tokens |
| Settings storage | **JSON file** at `ApplicationData.Current.LocalFolder\settings.json` behind `ISettingsService` |
| Global hotkeys | P/Invoke `RegisterHotKey`/`UnregisterHotKey` (fallback: `WH_KEYBOARD_LL` hook) |
| Tray icon | **H.NotifyIcon.WinUI** |
| Toasts | `AppNotificationManager` (Windows App SDK) |
| Launch at startup | Packaged `StartupTask` |
| Logging | `Microsoft.Extensions.Logging` with a **minimal custom file `ILoggerProvider`** writing to `LocalFolder\logs\` (no third-party sink) |
| Configuration | `appsettings.json` (content file, non-secret) bound to an options class — holds the redirect port + scopes. The per-user Spotify **Client ID** lives in `settings.json` (entered during onboarding), not here |
| Testing | **xUnit** + **NSubstitute** for mocking (interface-based fakes are also fine) |
| App version | from the packaged identity (`Package.Current.Id.Version`) |

### Architecture (layers)

```
 Views (XAML)  ──binds──▶  ViewModels  ──call──▶  Services
 ───────────────────────────────────────────────────────────
 Services:
   IAuthService          OAuth/PKCE, token lifecycle, sign-out
   ISpotifyClient        Web API calls (player state, volume, profile, devices)
   IHotkeyService        register/unregister global hotkeys, raise events
   IVolumeController      step math + orchestrates ISpotifyClient
   ISettingsService      typed get/set + persistence + change notifications
   IThemeService         follow Windows theme/accent; apply overrides
   ITrayService          tray icon, menu, show/hide, single-instance
   INotificationService  toasts
```

Everything Windows- or network-specific sits behind an interface so it can be unit-tested with
fakes/mocks. ViewModels contain no platform calls directly. **The canonical signatures for all
services, enums, models, events, and the full `AppSettings` shape live in
[`contracts.md`](./contracts.md)** — implement against those exactly so independently-built
features compose.

### Wiring & file ownership (avoid cross-feature collisions)

Because each feature may be built in a separate session, follow these conventions so the pieces
fit together without conflicting edits:

- **Each feature owns its own files** (its View(s), ViewModel(s), and service implementation) and
  exposes a single DI registration extension method, e.g. `services.AddAuth()`, `AddSettings()`,
  `AddHotkeys()`, `AddTray()`, etc.
- **The shell ([feature 01](./features/01-application-shell.md)) owns `App.xaml.cs` and the host
  setup.** It calls each feature's `AddXxx()` during host build, and invokes well-named init
  hooks at startup (e.g. `IStartupInitializer.OnLaunchedAsync()`) that features implement —
  features do **not** edit `App.xaml.cs` directly. Examples of init-time work: restore tokens
  (03), register hotkeys (06), apply theme (11), set up the tray + single instance + minimise/
  close handling (08).
- **Shared contracts/models live in one place** (a `Amplify.Core` project / `Contracts` +
  `Models` folders) as defined in [`contracts.md`](./contracts.md); features reference them rather
  than redefining.
- See [`getting-started.md`](./getting-started.md) for the solution/project layout and exact
  package versions.

---

## 5. Design principles & engineering standards

These apply to **every** feature. Each feature doc restates the ones relevant to it.

- **Native Windows 11 feel — the mockups are reference only, not to be copied pixel-for-pixel.**
  The app should look like it shipped with Windows. Use built-in WinUI controls (`ToggleSwitch`,
  `Slider`, `ComboBox`, `InfoBar`, `ContentDialog`, `Expander`, `NavigationView`/`Frame`, list
  views, `SettingsCard` from the Windows Community Toolkit) rather than re-creating the bespoke
  HTML/CSS controls from the prototype.
- **Iconography:** use the **default Windows 11 icons** (Segoe Fluent Icons / `FontIcon` glyphs)
  everywhere. The **only** custom graphic is the **Amplify logo**.
- **Theme default = Windows:** the app **follows the user's Windows theme (light/dark) and the
  Windows accent colour** by default. A manual override (System/Light/Dark) lives in settings.
  Do not hard-code the prototype's colour palette as the source of truth.
- **Best practices + verify with docs:** follow current WinUI 3 / Windows App SDK / .NET best
  practices and **use the Microsoft docs skills** (`microsoft-docs:winui3`,
  `microsoft-docs:microsoft-code-reference`, `microsoft-docs:microsoft-docs`) to confirm APIs,
  signatures and patterns instead of guessing.
- **Concise code:** prefer concise, idiomatic C# over verbose boilerplate.
- **Don't reinvent the wheel:** use the standard **.NET BCL** and **Windows App SDK / WinRT**
  classes and functions wherever a suitable one exists, rather than hand-rolling your own. For
  example: `System.Text.Json` for serialisation, `HttpClient`/`IHttpClientFactory` for HTTP,
  `System.Security.Cryptography` for PKCE hashing, `Uri`/`QueryHelpers` for URL building,
  `System.Net.HttpListener` for the loopback callback, `Windows.Security.Credentials.PasswordVault`
  for token storage, and built-in WinUI controls/converters for UI. Only write custom code when no
  standard equivalent fits (e.g. the minimal file `ILoggerProvider`), and say why.
- **Compiler strictness:** enable nullable reference types and fail the build on warnings, set once
  in a root `Directory.Build.props` so every project inherits it — `<Nullable>enable</Nullable>`,
  `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<EnableNETAnalyzers>true</EnableNETAnalyzers>`,
  `<AnalysisLevel>latest-recommended</AnalysisLevel>`.
- **Consistent style:** a solution-wide `.editorconfig` governs formatting and naming; code must be
  `dotnet format`-clean so output from different sessions stays consistent.
- **Async discipline:** async all the way; **never block on async** (`.Result`, `.Wait()`,
  `.GetAwaiter().GetResult()` — they deadlock in UI apps). Flow a `CancellationToken` through
  service calls, and marshal UI updates back to the window's `DispatcherQueue`.
- **Deterministic resource cleanup:** honour `IDisposable`/`using`; explicitly dispose the
  `HttpListener`, hotkey registrations / keyboard hook, the tray icon, and HttpClient handlers
  (via `IHttpClientFactory`). Don't leak native handles or event subscriptions.
- **Error handling:** catch **specific** exceptions; no empty or silent `catch` blocks. Log the
  failure and surface meaningful feedback (see §6 and the per-feature error sections).
- **DI lifetimes:** register stateful services as **singletons** (settings, auth, hotkeys, tray,
  theme); ViewModels depend only on the interfaces in [`contracts.md`](./contracts.md), never on
  concrete implementations or platform types.
- **Document the public contract:** XML doc comments on the interfaces, enums, and models in
  `Amplify.Core` — that is the shared API surface every feature builds against.
- **No magic values:** centralise constants (redirect port, volume step bounds 1–25, settings
  `schemaVersion`, Spotify scopes) in one place rather than scattering literals.
- **Localisation/accessibility readiness:** put user-facing strings in `.resw` resource files
  rather than hard-coding them in XAML/code, so the app can be localised and reads consistently to
  assistive tech.
- **Testing:** write **unit tests around key functionality** using **xUnit** (+ **NSubstitute**
  for mocking). Prioritise services and logic (token handling, hotkey parsing/formatting,
  volume/step math, settings serialisation + migration, backoff) over UI. 100% coverage is
  **not** the goal — more is better.
- **Definition of done & test discipline:** A feature is complete only when the solution builds
  and **all unit tests pass** (`dotnet test` green). Do **not** disable, skip, comment out, or
  weaken tests (e.g. `[Fact(Skip)]`, `Assert.True(true)`, deleting assertions) to make a build
  pass. When a test fails, fix the code — change the test only if the test itself is genuinely
  wrong, and say so. Unit tests must have **no external dependencies** (no live Spotify, network,
  UI, or registry) so they always run; checks that require a real Spotify session, Premium, an
  active device, or the UI are **integration/manual**, must be clearly marked as such (e.g. a
  `RequiresSpotify` trait, excluded from the default `dotnet test` run), and are verified per each
  feature's *Testing* section. A test may be skipped only with an explicit, documented reason —
  never silently.
- **Security:** never store secrets in plaintext or source; refresh tokens go in the Credential
  Locker. PKCE means there is **no client secret** to embed.

---

## 6. Spotify Web API client standards

The Spotify integration (features [03](./features/03-spotify-authentication.md) and
[07](./features/07-volume-control.md)) **must** follow these rules:

- **OpenAPI spec is authoritative.** Take all endpoint paths, parameters and response schemas
  from the official OpenAPI document:
  `https://developer.spotify.com/reference/web-api/open-api-schema.yaml`. **Do not guess**
  endpoints or field names.
- **Authorization = Authorization Code with PKCE only.**
  See `https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow`.
  Do **not** use plain Authorization Code or Client Credentials, and **never** the deprecated
  Implicit Grant flow.
- **Redirect URIs.** HTTPS only, **except `http://127.0.0.1`** (with an explicit port) for local
  development. Never `http://localhost` and never wildcards. See
  `https://developer.spotify.com/documentation/web-api/concepts/redirect_uri`.
- **Scopes.** Request only the **minimum** needed. For Amplify that is
  **`user-read-playback-state`** and **`user-modify-playback-state`**. No preemptive broad
  scopes. See `https://developer.spotify.com/documentation/web-api/concepts/scopes`.
- **Token management.** Store tokens securely; **never expose a Client Secret** in client-side
  code (PKCE needs none). Implement refresh so expiry doesn't break the app:
  `https://developer.spotify.com/documentation/web-api/tutorials/refreshing-tokens`.
- **Rate limits.** On HTTP **429**, use **exponential backoff** and honour the **`Retry-After`**
  header. Never retry immediately or in tight loops.
- **No deprecated endpoints.**
- **Error handling.** Handle every HTTP status the OpenAPI schema documents; read the returned
  error message and surface meaningful feedback to the user.
- **Developer Terms of Service** (`https://developer.spotify.com/terms`): don't cache Spotify
  content beyond immediate use, always attribute content to Spotify, and never use the API to
  train machine-learning models on Spotify data.

**Endpoints used by Amplify** (confirm exact shapes against the OpenAPI spec):
- `GET /v1/me` — read profile incl. `product` to confirm Premium.
- `GET /v1/me/player` — current playback state / active device / current volume.
- `GET /v1/me/player/devices` — available devices.
- `PUT /v1/me/player/volume?volume_percent={0-100}` — set volume (optional `device_id`).

---

## 7. Feature index

Build order roughly follows dependencies: shell + settings + theming first, then auth, then the
features that depend on a connected account.

| # | Feature | Summary | Key deps |
| --- | --- | --- | --- |
| 01 | [Application shell & window](./features/01-application-shell.md) | Main window, Mica, custom title bar, navigation/routing, caption buttons | — |
| 02 | [CI — PR tests](./features/02-ci-pr-tests.md) | GitHub Actions: build + run unit tests on every PR | 01 |
| 03 | [Spotify authentication](./features/03-spotify-authentication.md) | PKCE OAuth, local callback, token storage & refresh, Premium check, sign-out | 01 |
| 04 | [Onboarding / first run](./features/04-onboarding.md) | Welcome screen, connect flow, denied handling | 01, 03 |
| 05 | [Connection status & account](./features/05-connection-status.md) | Connected/connecting/error states, account card, reconnect | 03 |
| 06 | [Global hotkeys](./features/06-global-hotkeys.md) | Bind/record combos, global registration, conflicts, persistence | 01, 10 |
| 07 | [Volume control](./features/07-volume-control.md) | Web API volume, live meter, slider, ± buttons, step size | 03, 06 |
| 08 | [System tray & background](./features/08-system-tray-background.md) | Tray icon, minimise-to-tray, launch at startup, single instance | 01, 10 |
| 09 | [Notifications](./features/09-notifications.md) | Toast on volume change | 07, 10 |
| 10 | [Settings & persistence](./features/10-settings-persistence.md) | Settings page + `ISettingsService` persistence layer | 01 |
| 11 | [Theming & appearance](./features/11-theming-appearance.md) | Follow Windows theme/accent; manual override; Mica | 01, 10 |
| 12 | [Reset & account management](./features/12-reset-and-account.md) | Reset everything, disconnect, data clearing | 03, 10 |
| 13 | [App icon & branding](./features/13-app-icon-branding.md) | Logo direction, app/tray/tile assets | — |
| 14 | [Release — build & publish](./features/14-release.md) | Tag-driven MSIX build, signing, GitHub Release | 01, 02 |

---

## 8. Cross-cutting concerns

- **Error handling:** all service calls fail gracefully and surface a user-readable message
  (typically via `InfoBar` or toast). Network/Spotify errors map to the status states in
  feature 05.
- **Logging:** `Microsoft.Extensions.Logging` with a **minimal custom file `ILoggerProvider`**
  writing rolling files to `ApplicationData.Current.LocalFolder\logs\` (plus the Debug provider in
  dev). No third-party logging dependency. Never log tokens or PII.
- **Security:** refresh tokens in Credential Locker; no client secret; minimum scopes.
- **App version:** the footer/about version string comes from the packaged identity
  (`Package.Current.Id.Version`) — not hard-coded.
- **License & dependencies:** Amplify is **open-source under the MIT License** (a `LICENSE` file
  lives at the repo root). Only add third-party dependencies under OSI-approved **permissive**
  licences compatible with MIT — **MIT, Apache-2.0, BSD-2/3-Clause**. **Avoid copyleft**
  (GPL/LGPL/AGPL/MPL) and source-available / non-OSI licences. Honour each dependency's
  attribution/notice terms (e.g. Apache-2.0 `NOTICE`, BSD copyright lines) by shipping a
  third-party-notices file. All currently-pinned dependencies are permissive — see
  [`getting-started.md` §3](./getting-started.md#3-nuget-packages).
- **Telemetry:** none.
- **Accessibility:** because the app uses native controls, keyboard navigation, narrator labels,
  and high-contrast themes should work out of the box — set `AutomationProperties` where a
  control's purpose isn't obvious from its content.
- **Single instance:** only one Amplify process runs; launching again surfaces the existing
  window (feature 08).

---

## 9. Visual design reference (prototype)

The HTML prototype in [`../design/project`](../design/project) shows the intended **layout,
copy, information hierarchy, and states**. Treat it as a **reference for *what* each screen
contains**, not as a stylesheet to port. Concretely:

- The window is compact (~480px wide) with a custom title bar and a single scrollable content
  area that swaps between onboarding / main / settings.
- Spacing, type ramp, radii and accent maths in `Amplify.html` and `app.jsx` illustrate the
  *intended density and tone* — match the spirit with native WinUI styling and system brushes.
- Copy strings in the prototype (e.g. status messages, onboarding text, the "Not affiliated with
  Spotify" footer) are good defaults; reuse them unless a feature doc says otherwise.

---

## 10. Glossary & external dependencies

- **PKCE** — Proof Key for Code Exchange; the OAuth flow used (no client secret).
- **Active device** — the Spotify Connect device currently playing/selected; volume changes
  target it.
- **Step size** — the percentage each hotkey press changes the volume (1–25%, default in the
  prototype is 5%).
- **Scopes** — `user-read-playback-state`, `user-modify-playback-state`.
- **External services** — Spotify Accounts (OAuth) and Spotify Web API.
