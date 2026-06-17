# Feature 09 — Notifications

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [07 volume control](./07-volume-control.md), [10 settings](./10-settings-persistence.md).

## Summary

Optional, lightweight feedback when a volume shortcut fires. When **"Notify on volume change"**
is enabled, Amplify shows a brief Windows toast (or equivalent transient indicator) with the new
volume level so the user gets confirmation without opening the window.

## User stories

- As a user, I want a quick confirmation that my hotkey worked and what the volume is now.
- As a user, I want to turn these notifications off if I find them noisy.

## UX / behaviour

*Reference:* `design/project/components/settings.jsx`
("Notify on volume change — Show a brief toast when a shortcut fires").

- Setting is **off by default** (matches the prototype's default).
- When enabled, each hotkey-driven volume change shows a short toast indicating direction and the
  new level (e.g. "Volume 65%"). Rapid changes should **coalesce/replace** rather than stack.
- Slider/button changes within the app need no toast (the UI already shows the value); toasts are
  primarily for background hotkey use.

## Acceptance criteria

- [ ] A toast appears on hotkey volume change only when the setting is enabled.
- [ ] The toast shows the resulting volume (and ideally direction).
- [ ] Rapid successive changes update/replace a single toast instead of flooding the action
      centre.
- [ ] Disabling the setting suppresses toasts immediately.

## Implementation guidance

- Use **`AppNotificationManager`** (Windows App SDK) to build and show toasts; requires the
  app to be packaged (MSIX) with notification registration at startup. Verify the current API via
  the `microsoft-docs:winui3`/`microsoft-docs` skills.
- `INotificationService.ShowVolume(int percent, int direction)`; called by
  [feature 07](./07-volume-control.md) after a successful (or optimistic) hotkey change, gated by
  the setting.
- **Coalescing:** reuse a stable notification `Tag`/`Group` so a new toast replaces the previous
  one; optionally debounce so a burst yields one final toast.
- Keep content minimal and native (no custom imagery beyond the app icon).

## Data & persistence

- `NotifyOnVolumeChange` boolean via [10 settings](./10-settings-persistence.md).

## Edge cases & error handling

- Notifications disabled at the OS/Focus-Assist level → respect the OS; the feature simply
  doesn't appear, no errors.
- Notification registration failure → degrade gracefully (no toasts), log, don't crash.
- Avoid toasts for in-app slider/button changes to reduce noise.

## Dependencies

- Triggered by [07 volume control](./07-volume-control.md); gated by
  [10 settings](./10-settings-persistence.md); most useful alongside
  [08 background](./08-system-tray-background.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- `INotificationService` is invoked only when the setting is enabled (mock the service; verify
  feature 07 gating).
- Coalescing logic produces a single active toast under rapid changes.
- Content reflects the correct resulting percentage/direction.

## Out of scope

- The volume change itself (feature 07) and the settings UI control (feature 10).

## Standards reminder

Native Windows toasts via `AppNotificationManager`; off by default; concise code; verify the API
via Microsoft docs skills.
