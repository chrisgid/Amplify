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

## 2026-08-25 — Fix: system caption buttons ignored the app theme · branch `fix/11-caption-button-theming`

The Phase 2 smoke test logged item 11 as *pass with defect*: in the light theme the window's
Minimise/Maximise/Close buttons kept their dark-mode look (white glyphs, near-black hover). Everything
else — including the `TitleBar` control's own title and icon — themed correctly.

- **Root cause:** `MainWindow.ApplyTheme()` only set `RequestedTheme` on the content root. The system
  caption buttons are **not in the XAML tree** — the `TitleBar` control merely reserves space for them
  and they are drawn by `AppWindowTitleBar`, so `RequestedTheme` never reaches them and the deprecated
  `WindowCaptionBackground`-style resource theming has no effect either. They have to be given explicit
  colours. The stale comment above `ApplyTheme()` (the root "carries the … title bar along") was
  corrected to say which parts that does and doesn't cover.

- **Deviations from spec/contracts:** none. No contract change: `EffectiveTheme` is on the concrete
  `ThemeService` (which `MainWindow` already depends on for `CurrentTheme`), not on `IThemeService`.

- **Assumptions / design (docs were silent):**
  - **The colours are set inside `ApplyTheme()`, not in `ConfigureWindowChrome()`**, so they re-run on
    every `ThemeChanged` rather than only at window creation. `ThemeService` already raises
    `ThemeChanged` unconditionally on `UISettings.ColorValuesChanged`, so an OS light/dark switch
    re-evaluates them live with no new plumbing.
  - **New Core seam `ThemeResolver.ResolveEffective(ThemeMode, bool systemIsDark)`** returning a
    concrete `Light`/`Dark` — never `Default`. This is the trap in this fix: `Resolve` deliberately
    maps `System → Default` because the *framework* resolves that for XAML elements, but a colour
    picker has no "follow the OS" option. The OS read is the caller's job, which keeps the mapping
    pure and unit-testable (7 new cases, incl. "never returns Default" and "an override beats the OS").
  - **`ThemeService.EffectiveTheme` does the OS read** via the `UISettings` instance it already holds:
    `GetColorValue(UIColorType.Background)` is black under the dark theme and white under the light one,
    tested with the standard perceived-brightness weighting. It is a **property evaluated per call**,
    not a cached field — while following the system the OS theme can change under us. Falls back to
    light where `UISettings` is unavailable (unpackaged/headless), matching the Windows default.
  - **`ThemeService` now keeps the `ThemeMode` it resolved** in `_mode`, because `ElementTheme.Default`
    is lossy — it says "follow the OS" without saying which OS theme is in force. `_mode` is assigned
    **before** `Apply`'s idempotence guard, since `System` and an unknown value both resolve to
    `Default` and the guard can short-circuit a preference change `EffectiveTheme` still needs to see.
  - **Colour choices** (`MainWindow`'s private statics): glyph tones are the Fluent primary/disabled
    text fills flattened onto their backdrop — the foreground properties **ignore the alpha channel**,
    so they must be opaque. Hover/pressed follow the Fluent subtle-fill roles (translucent black over
    light, white over dark, pressed lighter than hover). `ButtonBackgroundColor` and
    `ButtonInactiveBackgroundColor` stay `Colors.Transparent` so Mica shows through.
  - **Guarded on `AppWindowTitleBar.IsCustomizationSupported()`**, which is false only on Windows 10
    builds predating title bar customisation — there the system draws its own themed buttons anyway.

- **Deferred / known gaps:**
  - **The Close button's hover/pressed background is system-defined** (the red) and can't be
    overridden — documented behaviour, not a defect. Don't chase it.
  - The unrelated Phase 2 defect (Spotify-unreachable never surfaced in the UI) is untouched.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test` → **265 passed, 0 skipped** (254 baseline + 11 new `ResolveEffective` cases).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean.
  - **Not reachable by unit tests** (OS/UI-bound), so verified by hand on a packaged run — **pass**:
    Windows light + override System → dark-on-light glyphs; Windows dark + override System →
    light-on-dark; override Light while Windows is dark, and override Dark while Windows is light →
    buttons follow the **app**, not the OS; hover legible in both themes; and switching the Windows
    theme while the app is running (including while tray-hidden, then reopened) updates the colours
    live rather than only at startup.

- **Verified facts (microsoft-docs):**
  - [TitleBar — Anatomy](https://learn.microsoft.com/windows/apps/develop/ui/controls/title-bar#anatomy):
    the system caption buttons are not part of the `TitleBar` control; it allocates space and
    `AppWindowTitleBar` owns their customisation.
  - [Title bar customization](https://learn.microsoft.com/windows/apps/develop/title-bar): the caption
    *background* properties (`ButtonBackgroundColor`, `ButtonHoverBackgroundColor`,
    `ButtonPressedBackgroundColor`, `ButtonInactiveBackgroundColor`) honour the alpha channel **only**
    while content is extended into the title bar; **all other colour properties ignore alpha**. Setting
    a property to `null` resets it to the system colour. The Close button's hover/pressed background is
    always system-defined. Colour customisation is a no-op on Windows 10.

## 2026-08-25 — Code review fixes (PR #48)

Two valid findings on the caption-button fix above; both implemented.

- **Contrast themes were unhandled.** With a Windows contrast theme active *and* an explicit
  Light/Dark override (e.g. "Night sky" + Appearance = Light), the hardcoded palette painted
  `#1B1B1B` glyphs onto a black title bar. `ApplyCaptionButtonColors` now checks
  `Microsoft.UI.System.ThemeSettings.HighContrast` first and, when set, assigns **null** to all eight
  properties — documented as "resets it to the default system colour". It resets rather than skips, so
  turning a contrast theme *on* while running clears colours set before it.
  - **`ThemeSettings` lives in `MainWindow`, not `ThemeService`.** It is created with
    `ThemeSettings.CreateForWindowId(AppWindow.Id)`, and the service deliberately holds no UI
    reference. Held in a field for the same reason as the service's `UISettings` — the docs are
    explicit that `Changed` stops firing once the object is collected. Subscribed alongside the other
    window events and unsubscribed in `Dispose`.
  - **Its `Changed` handler marshals to the UI thread itself.** `ThemeService` documents that it owns
    marshalling for *its* OS sources so the window can apply directly; this is a second OS source
    wired straight to the window, so the window owns marshalling for it.
  - Per the [contrast themes](https://learn.microsoft.com/windows/apps/design/accessibility/high-contrast-themes)
    guidance, a contrast-theme palette is user-customisable — app-chosen foreground colours are the
    wrong thing there by design, not merely a bad fit for one scheme.

- **`EffectiveTheme` read the OS unconditionally, and unguarded.** `ResolveEffective`'s second
  parameter is now a `Func<bool>` rather than a `bool`, so the OS is queried **only** for the modes
  that follow it — a pinned Light/Dark never calls it. Laziness is a property of the seam rather than
  of one call site, and is covered by two new tests (read / not read). `IsSystemDark` also wraps
  `GetColorValue` in the same `InvalidOperationException`/`COMException` catch the constructor already
  uses around `new UISettings()`: it runs from a `ThemeChanged` callback on the dispatcher, where a
  throw would be unhandled and take the app down.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → 0 warnings, 0 errors.
  - `dotnet test` → **269 passed, 0 skipped** (265 + 4 new lazy-read cases).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean.
  - The contrast-theme path was verified by hand on a packaged run — **pass**: a contrast theme
    (Settings > Accessibility > Contrast themes, or Left Alt + Left Shift + PrtScn) toggled *while the
    app is running*, with the Appearance override set to Light and then Dark, hands the buttons back
    to the system's contrast colours and returns them when it is switched off.
