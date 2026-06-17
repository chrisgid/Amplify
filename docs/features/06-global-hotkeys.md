# Feature 06 — Global Hotkeys

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md), [10 settings](./10-settings-persistence.md) ·
> Required by: 07.

## Summary

The core capability: two **global** keyboard shortcuts — **Volume up** and **Volume down** —
that work system-wide, even when Amplify is minimised or in the tray. Includes the in-app UI for
**recording/rebinding** each shortcut, persistence of the chosen combinations, and registration
with Windows.

## User stories

- As a user, I want to change Spotify's volume from any app using a keyboard shortcut.
- As a user, I want to choose my own key combinations by pressing them.
- As a user, I want my shortcuts to keep working when Amplify is in the background.
- As a user, I want to know if a combination is unavailable (already taken).

## UX / behaviour

*Reference:* `design/project/components/mainapp.jsx` (`HotkeyRow`, `comboFromEvent`).

- Two rows on the Main screen: **Volume up** and **Volume down**, each showing its current
  combination as keycaps and an edit (rebind) button. Defaults: **`Ctrl+Alt+↑`** and
  **`Ctrl+Alt+↓`**.
- **Recording:** clicking edit puts the row into "listening" — helper text
  "Press any key combination · Esc to cancel". The next combination with at least one modifier +
  a non-modifier key is captured and saved; **Esc** cancels.
- Sub-text per row reflects the step: "Raises/Lowers Spotify volume by {step}%".
- Footer hint when idle: "Shortcuts work globally, even when Amplify is in the background."

## Acceptance criteria

- [ ] Default bindings are `Ctrl+Alt+↑` (up) and `Ctrl+Alt+↓` (down).
- [ ] Each shortcut can be re-recorded by pressing a new combination; Esc cancels with no change.
- [ ] Captured combos require ≥1 modifier and a single non-modifier key.
- [ ] Bindings persist across restarts and re-register on launch.
- [ ] Shortcuts fire globally regardless of focus / when minimised to tray.
- [ ] Pressing a bound shortcut invokes the corresponding volume change
      ([feature 07](./07-volume-control.md)).
- [ ] If a combination can't be registered (already in use), the user is told and the previous
      binding is kept.
- [ ] The two shortcuts can't be bound to the same combination.

## Implementation guidance

- **Registration:** P/Invoke `RegisterHotKey`/`UnregisterHotKey` with a message-only window to
  receive `WM_HOTKEY`. This is the recommended approach for true global hotkeys. Where a desired
  combination can't be expressed via `RegisterHotKey`, fall back to a low-level keyboard hook
  (`SetWindowsHookEx` `WH_KEYBOARD_LL`). Verify modifier flags (`MOD_CONTROL`, `MOD_ALT`,
  `MOD_SHIFT`, `MOD_WIN`, `MOD_NOREPEAT`) and virtual-key mapping with the Microsoft docs skills.
- **Service:** `IHotkeyService` with `Register(HotkeyAction action, Hotkey combo)`,
  `Unregister(...)`, and an event `HotkeyPressed(HotkeyAction)`. A `Hotkey` model holds modifiers
  + key and can format to display keycaps and parse from a captured key event.
- **Recording UI:** `HotkeyEditorViewModel` listens for key events while in recording mode,
  ignores modifier-only presses, builds a `Hotkey`, validates (modifier required, not duplicate),
  then persists + re-registers. Esc aborts.
- **Persistence:** store both combos via `ISettingsService`
  ([10](./10-settings-persistence.md)) as a stable string form (e.g. `ctrl+alt+arrowup`).
- **Lifecycle:** register on app start (after settings load); unregister on exit; re-register on
  change. Keep registration independent of which screen is visible.

## Data & persistence

- Two `Hotkey` values (up/down) in settings, serialised as canonical strings; loaded at startup.

## Edge cases & error handling

- **Conflict:** `RegisterHotKey` returns failure when the combo is owned by another app → notify
  the user and retain the prior binding.
- **Duplicate:** prevent binding both actions to the same combo.
- **Modifier-only / invalid:** ignore until a valid combo is pressed.
- **Re-entrancy/repeat:** use `MOD_NOREPEAT` (or debounce) so holding the keys doesn't spam.
- Registration should not throw on transient failures; surface and continue.

## Dependencies

- Persists via [10 settings](./10-settings-persistence.md). Fires actions consumed by
  [07 volume control](./07-volume-control.md). Continues working with the window hidden via
  [08 tray/background](./08-system-tray-background.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- `Hotkey` parsing/formatting round-trips (string ↔ model ↔ keycaps).
- Capture logic: rejects modifier-only, accepts modifier+key, rejects duplicates.
- Default bindings applied when none are stored.
- Registration failure keeps the previous binding (mock `IHotkeyService`).
- Pressed event maps to the correct volume action.

## Out of scope

- Performing the volume change (feature 07) and toasts (feature 09).

## Standards reminder

Native controls for the rows + Fluent icons; concise P/Invoke; verify Win32 hotkey APIs via
Microsoft docs skills; persist through `ISettingsService`.
