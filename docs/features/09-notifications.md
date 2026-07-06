# Feature 09 — First-Run Tray Hint

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [08 system tray](./08-system-tray-background.md), [10 settings](./10-settings-persistence.md).

## Summary

A single, one-time notification that helps the user find the app after it first disappears into the
system tray. **The first time** Amplify hides to the tray (minimise **or** close-to-tray), it shows
a brief balloon — anchored to the tray icon — explaining where the window went and that hotkeys keep
working. After it has been shown once, it **never appears again** (a flag is persisted). There are
no other notifications and no user-facing toggle.

> Supersedes the earlier "toast on every volume change" design, which was dropped as unnecessary
> noise (the in-app meter already confirms hotkey changes). The `NotifyOnVolumeChange` setting and
> `INotificationService.ShowVolume` are removed with it.

## User stories

- As a first-time user, when the window vanishes after I minimise it, I want to understand that
  Amplify is still running and where to find it — so I don't think it crashed or quit.
- As a returning user, I don't want to be nagged by that hint again once I've seen it.

## UX / behaviour

- The hint fires on the **first** hide-to-tray of the app's lifetime (across restarts), whether
  triggered by minimising or by close-to-tray — both hide the window ([08](./08-system-tray-background.md)).
- It is shown as a **tray balloon** via the existing tray icon (`TaskbarIcon`), so it visually
  points at where the app now lives. Content is minimal and native (app icon + short text):
  - **Title:** "Amplify is still running"
  - **Message:** "Find it in the system tray — your volume hotkeys keep working."
- Clicking the balloon may restore the window (nice-to-have, reusing `ITrayService.ShowWindow()`);
  it is not required.
- Once shown, `TrayHintShown` is set to `true` and the hint is **never shown again**.
- The tray persona only exists once connected ([08](./08-system-tray-background.md) suppresses the
  tray while onboarding), so the hint naturally cannot fire before the user has connected.

## Acceptance criteria

- [ ] The first time the window hides to the tray, the balloon appears once with the correct copy.
- [ ] It fires for **either** trigger (minimise-to-tray or close-to-tray).
- [ ] After being shown once, it does **not** appear again — including across app restarts
      (`TrayHintShown` is persisted).
- [ ] No notification appears on volume changes, and there is no "notify" toggle in Settings.
- [ ] If the OS suppresses notifications (Focus Assist / disabled), the app does not error; the
      flag is still treated as "shown" is **not** assumed — see edge cases.

## Implementation guidance

- **Trigger:** subscribe to **`ITrayService.HiddenToTray`** ([08](./08-system-tray-background.md),
  [`../contracts.md`](../contracts.md)). Feature 08 owns detecting the hide and raising the event;
  this feature owns the *policy* (show once) and the *copy*.
- **Service:** `INotificationService.ShowFirstMinimizeHintIfNeeded()` — on the first `HiddenToTray`,
  read `AppSettings.TrayHintShown`; if false, call **`ITrayService.ShowTrayNotification(title,
  message)`** (which shows the balloon through the tray `TaskbarIcon`), then persist
  `TrayHintShown = true` via [`ISettingsService`](./10-settings-persistence.md). If already true,
  no-op. Wire the subscription via an **`IStartupInitializer`** (band 900 — after tray/window at
  200 so the tray exists).
- **Balloon primitive:** shown by feature 08 through **H.NotifyIcon.WinUI** (`TaskbarIcon`), which
  Amplify already depends on for the tray icon — **no `AppNotificationManager`, MSIX toast
  registration, or COM activator is needed**. Confirm the current `TaskbarIcon` notification API via
  the `microsoft-docs:winui3` skill when building 08's `ShowTrayNotification`.
- Keep this feature thin: it is one subscription, one guarded call, and one settings write.

## Data & persistence

- `AppSettings.TrayHintShown` (bool, default `false`) via [10 settings](./10-settings-persistence.md).
  It is **internal one-shot state, not a user-facing setting** — it appears nowhere in the Settings
  UI. Being an additive optional field, it needs no migrator (default-merge covers it).

## Edge cases & error handling

- **OS notifications disabled / Focus Assist:** respect the OS — the balloon simply may not appear.
  Persist `TrayHintShown` only after a successful show attempt so a user who had notifications off
  the first time can still get the hint later; if the show call throws, log and leave the flag
  unset to retry next hide. (Prefer a best-effort single retry over nagging.)
- **Tray icon unavailable** ([08](./08-system-tray-background.md) edge case): no balloon; app still
  runs; do not set the flag.
- **Reset** ([12](./12-reset-and-account.md)) returns settings to defaults, which clears
  `TrayHintShown` — so after a full reset the hint can legitimately show again on first minimise.

## Dependencies

- Triggered by [08 system tray](./08-system-tray-background.md) (owns `HiddenToTray` +
  `ShowTrayNotification`); persists its one flag via [10 settings](./10-settings-persistence.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- First `HiddenToTray` with `TrayHintShown == false` → calls `ShowTrayNotification` once and sets
  the flag (mock `ITrayService` + `ISettingsService`).
- Subsequent `HiddenToTray` events (and a fresh run with `TrayHintShown == true`) → no call.
- A failed/throwing `ShowTrayNotification` → the flag stays unset (retry preserved), no crash.

## Out of scope

- Detecting the hide and raising the event / owning the tray icon (feature 08).
- Any per-volume-change or ongoing notifications — deliberately removed.

## Standards reminder

Native tray balloon via the existing `TaskbarIcon` (no `AppNotificationManager`); one-shot and
silent thereafter; internal flag only (no Settings UI); concise code; verify the `TaskbarIcon`
notification API via the Microsoft docs skills.
