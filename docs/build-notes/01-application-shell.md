# Build notes — Feature 01: Application Shell & Window

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-18 — Phase 0 (walking-skeleton sliver) · branch `feat/01-application-shell`

Scaffolded the solution and stood up a launchable, MSIX-packaged WinUI 3 window hosted by the
`Microsoft.Extensions.Hosting` DI container. Bare shell only — no Mica, custom title bar, or routing
(those are Phase 1). The diff covers the obvious mechanics; recorded below is only the non-obvious.

- **Deviations from spec/contracts:**
  - **Windows App SDK 2.2, not 1.8.** The pinned `1.8.x` / TFM `net10.0-windows10.0.19041.0` /
    `WindowsSdkPackageVersion 10.0.19041.31` in getting-started were forward-looking and had moved.
    The WinUI template now resolves **`Microsoft.WindowsAppSDK` 2.2.0** (current stable, released
    2026-06-09; the SDK adopted SemVer at 2.0) on **TFM `net10.0-windows10.0.26100.0`** with **no
    `WindowsSdkPackageVersion` override**. Min platform `10.0.17763.0` is preserved. Per
    getting-started §7 I verified the current versions via the `microsoft-docs` skill and **updated
    getting-started §1/§3** accordingly.
  - **Solution file is `Amplify.slnx`**, not `Amplify.sln` — the .NET 10 SDK's `dotnet new sln`
    defaults to the new XML solution format (supported by the CLI and VS 2026). Updated the §2 layout
    reference. `dotnet build/test/format` all accept `Amplify.slnx`.
  - **Solution platforms pinned to `x86;x64;ARM64`** (the packaged App supports no Any CPU). The
    `.slnx` format **defaults each project to "Any CPU" unless mapped**, which none of these projects
    expose — so Visual Studio repeatedly warned "specifies a project configuration that does not
    exist" for *all three* projects (a `dotnet build` succeeds regardless, so the CLI does not catch
    this — it only surfaces when VS loads the solution). The working fix, mirrored from a
    VS-generated WinUI `.slnx`, has two parts:
    1. In `Amplify.slnx`: a `<Configurations>` block listing the three `<Platform>`s **plus
       per-project `<Platform Solution="*|x64" Project="x64" />` mapping rules on every project**
       (and `<Deploy />` on the App). The per-project rules are the essential part — a
       `<Configurations>` block alone is **not** enough; VS still defaults unmapped projects to
       Any CPU.
    2. `Platforms=x86;x64;ARM64` on every project via `Directory.Build.props`, so each library
       actually exposes the `x64`/`x86`/`ARM64` the slnx maps `Project="…"` to.
    `Platforms` is advisory IDE metadata, so building a project directly with the default
    Platform=AnyCPU (`dotnet test <csproj>`) still writes to `bin/Debug/net10.0/`; verified
    `dotnet build`/`test`/`format` remain green. Building the **solution** now defaults to the first
    listed platform (ARM64); pass `-p:Platform=x64` for locally-runnable output.
    Note: the `.vs/` cache (gitignored) holds a `.suo` pinning the last-active platform; delete `.vs/`
    once after this change so VS reloads cleanly.
  - **`IStartupInitializer` lives in `Amplify.Core/Startup/`** (with a sibling pure
    `StartupInitializerRunner`). Contract signature is copied verbatim from contracts.md — no contract
    change.

- **Contract changes:** none.

- **Assumptions (docs were silent):**
  - `SpotifyOptions` (typed `appsettings.json` → `Spotify` section binding) and `appsettings.json`
    itself are **shell-owned** and added here (feature 01 owns config binding per spec §4), so
    feature 03's Phase 0 sliver can consume `RedirectPort`/`Scopes` without re-deriving them. Lives in
    `Amplify.Core/Configuration/`.
  - Host is used purely as a DI/config/logging container in Phase 0 — `OnLaunched` resolves
    `MainWindow` from DI and runs the (currently empty) ordered `IStartupInitializer` set via
    `StartupInitializerRunner`; **no `StartAsync`/hosted services** (the launch contract uses
    `IStartupInitializer`, not `IHostedService`). Host disposed on the main window's `Closed`.
  - Test project targets **`net10.0` and references `Amplify.Core` only** (not the WinUI App), so it
    needs no Windows TFM/RID; WinUI is verified manually per spec.
  - Project namespace standardized to `Amplify.App` (template default was the sanitized `Amplify_App`).
  - Manifest `DisplayName` set to "Amplify"; the template's `systemAIModels` capability was removed
    (unused; spec wants minimum capabilities). `runFullTrust` retained (required for packaged desktop).

