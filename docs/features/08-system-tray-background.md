# Feature 08 — System Tray & Background Operation

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md), [10 settings](./10-settings-persistence.md).

## Summary

Lets Amplify run quietly in the background. Adds a **system tray (notification area) icon** with
a context menu, supports **minimise/close to tray**, **start minimised**, **launch at startup**,
and enforces **single-instance** behaviour so the hotkeys keep working without a visible window.

## User stories

- As a user, I want Amplify to stay running in the tray so my hotkeys always work.
- As a user, I want it to start with Windows and optionally start hidden.
- As a user, I want closing the window to keep it running, not quit it.
- As a user, I want a tray menu to reopen the window or fully quit.

## UX / behaviour

*Reference:* `design/project/components/settings.jsx` ("Launch at startup",
"Start minimized to the tray").

- A tray icon (Amplify logo) is present whenever the app runs.
- **Tray menu:** Open Amplify, (optionally) Connected status line, Settings, Quit.
- **Minimise to tray (not the taskbar):** minimising the window hides it to the **system tray**
  rather than collapsing to a Windows **taskbar** button. While hidden, Amplify should not show a
  taskbar button — its only presence is the tray icon. Restoring from the tray brings the window
  back.
- **Close to tray:** closing the window also hides it to the tray instead of exiting (app keeps
  running + hotkeys active). Quit (menu) exits fully.
- **Start minimized to the tray:** when enabled, launch with no window shown.
- **Launch at startup:** when enabled, Amplify starts on Windows sign-in. **Off by default** and only
  registered once the user opts in from Settings — which is unreachable until onboarding is complete —
  so a fresh install, a reset, or a disconnect never launches at sign-in while there is no account set
  up. The OS entry is held **off for as long as the app is in the onboarding (not-connected) state**;
  on a disconnect the stored preference is preserved and silently re-applied when the user reconnects,
  while a reset clears it back to off.

## Acceptance criteria

- [ ] A tray icon with a working context menu (Open, Settings, Quit) appears while running.
- [ ] Double-clicking the tray icon (or Open) shows/focuses the window.
- [ ] Minimising the window hides it to the tray and **removes its taskbar button** (no taskbar
      presence while hidden); restoring from the tray shows the window again.
- [ ] Closing the window hides to tray and keeps hotkeys working; Quit exits the process.
- [ ] "Start minimized to the tray" launches without showing the window.
- [ ] "Launch at startup" registers/unregisters a startup entry that reflects the toggle.
- [ ] Only one instance runs; launching again surfaces the existing window.
- [ ] `HiddenToTray` is raised on both minimise-to-tray and close-to-tray; `ShowTrayNotification`
      displays a tray balloon (consumed by [feature 09](./09-notifications.md)).

## Implementation guidance

- **Tray icon:** use **H.NotifyIcon.WinUI** (`TaskbarIcon`) for the icon + `MenuFlyout` context
  menu. Bind menu commands to a `TrayViewModel`/`ITrayService`.
- **Minimise-to-tray:** detect the minimise transition (e.g. window state change /
  `OverlappedPresenter` state) and instead **hide** the window (`AppWindow.Hide()`), so it leaves
  the taskbar entirely rather than becoming a minimised taskbar button. Ensure no taskbar button
  remains while hidden — set `AppWindow.IsShownInSwitchers = false` when hidden (and restore it
  when shown) if any residual presence appears. Verify the exact presenter/minimise API via the
  `microsoft-docs:winui3` skill.
- **Close-to-tray:** intercept the window close (`AppWindow.Closing`) and hide instead of exit
  when the "keep running" behaviour applies; provide an explicit Quit that bypasses it.
- **Hide event + tray balloon (for [feature 09](./09-notifications.md)):** raise
  **`ITrayService.HiddenToTray`** whenever the window hides to the tray — from **both** the
  minimise-to-tray and close-to-tray paths above. Also implement
  **`ShowTrayNotification(title, message)`** to display a balloon through the tray `TaskbarIcon`
  (H.NotifyIcon.WinUI) — this reuses the icon you already own and needs **no** `AppNotificationManager`
  or COM activator. Feature 09 subscribes to the event and decides whether to show the one-time
  hint; this feature just provides the event and the balloon primitive. Confirm the current
  `TaskbarIcon` notification API via the `microsoft-docs:winui3` skill.
- **Start minimized:** read the setting at launch; if set, don't activate the main window.
- **Launch at startup:** packaged **`StartupTask`** (request/enable via
  `StartupTask.GetAsync` + `RequestEnableAsync`). Reflect the OS-controlled state back into the
  toggle (the user can disable it in Task Manager/Settings). Verify the API via the
  `microsoft-docs:winui3`/`microsoft-docs` skills.
  - **Manifest:** this requires a `windows.startupTask` extension (with a `TaskId`) declared in
    `Package.appxmanifest` — `StartupTask.GetAsync(taskId)` uses that same id. Add it as part of
    this feature; it is **not** handled by the release/packaging feature
    ([14](./14-release.md)). Ship it **`Enabled="false"`** so a fresh install doesn't launch at
    sign-in before onboarding; the app enables it from code once the user opts in
    (`RequestEnableAsync`/`Disable` are silent for a packaged desktop app).
  - **Onboarding gate:** the OS entry must stay **off while the app has no account** (the onboarding
    route). Force it off when the app enters onboarding — including on the return trip from a reset
    or disconnect — without rewriting the stored preference, and re-apply the stored preference when
    the user connects. This keeps the setting (default off, unreachable during onboarding) from ever
    producing a sign-in launch on a not-yet-set-up install.
- **Single instance:** use Windows App SDK single-instancing (`AppInstance.FindOrRegisterForKey`
  + redirect activation) so a second launch activates the first instance.
- **Lifetime:** the app's process lifetime is decoupled from window visibility; hotkeys
  ([06](./06-global-hotkeys.md)) remain registered while hidden.

## Data & persistence

- `LaunchAtStartup`, `StartMinimizedToTray`, and close-to-tray preference via
  [10 settings](./10-settings-persistence.md). Actual startup enablement is also reflected by the
  OS `StartupTask` state.

## Edge cases & error handling

- Startup task disabled by the user/OS policy → reflect the real state in the toggle and explain
  if it can't be enabled.
- Tray icon area unavailable → app still runs; window remains the access point.
- Quit must fully unregister hotkeys and dispose the tray icon.

## Dependencies

- Built on the shell ([01](./01-application-shell.md)); settings from
  [10](./10-settings-persistence.md); keeps [06 hotkeys](./06-global-hotkeys.md) alive while
  hidden; uses the logo asset from [13](./13-app-icon-branding.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Settings → behaviour mapping (start-minimized, close-to-tray, launch-at-startup intent).
- Startup toggle reflects the queried `StartupTask` state (mock the OS interface).
- Single-instance: second activation routes to the first instance (logic-level test where
  feasible; otherwise manual).

## Out of scope

- The **policy** for the first-run tray hint (feature 09) — this feature only raises `HiddenToTray`
  and provides `ShowTrayNotification`; feature 09 owns the once-only logic and copy. Theming of the
  window is feature 11.

## Standards reminder

Native tray/menu via the recommended library + logo icon; Fluent glyphs elsewhere; verify
`StartupTask`/single-instance APIs via Microsoft docs skills; concise code.
