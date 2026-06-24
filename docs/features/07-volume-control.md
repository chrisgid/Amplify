# Feature 07 — Volume Control

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [03 auth](./03-spotify-authentication.md), [06 hotkeys](./06-global-hotkeys.md) ·
> Required by: 09.
> **Follow [`../specification.md` §6 (Spotify Web API client standards)](../specification.md#6-spotify-web-api-client-standards).**

## Summary

Changes the volume of the user's active Spotify device via the **Spotify Web API**, and renders
the live volume UI on the Main screen: a meter (%), a slider, and ± buttons. Volume changes are
driven both by the global hotkeys ([06](./06-global-hotkeys.md)) and by direct interaction with
the slider/buttons. Each hotkey press changes volume by the configurable **step size**.

> *Phase 0 note:* the [walking skeleton](../getting-started.md#8-build-order) needs only one
> `GET /v1/me/player` read and one `PUT .../volume` wired to a button (then a single hard-coded
> `RegisterHotKey`); defer the full meter UI, optimistic/coalescing logic, and gating to Phase 1.

## User stories

- As a user, I want my up/down hotkeys to raise/lower Spotify's volume by my chosen step.
- As a user, I want to see the current volume and drag a slider or tap ± to adjust it.
- As a user, I want changes to apply to whatever device I'm currently playing on.

## UX / behaviour

*Reference:* `design/project/components/mainapp.jsx` (live volume card),
`components/settings.jsx` (step size).

- A "Now controlling" card shows the active device name and the current volume as a large `%`.
- A `Slider` (0–100) reflects and sets volume; ± icon buttons nudge by the **step size**.
- Hotkeys nudge by step and the meter updates; a brief flash/animation indicates direction
  (optional; keep subtle and native).
- **Step size** is 1–25% (default 5%), configured in Settings
  ([10](./10-settings-persistence.md)).
- The card is **disabled/dimmed** when not connected or when there is no active device — i.e.
  whenever `IVolumeController.CanControl` is false.
- Volume **0 = muted** (reflect with the muted speaker glyph).
- **Active device only:** Amplify always targets Spotify's **current active device**. There is
  **no device picker** — the user selects/switches devices in Spotify itself; Amplify just follows
  whatever is active.

## Acceptance criteria

- [ ] Up/down hotkeys change Spotify volume by the step size, clamped to 0–100.
- [ ] Step size defaults to **5%** when not configured, and is adjustable 1–25% in Settings.
- [ ] Slider and ± buttons change volume and stay in sync with hotkey changes.
- [ ] Volume is applied to the active device via
      `PUT /v1/me/player/volume?volume_percent={n}` (confirm shape via OpenAPI).
- [ ] Current volume is read from `GET /v1/me/player` on load and kept in sync.
- [ ] When disconnected or with no active device, the control is disabled with clear messaging (the
      no-active-device notice is owned by [feature 05](./05-connection-status.md)).
- [ ] 429/5xx responses are handled with backoff and don't desync the UI.

## Implementation guidance

- **Service:** `ISpotifyClient` — a **typed `HttpClient`** (registered via `IHttpClientFactory`,
  **no third-party SDK**) that uses `IAuthService.GetAccessTokenAsync()` for the bearer token.
  Canonical signature in [`../contracts.md`](../contracts.md):
  `Task<PlayerState?> GetPlayerStateAsync()` and `Task SetVolumeAsync(int percent)` (always the
  active device — no device id).
- **Controller:** `IVolumeController` owns the step math and orchestration:
  `Task NudgeAsync(int direction)` → `clamp(current + direction*step, 0, 100)` → `SetVolumeAsync`.
  Subscribes to `IHotkeyService.HotkeyPressed`.
- **Optimistic UI:** update the slider/meter immediately, then call the API; on failure, revert
  to the last known good value and surface an error. Debounce/coalesce rapid hotkey presses into
  the latest target to avoid flooding the API (helps with rate limits).
- **State sync:** poll `GET /v1/me/player` sparingly (e.g. on focus/connect) rather than
  continuously; respect ToS (no excessive caching/polling).
- **UI:** native `Slider`, icon `Button`s with `FontIcon` (plus/minus, speaker levels). No custom
  controls. Bind to `VolumeViewModel.Volume`, `IsEnabled`, `DeviceName`.
- **Rate limits:** on 429 use exponential backoff honouring `Retry-After`; never tight-loop.

## Data & persistence

- Step size persisted via [10 settings](./10-settings-persistence.md). Current volume/device are
  live (not persisted).

## Edge cases & error handling

- **No active device** → disable control, reading **`PlayerState.HasActiveDevice`** from
  `ISpotifyClient` (the single source of this signal; see [`../contracts.md`](../contracts.md)).
  The user-facing messaging for it is owned by [feature 05](./05-connection-status.md); this
  feature only gates the control.
- **Deriving the signal from the API:** `GET /v1/me/player` returns **`204 No Content`** when no
  device is active — map that (empty body) to `HasActiveDevice == false`, **not** an error. A
  `SetVolumeAsync` that returns **`404` ("Device not found")** or **`403`** likewise means no
  controllable device → revert the optimistic value and surface the no-device guidance (feature 05)
  rather than a generic failure.
- **Volume call rejected (`403`)** → a restriction means the device isn't controllable right now:
  revert the optimistic value and surface the same no-device/can't-control guidance
  ([feature 05](./05-connection-status.md)). There is **no** Premium pre-check (the API no longer
  reports subscription level, and Premium is enforced upstream) — the `403`/`404` reactive path is
  the whole story.
- **Rapid presses** → coalesce to the latest target; clamp at bounds (no wrap-around).
- **API failure** → revert optimistic value; show error; keep hotkeys responsive.
- **External change** (volume changed in Spotify) → reconcile on next read.

## Dependencies

- Requires a connected account ([03](./03-spotify-authentication.md)); **volume control
  additionally requires an active device** ([05](./05-connection-status.md)). Reacts to
  [06 hotkeys](./06-global-hotkeys.md); step size from [10](./10-settings-persistence.md); emits
  events used by [09 notifications](./09-notifications.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Step math: `NudgeAsync` clamps at 0 and 100; respects configured step; direction sign correct.
- Coalescing/debounce keeps only the latest target under rapid input.
- Optimistic update reverts on API failure (mock `ISpotifyClient`).
- `CanControl` gating: false when disconnected or when no active device; hotkey nudges are no-ops
  while `CanControl` is false.
- 429 handling backs off and honours `Retry-After`.
- Muted state at volume 0.

## Out of scope

- The hotkey capture/registration (feature 06) and toast display (feature 09).
- **Device selection / a device picker.** Amplify only ever controls the active device; choosing
  or transferring playback between devices is done in Spotify, not here.

## Standards reminder

OpenAPI as source of truth for the volume/player endpoints; minimum scopes; 429 backoff; native
`Slider`/`Button` + Fluent icons; concise code; comply with Spotify ToS.