- **Deferred / known gaps (→ Phase 1):** Mica backdrop, custom title bar + caption-button insets,
  `ShellViewModel`/`Frame` routing + transitions and its initial-route unit tests (no router exists
  in Phase 0, so the Phase-0 unit test instead covers the `StartupInitializerRunner` ordering seam),
  the minimal custom file `ILoggerProvider` (LocalFolder\logs\ — Phase 0 uses the Debug provider
  only), window state persistence, theming, tray, single-instance redirect, real Amplify logo
  (feature 13). Feature 02 CI is a separate session.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx` → **0 warnings, 0 errors** (with `TreatWarningsAsErrors=true`).
  - `dotnet test` → **3 passed** (`StartupInitializerRunner` ascending-order + cancellation;
    `SpotifyOptions` binding).
  - `dotnet format --verify-no-changes` → clean (exit 0).
  - **Outstanding (requires an interactive desktop):** deploy/run the packaged app from Visual Studio
    (MsixPackage profile) or `dotnet run` and confirm a single window titled "Amplify" launches with
    working native minimise / maximise-restore / close. Not runnable from this headless session.

- **Verified facts:**
  - `Microsoft.WindowsAppSDK` **2.2.0** is current stable (2026-06-09); the WinUI templates also pull
    `Microsoft.Windows.SDK.BuildTools` 10.0.28000.1839 and `Microsoft.Windows.SDK.BuildTools.WinApp`
    0.3.2 — the latter adds first-class **`dotnet run`** support for packaged WinUI apps (so the
    manual check above no longer strictly requires Visual Studio).
  - `Microsoft.Extensions.*` (Hosting, Configuration, Configuration.Binder) resolve to **10.0.9** on
    the .NET 10 SDK (10.0.201 installed).
  - The .NET Generic Host pattern (`Host.CreateApplicationBuilder` → register window singleton →
    resolve from DI → `Activate()`) composes cleanly inside WinUI 3 `App.OnLaunched`.

## 2026-06-19 — PR #1 review fixes · branch `feat/01-application-shell`

Addressed three findings from code review on the Phase 0 PR. No behaviour change to the happy path;
no contract changes.

- **Removed `PublishTrimmed` / `PublishReadyToRun` from `Amplify.App.csproj`** (the WinUI template
  enables both for Release). Two reasons: (1) trimmed Windows App SDK apps have documented
  release-mode crashes, and under trim/AOT the developer must manually root reflection-based XAML
  `{Binding}` targets — risk that buys little for an app this size; (2) reflection config binding is
  trim-incompatible in general (the `SpotifyOptions` bind is *probably* safe because `PublishTrimmed`
  auto-enables the configuration-binding source generator in .NET 8+, but that was untested — the
  earlier "0 warnings" build was Debug, where no trim analysis runs). Left a comment in the csproj so
  the template default isn't blindly re-added; revisit only with a concrete size/startup target and a
  thoroughly tested trimmed Release build. Verified via the `microsoft-docs` skill.

- **Re-added app-secret ignores to `.gitignore`.** Replacing the bespoke ignore with the
  GitHub-standard VisualStudio template had silently dropped `appsettings.*.local.json`,
  `secrets.json`, `*.cer`, `*.snk`, and `.claude/settings.local.json` (the standard file only
  *comments out* `*.snk`). Re-added an "App secrets & local config — never commit" block — relevant
  because onboarding captures a per-user Spotify Client ID. Also fixed the file's missing trailing
  newline.

- **Guarded the `async void OnLaunched` launch sequence.** An exception escaping the necessarily
  `async void` override would crash the process with no diagnostics. Wrapped the sequence in
  try/catch: logs `Critical`, disposes the host, then rethrows (fail-fast *with* diagnostics). To
  satisfy the repo's analyzers-as-errors (`CA1848` LoggerMessage delegates, `CA1873` no expensive
  args in log calls) the log uses a source-generated `[LoggerMessage]` partial (`LogStartupFailed`)
  with the `ILogger<App>` resolved into a local first. A richer in-app error screen is deferred to the
  Phase 1 shell UI; for now log + fail-fast is the honest behaviour.

- **Manual/integration checks (re-run after the fixes):**
  - `dotnet build Amplify.slnx -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test` → **3 passed** (unchanged).
  - `dotnet format --verify-no-changes` → clean (exit 0).

## 2026-06-21 — Phase 1 (shell completion) · branch `feat/01-application-shell`

Hardened the bare Phase 0 window into the real shell: Mica backdrop, custom title bar, and an
in-window `Frame` router across Onboarding / Main / Settings chosen from auth state, plus the
deferred custom file logger. The Phase 0 connect + volume/hotkey slice was **preserved** (user
decision) by moving its controls onto the routed pages so the app stays demonstrable end to end.

- **Title bar = the `TitleBar` control, not hand-rolled `AppWindowTitleBar` drag regions.** Verified
  via the `microsoft-docs` skill that Windows App SDK 2.2 ships `Microsoft.UI.Xaml.Controls.TitleBar`,
  which allocates caption-button space and exposes `Title`/`IconSource` while the system still draws
  and handles min/max/close. Used `ExtendsContentIntoTitleBar = true` **in code** (setting it in XAML
  errors) + `SetTitleBar(AppTitleBar)`. Avoids the manual interactive-region maths the older pattern
  needs. Logo icon is a placeholder pointing at an existing `Square44x44Logo` asset (the real Amplify
  logo is feature 13).

- **Mica:** `SystemBackdrop = new MicaBackdrop()` set unconditionally — it falls back to a solid
  themed colour automatically where Mica isn't supported, so no explicit `MicaController.IsSupported()`
  guard is needed. The root `Grid`/`Frame`/pages keep transparent backgrounds so Mica shows through.
  Mica also follows the OS light/dark theme by default, which is what satisfies feature 01's
  "respects the active theme" criterion without an `IThemeService` (that's feature 11).

- **Routing logic lives in `Amplify.Core/Navigation/ShellRouter.cs` (UI-free), not in the
  view-model.** Rationale: the test project targets `net10.0` and references `Amplify.Core` only;
  referencing the packaged WinUI App (WinExe/MSIX) from tests is the painful path getting-started §2
  warns about. Putting the route state machine in Core makes every routing rule unit-testable
  (`ShellRouterTests`, 10 cases) while `ShellViewModel` (App) stays a thin adapter that wraps the
  router, exposes `CommunityToolkit.Mvvm` `[RelayCommand]`s, and marshals
  `IAuthService.ConnectionStateChanged` onto the UI thread via the captured `DispatcherQueue`. Added
  `ShellRoute` enum to Core too. **No contracts.md change** — these are new shell-owned types; the
  contract's "ViewModels live in App" still holds (the VM is in App).

- **Settings→Main preserves state via `Frame.GoBack()` + `NavigationCacheMode.Required` on
  `MainPage`.** Going *to* settings is a forward `Navigate` (pushes onto the back stack, keeping the
  cached main page); returning calls `GoBack()` so the same instance comes back. Top-level switches
  (e.g. just-connected Onboarding→Main) `Navigate` fresh and clear the back stack so Back can't return
  to onboarding.

- **Incremental wiring respected (spec §4).** The shell references only services that exist today —
  `IAuthService`, `IStartupInitializer`. `ISettingsService`/`IThemeService`/`ITrayService` are **not**
  referenced or stubbed; they wire in when features 10/11/08 land. The launch sequence is unchanged
  (single-instance redirect + settings load still marked as future pre-steps).

- **Pages resolve dependencies from a new `App.Services` static `IServiceProvider`.** Pages are
  created by `Frame.Navigate(type)` with no constructor injection, so the standard WinUI+Hosting
  pattern is a static service-locator accessor on `App`. Acceptable here; the page code-behind is
  throwaway placeholder content anyway (the real screens are features 04/05/07/10).

- **Preserved-slice plumbing:** added a clearly-marked throwaway `DevPlaybackSlice` (App singleton)
  that holds the volume read/set logic + last-known state, so the main screen's buttons and the global
  hotkeys share one source of truth. The `GlobalHotkeyWindow` lifetime moved to `MainWindow` (owns the
  HWND), armed once on `Connected` and disposed on `Closed`, routing presses into `DevPlaybackSlice`.
  All of this is replaced by features 03/04/05/06/07.

- **Window size uses `GetDpiForWindow` to scale logical→physical px.** `AppWindow.Resize` and
  `OverlappedPresenter.PreferredMinimumWidth/Height` take physical pixels; without scaling the ~480px
  logical window would shrink on high-DPI displays. Small `user32` P/Invoke (System32 search path),
  consistent with the existing hotkey interop.

- **Deviation from the approved plan: no `.resw` this phase.** The plan called for moving strings into
  a `Resources.resw`. Decided against it for now: the only durable shell string is the brand name
  "Amplify" (title bar), and every other visible string is throwaway slice/placeholder text that the
  owning features (04/05/10) will replace **together with their own localized resources**. Standing up
  `.resw` + `x:Uid` wiring for soon-to-be-deleted text — on code that can't be runtime-verified in this
  headless session (a wrong `x:Uid` fails silently at runtime, not at build) — is low value and risk.
  Localization-readiness (spec §5) is therefore deferred to when the screens gain real content; noted
  here so it isn't lost.

- **Contract changes:** none. Added new Core types (`ShellRoute`, `ShellRouter`) under `Navigation/`.

- **Deferred / known gaps (still open):** `ISettingsService` (10) + window-state persistence,
  `IThemeService`/manual theme override (11), `ITrayService` + single-instance redirect (08), the real
  Onboarding/Status/Volume/Settings screen content (03/04/05/07/10), `.resw` localization (above), the
  real Amplify logo (13).

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -p:Platform=x64` → **0 warnings, 0 errors** (`TreatWarningsAsErrors`).
  - `dotnet test` → **31 passed** (21 prior + 10 new `ShellRouter` cases).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean (exit 0).
  - **Outstanding (requires an interactive desktop, not runnable headless):** deploy/run the packaged
    app and confirm — Mica window with custom title bar (logo + "Amplify"); native min/max-restore/
    close + draggable title bar + content not under the caption buttons; launches to Onboarding when
    disconnected and Main when connected; Connect routes to Main and volume ±/global hotkeys still move
    Spotify's volume; Main→Settings→Back works and preserves Main state; content scrolls under a fixed
    title bar; window follows the Windows light/dark theme. Also confirm a rolling log file appears
    under `LocalFolder\logs\amplify-<date>.log`.

