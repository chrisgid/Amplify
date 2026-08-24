# Build notes — Phase 2: Integration & Smoke Test

> Append a new dated entry each time the Phase 2 pass is run. Add to the end; don't rewrite earlier
> entries. See [`integration-smoke-test.md`](../integration-smoke-test.md) for the checklist itself.

## 2026-08-24 — First full Phase 2 pass · HEAD `2361ab4`

**Environment:** packaged (MSIX) dev-mode run, x64 Debug, launched via `dotnet run` (AUMID launch).
Real Spotify Premium account. Clean slate: `settings.json` deleted and the `Amplify`/`refresh_token`
Web Credential removed before starting, so the onboarding half ran as a genuine first run.

**Result: 17 of 18 checks pass; 1 fails.** Two checklist defects found and fixed; two app bugs found
and recorded (both spun off as follow-up work).

### Assembly — pass

- All 11 feature `AddXxx()` registrations are called from `App.BuildHost()`, plus `ShellViewModel`
  and `MainWindow`. No missing-service resolution errors across many launches.
- Six `IStartupInitializer`s, all registered, all inside the documented bands in
  [`contracts.md`](../contracts.md): Theme 100 → Tray 200 → PlayerState 250 → Hotkeys 400 →
  VolumeController 900 → Notifications 900.
