# Build notes — Feature 10: Settings & Persistence

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-22 — Phase 1 (full build) · feat/10-settings-persistence

- **Deviations from spec/contracts:**
  - **`SettingsService` and the migration engine live in `Amplify.Core`, not `Amplify.App`.** They
    use only `System.IO` + `System.Text.Json` and take a directory path, so the load/migrate/
    round-trip logic is unit-testable against a temp directory (mirrors the existing
    `SpotifyClient`-in-Core precedent and getting-started §2's "settings serialisation + migration"
    placement). The App layer only resolves the per-user directory (`LocalFolder` with a temp-dir
    fallback for unpackaged runs) in `AddSettings()`.
  - **`Amplify.Core` gained its first package reference:** `Microsoft.Extensions.Logging.Abstractions`
    (10.0.9), so `SettingsService` can log load/migration diagnostics through `ILogger` without ever
    surfacing them to the UI.
  - **JSON shape:** camelCase property names + case-insensitive reads, enums serialised as strings,
    `schemaVersion` emitted first (declaration order). The contract examples imply camelCase.
  - **CA1716 suppressed on `ISettingsService.Get<T>`** — the name is fixed by the shared contract and
    the app is C#-only (the analyzer flags `Get` as a VB keyword).
  - **Backups before every reset, not just version changes:** the prior file is copied to
    `settings.v{old}.bak` for a known-version migrate/reset and to `settings.corrupt.bak` when the
    file can't be parsed / has no readable `schemaVersion`, honouring "back up before any reset" even
    when the version is unknown.

- **Contract changes:** none — `ISettingsService`, `AppSettings`, `WindowState` implemented exactly
  as in [contracts.md](../contracts.md). New Core types added this feature: `ThemeMode` (enum — owned
  conceptually by feature 11's theming, but `AppSettings` needs it now, so it lands here), and the
  migration seam `ISettingsMigrator` / `SettingsMigrationRunner` / `SettingsMigrationResult` /
  `SettingsMigrationOutcome`. The migration seam is an internal mechanism, not a cross-feature
  contract, so it is intentionally **not** added to contracts.md.

- **Assumptions:**
  - **Account & Reset sections rendered read-only** (account status from `IAuthService`;
    Disconnect/Reset buttons present but disabled) — their behaviour is owned by features 05/12.
    Confirmed with the user.
  - **Client ID is display-only** and `DevClientIdSource` is left untouched — wiring auth to read
    `AppSettings.SpotifyClientId` belongs to feature 04 (onboarding). Confirmed with the user.
  - **Theme combo persists `ThemeMode` only**; applying the theme live is feature 11.
  - **Migrator registry is empty at `CurrentSchemaVersion = 1`.** The engine is fully built and tested
    via synthetic migrators (the `internal` `SettingsService` ctor that injects migrators + target
    version, exposed to tests via `InternalsVisibleTo`, plus direct `SettingsMigrationRunner` tests),
    so the chain is proven for when the first real migrator lands.
  - Footer brand links to `https://github.com/chrisgid/Amplify` per the feature doc (matches the git
    `origin`).

- **Deferred / known gaps:**
  - The persisted toggles drive other features' behaviour, which is wired when those features land via
    `ISettingsService.Changed`: StartupTask + tray (08), toasts (09), theme application (11), reset
    (12). This feature only stores the values.
  - `AppSettings.Window` round-trips but nothing writes it yet (feature 01's optional window-state
    persistence).
  - This is the feature that **stands up the shared `Resources.resw`** (deferred by feature 01) plus
    the `x:Uid` / `ResourceLoader` convention. Keys are namespaced with a `Settings_` prefix; later
    screens add their own prefixed keys (`Onboarding_*`, `Status_*`, `Volume_*`, …) to the **same**
    file rather than forking per-feature `.resw` files.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx --configuration Release -p:Platform=x64` → 0 warnings, 0 errors
    (strict settings inherited from Directory.Build.props).
  - `dotnet test` → 47 passing, 0 skipped (settings round-trip / defaults / corrupt-recovery /
    change-notification / atomic-write, and the migration matrix: single-hop, multi-hop ordering,
    newer→defaults, backup-written, throwing-migrator→defaults).
  - Confirmed the built `resources.pri` contains the `Settings_*` resource names, so `x:Uid` /
    `ResourceLoader` resolve at runtime. Full UI persistence (toggle → reopen) is a manual check left
    for the Phase 2 integration smoke test (no UI run in this session).

- **Verified facts:**
  - **`CommunityToolkit.WinUI.Controls.SettingsControls` v8.2.250402** (namespace
    `CommunityToolkit.WinUI.Controls`, MIT) restores and builds against WinAppSDK 2.2 / .NET 10.
  - **WinUI 3 `.resw` placement:** `Strings/en-US/Resources.resw` is auto-included as a `PRIResource`
    by the .NET 10 SDK — no explicit MSBuild item needed (confirmed via the indexed `resources.pri`).
    `<Resource Language="x-generate"/>` in the manifest generates the language list.
  - **WinUI 3 code-side loader is `Microsoft.Windows.ApplicationModel.Resources.ResourceLoader`**
    (Windows App SDK), distinct from the UWP `Windows.ApplicationModel.Resources.ResourceLoader`.
  - **CommunityToolkit.Mvvm 8.4 in WinUI requires `[ObservableProperty]` on `partial` properties**,
    not fields — field usage errors with analyzer `MVVMTK0045` (CsWinRT marshalling).