- **Verified facts:**
  - Windows App SDK 2.2 `TitleBar` control (`Microsoft.UI.Xaml.Controls.TitleBar`) is the current
    recommended custom-title-bar approach; `Window.ExtendsContentIntoTitleBar` must be set in code.
  - `MicaBackdrop` auto-falls-back to a solid colour where unsupported (no manual support check).
  - `CommunityToolkit.Mvvm` latest stable resolves to **8.4.2** on this SDK; compatible with the App's
    `net10.0-windows10.0.26100.0` TFM.

## 2026-06-21 — PR #7 review fixes · branch `feat/01-application-shell`

Addressed four findings from review of the Phase 1 PR. No contract changes; routing/tests unchanged.

- **Arm global hotkeys for the *initial* connected state, not only on transitions
  (`MainWindow`).** Hotkeys were armed reactively in `OnConnectionStateChanged`, but `App.OnLaunched`
  awaits `RestoreSessionAsync()` *before* the window is constructed and subscribed. Once token
  persistence lands (feature 03 Phase 1), a restore that sets `Connected` raises
  `ConnectionStateChanged` before the window exists, so the event is missed: the shell routes to Main
  correctly (the route is read from a `State` snapshot) but the global hotkeys never arm — on-screen
  buttons work, hotkeys silently don't. Fixed by extracting `OnConnected()` (arm + refresh) and
  invoking it from the constructor when `_authService.State == ConnectionState.Connected`, as well as
  from the transition handler. Latent today (restore is a no-op) but on the exact path feature 03's new
  "launch lands on Main after restore" manual check exercises.

