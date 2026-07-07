# Build notes — Feature 09: First-Run Tray Hint

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-07-07 — Phase 1 (full build) · feat/09-notifications

- **Settings rename landed here:** `AppSettings.NotifyOnVolumeChange` → `TrayHintShown` (bool, default
  `false`), the rename feature 08's balloon entry deferred to "feature 10/09". It's internal one-shot
  state, so the user-facing "Notify on volume change" Settings card was removed entirely — the XAML
  `SettingsCard`, the `SettingsViewModel.NotifyOnVolumeChange` property + persister + load line, and the
  `Settings_Notify.*` resw strings all went with it. No migrator: the field is additive/optional, so an
  old file carrying `NotifyOnVolumeChange` is simply ignored on load (default-merge), per the feature doc.
- **`NotificationService` lives in `Amplify.Core`, not `Amplify.App`.** It has no WinUI/platform
  dependency (only `ITrayService`, `ISettingsService`, `ILogger`), and the test project references Core
  only — putting it in Core is what makes the required show-once/retry unit tests possible. This mirrors
  `VolumeController` (a Core service that also implements `IStartupInitializer`). The DI extension
  `AddNotifications()` stays in the App layer.
- **Balloon copy is injected, not hard-coded in Core.** Added a small `TrayHintCopy(Title, Message)`
  record in Core; `AddNotifications()` builds it from `ResourceLoader` (new resw keys
  `Notification_TrayHint_Title` / `Notification_TrayHint_Message`). This keeps user-facing text in the
  shared `.resw` (spec §5) while leaving the policy UI-free and testable. Tests pass a fixed copy.
- **Balloon wording deviates from the doc's suggested copy** (title "Amplify is still running" / message
  "Find it in the system tray — your volume hotkeys keep working."). Per user request the shipped copy is
  title **"Amplify is minimized to the system tray"** / message **"Your volume shortcuts keep working.
  This is a one-time notification that won't be shown again."** — the same intent (where the window went +
  hotkeys still work), plus an explicit reassurance that the hint is one-time so the user isn't left
  expecting recurring notifications.
- **Contract changes:** none to `contracts.md` — `INotificationService.ShowFirstMinimizeHintIfNeeded()`
  and `ITrayService.HiddenToTray`/`ShowTrayNotification` were already defined there (08 supplied the tray
  side). `TrayHintCopy` is a feature-local Core helper, deliberately not added to `contracts.md` (it
  doesn't cross feature boundaries).
- **Policy / wiring:** the service subscribes to `ITrayService.HiddenToTray` in `OnLaunchedAsync`
  (`IStartupInitializer` band **900** — after tray/window at 200). On each hide it runs the guard: if
  `TrayHintShown` is false, call `ShowTrayNotification`; **the flag is persisted only after a non-throwing
  call**, so a first hide with OS notifications suppressed / a transient balloon failure still lets the
  hint appear on a later hide. A throwing `ShowTrayNotification` is caught broadly (best-effort OS
  interaction, failure modes not enumerable — same rationale as `TrayService`'s icon-creation catch),
  logged, and swallowed so a hide-to-tray never crashes.
- **Deferred / known gaps:** the balloon actually rendering (and the click-to-restore nice-to-have, which
  is **not** implemented — 08's `ShowTrayNotification` shows an info balloon with no click handler) is
  window/OS-bound and unit-tested only at the policy level. The end-to-end check (first minimise **and**
  first close-to-tray each show the balloon once; nothing on restart; reset re-arms it) belongs to the
  Phase 2 packaged smoke test and has **not** been run in this session.
- **Manual/integration checks:** `dotnet build` (Core + packaged App) clean under
  `TreatWarningsAsErrors`; `dotnet test` green (208, +5 `NotificationServiceTests`, existing
  `SettingsServiceTests` updated for the rename); `dotnet format` clean.
- **Verified facts:** feature 08 already ships both seams this feature needs — `HiddenToTray` is raised
  from the single `HideToTray()` funnel (covers minimise- and close-to-tray). The "tray unavailable →
  no balloon, flag stays unset" edge case is handled **because `HideToTray()` early-returns without
  raising `HiddenToTray` when the icon is absent** (`TrayService.cs`), so this feature's handler never
  runs and never reaches the persist. It is *not* handled by `ShowTrayNotification` no-op'ing — that
  returns without throwing, which would flow through to `Update` and (wrongly) set the flag. So the guard
  that keeps the flag unset lives on 08's side; don't move the "leave the flag unset" safety onto a
  no-op/throw distinction in `ShowTrayNotification`. (`ShowTrayNotification` does still no-op when the
  icon is absent, but that path isn't reached here.) Confirmed against
  [`08-system-tray-background.md`](./08-system-tray-background.md) (2026-07-06 entry) and `TrayService.HideToTray()`.
