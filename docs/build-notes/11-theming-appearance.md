# Build notes — Feature 11: Theming & Appearance

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-23 — Phase 1 (full build) · branch `feat/11-theming-appearance`

Added the `IThemeService` that actually *applies* the appearance preference. Feature 10 already
persisted `AppSettings.ThemeMode` from the Settings combo; this feature drives the window's theme
from it and keeps it in sync with Windows. The diff covers the mechanics — recorded below is only
the non-obvious.

- **Deviations from spec/contracts:**
  - **`IThemeService` lives in `Amplify.Core/Theming/`** (alongside the other service interfaces),
    implemented **exactly** as in contracts.md (`void Apply(ThemeMode)` + `event EventHandler
    ThemeChanged`). No contract change.
  - **New Core type `ResolvedTheme { Default, Light, Dark }` + pure `ThemeResolver.Resolve`.** The
    test project references `Amplify.Core` only (no `Microsoft.UI.Xaml`), so the `ThemeMode →`
    effective-theme decision is expressed in a UI-free Core type to make it unit-testable; the App
    layer does the trivial `ResolvedTheme → ElementTheme` translation. This is an internal mechanism,
    not a cross-feature contract, so it is **not** added to contracts.md (mirrors how feature 10
    treated its migration seam).
  - **`ThemeService` is `public`, not `internal`.** `MainWindow`'s constructor is `public` and takes
    the concrete `ThemeService` (to read the resolved `ElementTheme CurrentTheme`), so the parameter
    type must be at least as accessible (CS0051). Matches the existing `public sealed` DI types the
    window already takes (`ShellViewModel`, `DevPlaybackSlice`). The `IThemeService`/`ResolvedTheme`
    public surface is in Core; the App `CurrentTheme` property is App-only and not on the interface.

- **Contract changes:** none.

- **Assumptions / design (docs were silent):**
  - **System mode → `ElementTheme.Default` on the content root, which WinUI 3 follows live** (verified
    via microsoft-docs). Light/Dark map to the fixed `ElementTheme` values. The service therefore does
    **not** read the current OS theme itself for System mode — the framework does. `ThemeResolver`'s
    "System → Default" is exactly this "follow the OS" mapping.
  - **`FrameworkElement.RequestedTheme` is applied to the window's content root**, not
    `Application.RequestedTheme` — the latter throws `NotSupportedException` if set while the app is
    running, whereas the element property is runtime-settable (verified via microsoft-docs). The root
    carries the Mica backdrop and the `TitleBar` control along; the OS accent flows through the system
    accent brushes with no code.
  - **The window owns applying `RequestedTheme`; the service owns computing it + watching for change.**
    Per the contracts event table (`IThemeService.ThemeChanged → consumed by 01`), `MainWindow`
    subscribes to `ThemeChanged` and re-applies `_theme.CurrentTheme` to its root (initial apply in
    the ctor after `InitializeComponent`, unsubscribe in `Dispose`). Keeps the platform `RequestedTheme`
    plumbing in the window that owns the content while the preference/OS logic stays in the singleton.
  - **`UISettings.ColorValuesChanged` is the live-OS hook**; it fires off the UI thread, so the
    service marshals `ThemeChanged` back via the captured `DispatcherQueue`. The `UISettings` instance
    is held in a field (the subscription is dropped if it is collected). On a colour/theme change the
    service raises `ThemeChanged` **unconditionally** (not via `Apply`'s idempotent guard): while
    following the system the resolved `ElementTheme` stays `Default`, so `Apply` would no-op, but the
    window still needs to re-assert and accent-driven surfaces refresh.
  - **`UISettings` construction is wrapped in try/catch** (`InvalidOperationException`/`COMException`):
    on an unpackaged/headless run there is no view context, so live OS-following is simply not wired
    while the manual override still works. Same fallback philosophy as `AddSettings`/the file logger.
  - **`ThemeService` is the `IStartupInitializer` at `Order = 100`.** `OnLaunchedAsync` calls
    `Apply(settings.Current.ThemeMode)` so `CurrentTheme` is correct before the shell resolves
    `MainWindow` (the window reads it in its ctor). Registered as one shared singleton exposed as
    `ThemeService` + `IThemeService` + `IStartupInitializer` via `AddTheming()`.

- **Deferred / known gaps:** none specific to theming. The override persists via feature 10's existing
  combo; no new `.resw` keys were needed (no new user-facing strings).

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -p:Platform=x64` → **0 warnings, 0 errors** (strict
    `TreatWarningsAsErrors`/nullable).
  - `dotnet test` → **56 passed, 0 skipped** (52 prior + 4 new `ThemeResolver` cases: System→Default,
    Light→Light, Dark→Dark, unknown→Default).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean (after a normalising pass — the new files
    were authored with LF; the repo enforces CRLF via `.editorconfig`).
  - **Outstanding (requires an interactive desktop, not runnable headless), per the acceptance
    criteria:** launch → matches current Windows theme + accent; toggle Windows light/dark → Amplify
    updates live (no restart); change Windows accent → reflected; Settings → App theme = Light/Dark
    applies instantly and persists across restart; switching back to "Use system" resumes live
    OS-following; all surfaces stay legible via system brushes; high-contrast renders via native
    controls. Also confirm Mica tint and the title bar follow the chosen Light/Dark override.

- **Verified facts (microsoft-docs):**
  - `FrameworkElement.RequestedTheme` (`ElementTheme`) is settable at runtime; `Application.RequestedTheme`
    throws `NotSupportedException` if set while running — so the override is applied on the content root.
  - `ElementTheme.Default` on a desktop window's root follows the OS app theme live (WinUI 3 auto-detects;
    the old "Default = always Dark" note is a stale Windows 8.x/Phone caveat).
  - Setting `RequestedTheme` on the content root carries the `MicaBackdrop` and the WinAppSDK 2.2
    `TitleBar` control with it; the OS accent flows via `SystemAccentColor`/accent system brushes.
  - `Windows.UI.ViewManagement.UISettings.ColorValuesChanged` is the event for OS theme/accent changes
    and fires on a non-UI thread.
