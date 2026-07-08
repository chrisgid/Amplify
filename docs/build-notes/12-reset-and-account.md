# Build notes — Feature 12: Reset & Account Management

> Append a new dated entry each time a session works on this feature. Add to the end; don't rewrite
> earlier entries.

## 2026-07-08 — Phase 1 (full build) · feat/12-reset-and-account

- **Contract changes:**
  - Added `void ISettingsService.Reset()` (restore all defaults, persist atomically, raise
    `Changed`) and a new `IResetService { Task ResetAsync(); }` — both in
    [contracts.md](../contracts.md). `ResetService` (Amplify.Core) resets settings **then**
    disconnects, so it's covered by the Core unit suite (NSubstitute on `ISettingsService` +
    `IAuthService`).
- **Routing behaviour change (shared):** `ShellRouter.OnConnectionStateChanged` now also routes to
  **Onboarding on `ConnectionState.Disconnected`** (from anywhere), in addition to the existing
  Onboarding→Main on `Connected`. This is the mechanism by which Disconnect **and** Reset return the
  app to first-run — the doc's "raise state change so the shell routes to onboarding". `Connecting`
  and `Error` still leave the route untouched (feature 05 surfaces those in place with a reconnect
  InfoBar), so a background-refresh hiccup doesn't yank the user off Main/Settings. Reconnect-then-
  decline from the Main error state now lands on Onboarding — intended (no session → first-run).
- **Onboarding stale-state fix (surfaced by the disconnect routing):** `OnboardingViewModel` is a
  singleton and `OnboardingFlow` deliberately leaves `Phase = Authorizing` after a *successful*
  connect (the shell navigates away, so a distinct "verifying" phase would only flash). Once a
  disconnect routes back to the reused onboarding screen, that stale phase resurfaced as a "Waiting
  for Spotify" button on a screen the user hadn't touched. Fix: added `OnboardingFlow.Reset()` (clears
  to a clean `Welcome`, no-op if already clean) and `OnboardingViewModel` now calls it when
  `IAuthService` raises **`Connected`** — cleaning the singleton after each successful connect. Keyed
  on `Connected` (not `Disconnected`) on purpose: a denial/error also ends at `Disconnected`, and
  resetting there would wipe the failure state the attempt just set before its InfoBar shows.
- **Onboarding Client ID field re-sync:** the same singleton also kept the old typed Client ID in its
  text field after a reset (which clears `SpotifyClientId` in the store). `OnboardingViewModel` now
  seeds `ClientId` from `ISettingsService.Current.SpotifyClientId` at construction and re-syncs it on
  every `Disconnected` — so a reset shows an empty field, a plain disconnect prefills the stored ID
  for a one-click reconnect, and a failed attempt keeps the value the user just entered (it's
  persisted to the store *before* the attempt begins, so the stored value already matches).
- **How defaults hotkeys get re-applied:** the reset flow does **not** touch `IHotkeyService`
  directly. `HotkeyRegistrar` already listens to `ISettingsService.Changed` and re-registers from the
  stored combos, so `ISettingsService.Reset()` (which restores the default canonical strings)
  re-registers the default hotkeys as a side effect. Reset order is settings-first, disconnect-last.
- **Reset scope decision:** a full reset wipes the **entire** `AppSettings`, including the saved
  `WindowState` (`Reset()` swaps in a fresh `AppSettings`). Confirmed with the user — true first-run
  semantics; the currently-open window doesn't move (placement is only read at window construction),
  it just re-centres on next launch. `TrayHintShown` resets to false too, so the first-minimise hint
  shows again after a reset.
- **Confirmation dialog placement:** the `ContentDialog` lives in `SettingsPage.xaml.cs` (needs
  `XamlRoot`); `SettingsViewModel.ResetCommand` performs the reset unconditionally and is only invoked
  on `ContentDialogResult.Primary`. So "Cancel changes nothing" holds by construction and the reset
  logic stays unit-tested at the Core level. **Styling (per user):** primary "Reset everything" is the
  accented call-to-action (`DefaultButton = Primary`), Cancel is the standard secondary button — no red
  destructive styling, no warning glyph, matching the Windows dialog convention. Content is a plain
  wrapped `TextBlock`, and `ContentDialogMaxWidth` is trimmed to 460 (from the 548 default). The dialog
  is hosted in a popup off the `XamlRoot`, so it does **not** inherit the theme override the shell
  applies to the window's content root — its `RequestedTheme` is copied from
  `XamlRoot.Content.RequestedTheme` before `ShowAsync` so a Light/Dark choice is honoured.
- **Account row:** connected → account name + **Disconnect**; not connected → "Not connected" +
  accent **Reconnect**. Button visibility driven by `IsConnected` / `IsNotConnected` (x:Bind can't
  negate a bool in a path, so `IsNotConnected` is a real property — same pattern as onboarding's
  `RedirectUriNotCopied`). Disconnect preserves the stored Client ID; only Reset clears it. (No
  "Premium" sub-text or green check — dropped at the user's request as unnecessary, since every
  connectable account is already Premium.)
- **Verified facts:** WinUI `x:Bind` implicitly converts `bool`→`Visibility` (used throughout the
  codebase, no converter needed).
- **Deferred / known gaps:** reset/disconnect failures are logged (fire-and-forget commands) but not
  surfaced with an in-app `InfoBar` yet — settings are reset before the disconnect so the app is left
  in a coherent defaults-applied state regardless. `Reset()`'s `Persist` can throw `IOException`
  (same as `Update`); the `ResetCommand` catch logs it.
- **Manual/integration checks:** none run this session (no live Spotify). Logic covered by unit tests
  (`ResetServiceTests`, `SettingsServiceTests.Reset*`, `ShellRouterTests` disconnect/transient cases);
  `dotnet test` green (218 tests total), `Amplify.App` builds clean with warnings-as-errors.
