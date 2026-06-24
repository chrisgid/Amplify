# Build notes — Feature 04: Onboarding / First-Run Experience

> Append a new dated entry each time a session works on this feature. Add to the end; don't
> rewrite earlier entries.

## 2026-06-24 — Phase 1 (full build)

- **Deviations from spec/contracts:** None to `contracts.md` itself, but the phase-transition logic
  was factored differently than the doc's wording implies: `OnboardingPhase` and a new
  `OnboardingFlow` state machine live in `Amplify.Core/Onboarding/`, mirroring how `ShellRouter`
  carries the shell's routing rules — `OnboardingViewModel` (in `Amplify.App`) is a thin adapter
  over it, the same split `ShellViewModel`/`ShellRouter` already establish. This was necessary
  because `tests/Amplify.Tests` only references `Amplify.Core` (plain `net10.0`, no WinUI), so any
  logic worth unit-testing has to live there — `SettingsViewModel`/`ShellViewModel` themselves have
  no direct tests for the same reason.
- **Contract changes:** none (the `OnboardingPhase` enum is unchanged from `contracts.md`).
- **Assumptions:**
  - A successful `ConnectAsync()` result leaves `OnboardingFlow.Phase` at `Authorizing` rather than
    advancing it to `Verifying`. `ShellRouter` already navigates `Onboarding → Main` as soon as
    `IAuthService` raises `ConnectionStateChanged(Connected)`, which happens essentially
    immediately after `ConnectAsync` returns success — a distinct "Verifying" UI state would only
    ever flash, so it isn't modeled. The enum value is kept for forward-compatibility with the
    contract; `OnboardingFlow` simply never sets it.
  - No UI-level connect timeout was added. `SpotifyAuthService.ConnectAsync` already enforces its
    own ~5-minute callback timeout (per the feature 03 build notes) and resolves to a non-success
    `AuthResult` rather than hanging, so a second timeout in the view-model would be redundant.
  - The redirect URI shown in the copy chip is built from the existing
    `SpotifyOAuthConstants.RedirectUri(port)` + `IOptions<SpotifyOptions>.RedirectPort` (already
    used by `SpotifyAuthService`) rather than a new literal, per the "no magic values" standard.
  - No `IValueConverter` exists anywhere in the codebase yet; kept it that way — the copy-chip
    glyph swap and all `Visibility` bindings use WinUI's built-in bool→`Visibility` conversion for
    direct (non-function) `x:Bind` property paths (confirmed via Microsoft Learn: available since
    Windows 10 1607, applies to property bindings, not function bindings — Amplify's min platform
    well exceeds that).
  - `OnboardingViewModel` is registered as a DI singleton, matching `SettingsViewModel`'s lifetime.
- **Deferred / known gaps:** The Amplify logo asset doesn't exist yet (feature 13, not yet built),
  so the welcome header is text-only, same as the Phase 0 placeholder it replaces.
- **Manual/integration checks:** Not yet run in this session — requires a real Spotify Client ID
  and Premium account; see the feature doc's *Testing* section for the manual connect/deny walk.
- **Verified facts:** WinUI/UWP's implicit bool→`Visibility` x:Bind conversion (Microsoft Learn,
  "{x:Bind} markup extension" + "Data binding in depth" docs) — works for property-path bindings on
  builds targeting SDK 14393+, which Amplify's `net10.0-windows10.0.26100.0` far exceeds.