- **Re-entrancy guard on `DevPlaybackSlice.RefreshAsync`.** On connect the window enqueues a refresh
  and the route advance triggers `MainPage.OnNavigatedTo`, which also refreshes — two overlapping reads
  mutating the cached fields and issuing duplicate GETs. Added a `_refreshing` flag (all calls are on
  the UI thread, so a simple bool collapses them into one in-flight read). Throwaway code, but cheap to
  make correct.

- **Drop HttpClient noise inside the file provider (not via a framework filter).** With no `Logging`
  config section the floor is Information, at which `System.Net.Http.HttpClient` logs request URIs into
  the file. Not a leak with defaults (token-exchange secrets are in the request *body*, not logged; the
  `Authorization` header is in the framework's default-redacted set) — but it's noise, and a latent
  token/PII risk if the level were ever lowered to log headers. First attempt used
  `AddFilter<FileLoggerProvider>(…)`, but although that is meant to be provider-scoped it also quieted
  the **Debug** provider in practice (verbose Debug output dropped). Moved the policy **into
  `FileLogger.IsEnabled`** instead — it keeps only `Warning`+ for `System.Net.Http.HttpClient*`
  categories — so the suppression is structurally confined to the file and the Debug provider stays
  fully verbose for development. App categories keep logging at Information.

- **Documented `ShellRouter`'s deliberate Connected→Onboarding asymmetry.** The router advances
  Onboarding→Main on connect but does **not** route Main/Settings back to Onboarding on a
  disconnect/reset. This is intentional for now — nothing in this phase triggers it (`DisconnectAsync`
  is unused, and a failed *connect* correctly leaves the user on Onboarding). Routing back on
  disconnect belongs to the connection-status / reset features (05/12) when there's an affordance to
  drive it; recorded here so the asymmetry isn't mistaken for an oversight.

- **Manual/integration checks (re-run after the fixes):**
  - `dotnet build Amplify.slnx -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test` → **31 passed** (unchanged).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean (exit 0).

## 2026-07-05 — Window placement: centre on first run + remember last position · branch `main`

Closed the long-deferred "window state persistence" gap (listed open since Phase 0/1). Symptom that
prompted it: launched from Visual Studio the window opened toward the upper-left, not centred —
`ConfigureWindowChrome` called `AppWindow.Resize` but never positioned the window, so it kept the OS
cascade default top-left corner and only grew from there. Now the window centres on first run and
restores its last footprint on subsequent launches.

- **`AppSettings.Window` is now written, not just round-tripped.** The `WindowState` record already
  existed (feature 10 built the model; nothing wrote it). `MainWindow` captures the window's last
  **Restored** footprint via `AppWindow.Changed` (`DidPositionChange`/`DidSizeChange`) and persists it
  through `ISettingsService.Update` when the window is put away (`VisibilityChanged` → not visible:
  minimise or hide-to-tray) and on close (`Dispose`, which runs on the shell's `Closed` handler
  *before* `App` disposes the host, so the write is safe). Minimised/maximised states report
  placeholder coordinates, so only `Restored` is captured; the persist compares against the stored
  value so redundant writes are skipped. `MainWindow` gained an `ISettingsService` ctor dependency
  (already a DI singleton).

- **Stored in device (physical) pixels, deviating from the `WindowState` XML doc's "logical
  pixels".** `AppWindow` reads/writes the Win32 Window Coordinate System (device pixels) directly —
  confirmed via `microsoft-docs:winui3` (`AppWindow.Position/Size`, `MoveAndResize`, and the
  windowing-overview "AppWindow uses device pixels, XAML uses effective pixels" note). Persisting the
  raw `AppWindow` values means restore needs **no** DPI round-trip (which would be lossy and ambiguous
  across mixed-DPI monitors). Updated the `WindowState` doc comment to say device pixels; **no
  contracts.md change** (the record's shape/fields are unchanged, and contracts.md never annotated the
  pixel space). Per user direction the schema version was **not** bumped — the app is pre-release and a
  stale local `settings.json` only affects the user's own install, which they'll resolve manually.

- **Off-screen guard, kept as pure testable geometry.** A remembered placement is only restored when
  its title-bar strip lands on a currently-connected display (so an unplugged/rearranged monitor can't
  strand the window off-screen) — otherwise it falls back to centred. That decision plus the centring
  math live in `Amplify.Core/Windowing/WindowPlacement.cs` (WinUI-free, operates on plain `PixelRect`
  work areas); `MainWindow` just feeds it `DisplayArea.FindAll()`/`GetFromWindowId` work areas and
  applies the result with a single `AppWindow.MoveAndResize`. Saved size is grown to the existing
  min-width/height floor. Six `WindowPlacementTests` cover restore-on-primary, min-size clamp,
  off-screen-left/above rejection, secondary-monitor restore, and centring.

- **Contract changes:** none.

- **Manual/integration checks:**
  - `dotnet build src\Amplify.App` and `src` test project → **0 warnings, 0 errors** (Release x64,
    `TreatWarningsAsErrors`).
  - `dotnet test -p:Platform=x64` → **203 passed, 0 skipped** (197 prior + 6 new `WindowPlacement`).
  - **Outstanding (requires an interactive desktop):** launch and confirm the window opens centred on
    first run; move/resize it, close, relaunch, and confirm it reopens where it was left; then unplug
    the monitor it was on (or clear `settings.json`'s `window`) and confirm it falls back to centred.

- **Runtime fix (same session, found on manual run): `DisplayArea.FindAll()` can't be LINQ/`foreach`-
  enumerated.** The first launch threw `InvalidCastException` ("Specified cast is not valid") from
  `PositionWindow`'s `DisplayArea.FindAll().Select(...).ToList()`. `FindAll()` returns a projected
  WinRT `IReadOnlyList<DisplayArea>`; enumerating it makes CsWinRT `QueryInterface` the underlying COM
  object for `IEnumerable<DisplayArea>` (`Make_IEnumerableObjRef` → `As<T>(iid)` → `E_NOINTERFACE`),
  which fails on Windows App SDK 2.2. Replaced the LINQ projection with an indexed `for` loop
  (`.Count` + `[i]`) in a `ReadWorkAreas()` helper — indexed access goes through the list's own
  projected interface and marshals cleanly. Reminder for any future WinRT collection use (e.g.
  `AppWindow`/`DisplayArea`/other `FindAll`-style APIs): prefer indexed access over LINQ/`foreach`.
  `WindowPlacement` itself was unaffected (its unit tests pass a plain managed list, so they never
  exercised the WinRT enumeration — a reminder that Core-side geometry tests can't catch App-side
  marshalling faults). Re-verified: build 0/0, `dotnet test` 203 passed, `dotnet format` clean.

- **Save-on-move (same session): the position now survives a hard kill, not just a graceful close.**
  Manual testing showed the placement only persisted when Amplify closed itself (Quit / close- or
  minimise-to-tray fire `Closed`/`VisibilityChanged`); **stopping the debugger `TerminateProcess`es
  the app, so no handler runs and nothing is saved.** Added a debounced save: `OnAppWindowChanged`
  restarts a one-shot `DispatcherQueueTimer` (1s) that flushes `_pendingWindow` once moves/resizes
  settle, so a crash or force-kill loses at most the last ~1s of dragging. The graceful-close flushes
  (`VisibilityChanged` → not visible, and `Dispose`) are kept as immediate saves; the timer is stopped
  and detached in `Dispose`. Re-verified: build 0/0, `dotnet test` 203 passed, `dotnet format` clean.

- **PR #24 review fix: restore min-size used the wrong monitor's DPI.** `ConfigureWindowChrome` read
  `GetDpiForWindow` once at construction — when the window sits on the primary/default monitor — and
  used that `scale` for both the presenter minimum and the restore clamp. Restoring a window that was
  saved near its minimum on a *lower*-DPI monitor, during a cold start on a *higher*-DPI primary, then
  inflated it (e.g. a 560-device-px window grown to 1120 device px = ~2× the intended logical minimum
  on a 100% monitor). Confirmed via `microsoft-docs:winui3` that `PreferredMinimumWidth/Height`
  **also constrain programmatic `AppWindow.Resize`/`MoveAndResize`**, so both the clamp *and* the
  presenter minimum contributed — fixing only the clamp (the flagged line) would not have resolved it.
  Fix: `PositionWindow()` now scales the min/default sizes by the **target** monitor's effective DPI
  (`MonitorFromPoint` at the saved top-left → `GetDpiForMonitor`, both System32 P/Invokes matching the
  existing `GetDpiForWindow` pattern) and sets the presenter minimum from the same scale; the default
  (first-run/off-screen) path still uses the construction monitor's DPI, which is correct because the
  window opens there. `WindowPlacement` and its tests are unchanged — it's now simply fed the correct
  minimum. Re-verified: build 0/0, `dotnet test` 203 passed, `dotnet format` clean.