- Packaged identity registers and launches; `LocalFolder\logs\` is written.
- **No tokens/PII in logs** — scanned every log for `bearer`, `access_token`, `refresh_token`,
  `code_verifier`, `client_secret`, email patterns and the Client ID: zero hits. The only long
  opaque strings are .NET type names in stack traces.

### End-to-end journey — 13 of 14 pass

Everything in [spec §3](../specification.md#3-primary-user-journey) walked on a real machine.
Non-obvious corroboration worth keeping:

- **Onboarding disables the OS startup entry.** `AmplifyStartupTask` went `State: 2 → 0` within one
  second of the clean-slate launch (`LastDisabledTime` 22:13:21), and again on Reset (23:31:31).
  `ShouldDisableForOnboarding` fires on the route change — Reset retracts the real OS registration,
  not just the stored preference. Nothing on screen reveals this; check the registry.
- **Automatic vs manual start (item 14) is checkable objectively.** After a reboot at 23:06:13 the
  app started at 23:07:09 with **`MainWindowHandle: 0`** — no window created at all. A
  shown-then-minimised window still reports a non-zero HWND, so this distinguishes "started hidden"
  from "started and minimised" without relying on what the screen looked like.
- **Single instance (item 9):** after a second launch, exactly one process survived, with the *same*
  PID and continuous uptime — the incumbent surfaced and the second instance exited leaving no
  zombie.
- **Hotkey re-registration (item 7)** is provable from the log's *silence*: `HotkeyRegistrar` logs a
  Warning via `LogRegistrationFailed` on any failure, so an empty log after a restart means both
  combos were claimed cleanly.
- **`trayHintShown` is the item-10 mechanism.** It flipped `false → true` on the first hide, survived
  a restart (so the balloon stays spent), and went back to `false` on Reset (so the next onboarding
  re-arms it). It also stayed `false` through onboarding minimises, confirming a normal taskbar
  minimise doesn't burn the one-shot.
- **Theme override (item 11)** was verified against a *disagreeing* OS: `themeMode: "Dark"` while
  Windows reported `AppsUseLightTheme: 1`. That rules out an override that only looks right because
  it happens to match the system.

### Resilience — 2 of 3 pass, 1 fail

- **Uncontrollable device (pass, real hardware).** Tested with a mobile phone, which Spotify reports
  as `supports_volume: false`. Card disabled from the first read with no flicker; device line read
  `{Device} · volume control not supported`. Key corroboration: this run's log contains **zero**
  `DeviceNotControllableException` entries, where the 2026-07-24 log has three — the app now gates on
  the flag *before* attempting the write instead of writing and handling the rejection. The absence
  of the exception is the fix working.
- **Offline — FAIL.** See bug 2 below.
- **Theme change while hidden (pass).** Windows flipped to dark while minimised to the tray; applied
  correctly when next shown.

### Bugs found (recorded, not fixed in this pass)

**1. Caption buttons ignore the app theme.** With the app in light theme the minimise/maximise/close
glyphs stay white and the hover background stays black. `MainWindow.ApplyTheme()` only sets
`root.RequestedTheme`; per MS docs the system caption buttons are **not part of the `TitleBar`
control** and are not in the XAML tree — they are drawn by `AppWindowTitleBar` and must be coloured
via its `Button*Color` properties on every `ThemeChanged`. The in-code comment claiming the root
"carries the … title bar along" is correct for the `TitleBar` control's title/icon and wrong for the
caption buttons. Item 11 is therefore **pass with defect**: system-follow, override and persistence
all work; only the caption button colours do not.

**2. Spotify-unreachable is never surfaced.** With the network pulled, the app does not crash and
logs a warning every poll — but the UI never changes, leaving a stale "Connected" card asserting a
device and volume that may be minutes old. `StatusPresentation.IsError` is
`State == ConnectionState.Error`, and `ConnectionState.Error` is set in exactly six places, all in
`SpotifyAuthService` (auth/token failures). Nothing in the player-state path sets it;
`PlayerStateProvider.RefreshAsync` logs and returns *before* `Publish(...)`, so consumers observe no
change, and there is no consecutive-failure tracking anywhere. The responsibility fell in a gap:
`PlayerStateProvider`'s catch comment says "the status card owns surfacing persistent failures", but
the status card cannot observe read failures — while
[`ConnectionState.cs:17`](../../src/Amplify.Core/Auth/ConnectionState.cs) already documents `Error`
as "Token/refresh failed **or Spotify unreachable**". The unreachable half was never wired up.

> **Design constraint for the fix (user-directed).** Today's silent retry has a real virtue: nothing
> latches, polling continues, and the app heals itself the instant the network returns with no user
> action. Measured during this pass — 21 warnings over ~100s (~20 failed polls), then full recovery
> with no restart and no interaction. The fix must **add honesty, not a gate**: keep polling,
> self-clear on the next successful read, and treat Reconnect as an optional affordance rather than
> the required path back. The naive "enter error state, stop polling, wait for the click" would be a
> regression on current behaviour.

### Checklist defects found and fixed in this pass

- **Item 7's cross-app conflict clause was stale.** It required "a combo already owned by another app
  is rejected with the prior binding kept" — impossible by design. Hotkeys use a `WH_KEYBOARD_LL`
  hook, which observes keys without consuming them, so there is no OS-level ownership to collide
  with; registration only fails if the hook cannot be installed. This was a deliberate, user-directed
  swap from an earlier `RegisterHotKey` draft, already documented in
  [feature 06](../features/06-global-hotkeys.md#edge-cases--error-handling) and its
  [build note](./06-global-hotkeys.md) — the smoke test was simply never updated. Rewritten to test
  the **duplicate guard** instead (binding both actions to the same combo), which is the one conflict
  rule that still applies, and which passed: `Hotkey_Conflict_Duplicate` — "That combination is
  already used by the other shortcut." — with the prior binding kept.
- **Item 12 mis-quoted a UI string** as "Reset Amplify to change it". The real resource
  (`Resources.resw:181`) is "Disconnect your account or reset Amplify to change it". Corrected so a
  future tester does not log a false failure.

### Exit criteria

- [x] Full journey passes on a clean profile — **except** the offline check (bug 2).
- [x] No unhandled exceptions in the logs. This run's log has **0 ERROR / 0 CRITICAL**; all 21
      warnings are from the deliberate network-outage test.
- [x] `dotnet test` green — **254 passed, 0 failed, 0 skipped**. `dotnet build` clean, 0 warnings
      under `TreatWarningsAsErrors`.

**Verdict:** not yet ready for [release (feature 14)](../features/14-release.md). Bug 2 fails a
documented resilience criterion and should be fixed and re-checked; bug 1 is cosmetic but visible on
every light-theme launch. Everything else holds.

### Verified facts (worth not re-deriving)

- System caption buttons are **not** part of the `TitleBar` control and are unaffected by
  `RequestedTheme` on the content root — they belong to `AppWindowTitleBar` and are coloured via its
  `Button*Color` properties. Resource-based theming (`WindowCaptionBackground`) is deprecated and has
  no effect.
- `Process.MainWindowHandle == 0` cleanly distinguishes "launched hidden to tray" from "launched and
  minimised" — useful for any future start-minimized check.
- The packaged startup entry lives under
  `HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\<PFN>\AmplifyStartupTask`.
  `State` is `StartupTaskState` (0 Disabled, 1 DisabledByUser, 2 Enabled, 3/4 policy), and
  `LastDisabledTime` is a Unix-seconds high-water mark of the last disable.
- `PasswordVault` credentials do **not** appear in `cmdkey /list` — they live under Credential
  Manager → **Web Credentials** (resource `Amplify`, username `refresh_token`).
