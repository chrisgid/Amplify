# Feature 05 — Connection Status & Account

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [03 auth](./03-spotify-authentication.md) · Required by: 07.

## Summary

The status block at the top of the Main screen that communicates Amplify's live connection to
Spotify. It has three connection states — **connected**, **connecting**, and **error** — and
surfaces the signed-in account (name, active device) plus a reconnect affordance when needed.

Amplify is always **Premium** (see [03](./03-spotify-authentication.md) — Spotify enforces it on the
user's own developer app and the Web API no longer reports subscription level), so there is **no
Free variant**. The only "connected but can't control" case is **no active device**, plus the rare
reactive `403` on a volume call — both handled as needs-attention guidance, not a separate account
tier.

## User stories

- As a user, I want to see at a glance that Amplify is connected and which account/device it's
  controlling.
- As a user, when there's no active Spotify device, I want to understand that I need to start
  playback on a device before volume control works.
- As a user, when something breaks, I want a clear message and a one-click way to reconnect.

## UX / behaviour

*Reference:* `design/project/components/mainapp.jsx` (`StatusBlock`).

- **Connected:** a card with the account avatar/initials, name, a green success check, and the
  active device line "`{Device}`" (e.g. "DESKTOP-AX31"), plus a green "Connected" label.
- **Connected, no active device:** the same card, but the green check is replaced by a **yellow
  warning triangle** (`--warning`) and the device line reads a "No active device" hint. Below the
  card, a **warning `InfoBar`** — title "No active device", body "Open Spotify and start playback on
  a device, then it'll appear here." This is the same needs-attention treatment feature 07 gates the
  control on.
- **Connecting:** an info `InfoBar` (informational) — "Connecting to Spotify…
  Re-establishing the link to your account." with a spinner.
- **Error:** an error `InfoBar` — "Can't reach Spotify. Open Spotify on a device, then
  reconnect." with a **Reconnect** button.
- State is driven by `IAuthService`/`ISpotifyClient`: token validity, reachability, and whether
  there is an active device (`PlayerState.HasActiveDevice`).

## Acceptance criteria

- [ ] Reflects the real connection state and updates reactively when it changes.
- [ ] Connected shows account name and active device with the green check/label.
- [ ] Connected with no active device shows the warning triangle, warning-coloured "Connected"
      label, and the "No active device" warning `InfoBar`.
- [ ] Error state offers Reconnect, which re-runs the connect/refresh path.
- [ ] Connecting state is transient and resolves to connected or error.
- [ ] Avatar shows initials derived from the account name when no image is available.

## Implementation guidance

- A `StatusViewModel` exposes a `ConnectionState` enum and the `Account` model (name, initials). The
  **account comes from `IAuthService.CurrentAccount`** (auth already reads `GET /v1/me` during
  connect — do not call the profile endpoint again here); the **active device name/presence** comes
  from `ISpotifyClient.GetPlayerStateAsync()` (`GET /v1/me/player`). Confirm fields via the OpenAPI
  spec, §6.
- Use `InfoBar` (`Severity = Informational/Error`) for connecting/error, a `Severity = Warning`
  `InfoBar` for the no-active-device notice, and a native card layout (`Border`/`Grid` with theme
  brushes) for connected. Use `FontIcon` glyphs (check vs warning triangle); spinner = `ProgressRing`.
- Map service outcomes → state: valid token + reachable → **connected**; refresh in progress →
  **connecting**; failure / unreachable → **error**. Within **connected**,
  `PlayerState.HasActiveDevice` selects the full vs needs-attention presentation.
- **Reconnect** calls the auth/refresh path ([03](./03-spotify-authentication.md)); on success,
  return to connected.

## Data & persistence

- None persisted; reads live account/player info per session (do not cache beyond immediate use,
  per Spotify ToS).

## Edge cases & error handling

- No active device → treated as needs-attention with guidance to open Spotify on a device. The
  canonical signal is **`PlayerState.HasActiveDevice`** from `ISpotifyClient` (see
  [`../contracts.md`](../contracts.md)). **This feature owns the user-facing *messaging*** for it;
  [feature 07](./07-volume-control.md) owns *gating the control*. Both read the same field — neither
  re-implements the check.
- A volume call rejected with `403` (a restriction) → surfaced with the same "can't control"
  guidance; there is no Premium pre-check because the API no longer reports subscription level.
- Token refresh failure → error state with Reconnect.
- Account has no display name → fall back to a sensible label; initials from available text.

## Dependencies

- Consumes [feature 03](./03-spotify-authentication.md). Shares the "needs active device" concern
  with [feature 07](./07-volume-control.md). Settings shows a compact mirror of this state in
  [feature 12](./12-reset-and-account.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- State mapping: token/reachability combinations → correct `ConnectionState`.
- Connected presentation: `HasActiveDevice == false` while connected → warning visuals and the
  "No active device" `InfoBar`; `true` → green check, no notice.
- Initials derivation from various name formats.
- Reconnect command invokes the refresh/connect path and transitions on success/failure.

## Out of scope

- The OAuth flow itself (feature 03) and the volume UI (feature 07).

## Standards reminder

Native `InfoBar`/card + Fluent icons; reactive MVVM; follow Windows theme; OpenAPI as source of
truth for account/player fields.
