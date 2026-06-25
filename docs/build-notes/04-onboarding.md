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

## 2026-06-25 — Bug fixes + Cancel button

- **Bug:** the Connect button stayed disabled after typing a Client ID. Root cause: `{x:Bind}`'s
  default `UpdateSourceTrigger` for `TextBox.Text` is `LostFocus`, not `PropertyChanged` (unlike
  every other property, where `x:Bind` TwoWay defaults to `PropertyChanged` — confirmed via
  Microsoft Learn's "{x:Bind} markup extension" and "Data binding in depth" docs). Since the
  disabled button can't take focus on click, there was no way to trigger the commit. Fixed by adding
  `UpdateSourceTrigger=PropertyChanged` to the Client ID `TextBox` binding.
- **Removed the connect spinner** (`ProgressRing`/`IsConnecting`) per request — the Connect button's
  text already communicates the waiting state via `ConnectButtonText`.
- **Layout:** the Denied/error `InfoBar`s were leaving a gap in the `StackPanel` even while closed;
  added an explicit `Visibility` binding alongside `IsOpen` so a closed bar takes no `StackPanel`
  spacing.
- **Added a Cancel button**, shown only during `OnboardingPhase.Authorizing`, so a user who closed
  the browser tab can retry without waiting out the connect timeout. `OnboardingFlow.Cancel()`
  (Core) resets the phase; the view-model also bumps an attempt counter (to discard any late result)
  and, since `IAuthService.ConnectAsync` now accepts a `CancellationToken` (see the 2026-06-25 entry
  in `build-notes/03-spotify-authentication.md`), actually cancels the in-flight attempt rather than
  just ignoring its eventual result.
- **Manual/integration checks (closing the gap left open above):** run on a dev desktop with a real
  Spotify Client ID and Premium account. Connect (6-step guide → copy redirect URI → paste Client ID
  → Connect → browser consent → approve) routes to Main on return; Cancel mid-Authorizing returns to
  Welcome and a subsequent retry connects cleanly; declining consent on Spotify shows the Denied
  `InfoBar` and leaves Connect usable again. All three passed.

## 2026-06-25 — Code-review fixes

Eight review findings, all addressed:

- **`OnboardingViewModel.ConnectAsync` could escape unhandled (was high).** Only the underlying
  `IAuthService.ConnectAsync` converted failures to `AuthResult`; any other exception (e.g. a
  malformed-JSON edge case) would propagate out of the view-model with nothing to reset
  `OnboardingFlow.Phase`, leaving the screen stuck on Authorizing with no way to retry short of
  restarting the app. Wrapped the await in a `catch (Exception ex)` that logs
  (`ILogger<OnboardingViewModel>`, newly injected) and converts to a generic
  `Onboarding_Helper_UnexpectedError` `AuthResult` instead.
- **Replaced the hand-rolled cancellation/staleness tracking with CommunityToolkit.Mvvm's built-in
  support (was medium — maintainability/bug-surface).** `[RelayCommand(IncludeCancelCommand = true)]`
  on `ConnectAsync(CancellationToken)` generates `ConnectCancelCommand` and has the toolkit manage the
  `CancellationTokenSource` lifetime, including refusing a new execution while one is still running —
  removing the manual `_connectAttempt`/`_connectCts`/`ReferenceEquals` bookkeeping entirely. The
  "stale result" guard moved into `OnboardingFlow.OnConnectResult` itself (ignored outside
  `Authorizing`), which is simpler *and* better-placed: it's now impossible for a result to resurrect
  state after `Cancel()`, in Core, covered by `OnboardingFlowTests`, rather than relying on App-side
  counter bookkeeping with no direct test coverage.
- **`Cancel` had no `CanExecute` guard (was low-medium).** Unlike `ConnectCommand`, `CancelCommand`
  was invocable any time the button happened to be reachable, with `OnboardingFlow.Cancel()`'s no-op
  as the only safety net. Added `CanCancel => IsAuthorizing` and
  `[RelayCommand(CanExecute = nameof(CanCancel))]` so the "impossible" state is unreachable via the
  command itself, not just hidden by XAML `Visibility`.
- **Extracted the duplicated dispatcher-marshalling helper (was low — maintainability).**
  `OnboardingViewModel.RunOnUi`, `SettingsViewModel.RunOnUi`, and an inlined copy in
  `ShellViewModel.OnConnectionStateChanged` were three identical
  `_dispatcher is null || _dispatcher.HasThreadAccess` checks. Replaced all three with a shared
  `DispatcherQueue.RunOnUi(Action)` extension method (`ViewModels/DispatcherQueueExtensions.cs`).
- **Credential persisted before a cancelled attempt was observed (was high — feature 03's file; see
  its own 2026-06-25 entry).** `CompleteConnectAsync` now rolls back the just-persisted refresh token
  if cancelled between the token exchange and the account read.
- **Cancel-vs-timeout message accuracy (was low — feature 03's file).** Switched to checking the
  timeout `CancellationTokenSource`'s own flag directly rather than inferring from the external
  token, and fixed a variable-scoping bug introduced by the same change (`timeout` was declared
  inside the `try` it needed to be read from in the `catch`).
- **Magic-value cleanup (was low).** The Spotify Developer Dashboard/Terms URLs were hardcoded in
  `OnboardingPage.xaml`; moved to a new `Amplify.Core.Onboarding.OnboardingLinks` (mirroring
  `SpotifyOAuthConstants`), exposed as `Uri`-typed view-model properties (`DashboardUri`/`TermsUri`)
  and bound via `x:Bind` to `Hyperlink.NavigateUri` — confirmed `x:Bind` supports binding a `Uri`
  property directly to `NavigateUri`. The 1.5s clipboard-feedback delay became a named
  `_copyFeedbackDuration` static field, matching `SpotifyAuthService`'s `_consentTimeout`/
  `_refreshSkew` convention.
- **`RedirectUriNotCopied` — investigated, kept as-is.** The suggested simplification
  (`Visibility="{x:Bind !ViewModel.RedirectUriCopied}"`) was tried directly: the WinUI 3 `x:Bind`
  parser rejects a leading `!` in a property path (`token recognition error at: '!'`). No converter
  exists in the codebase to introduce instead, so the dedicated property + change-handler stays;
  documented why in a `<remarks>` on the property so it isn't "simplified" again without checking.
- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test tests/Amplify.Tests` → **91 passed** (+2 new `OnboardingFlow` staleness tests).
  - Live manual re-walk (Connect / Cancel-mid-Authorizing / deny) not repeated this session — no
    behavioural change to the user-visible flow, only to its internals and to two latent bugs that
    needed a race to trigger.
