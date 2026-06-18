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
