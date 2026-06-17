# Feature 05 — Connection Status & Account

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [03 auth](./03-spotify-authentication.md) · Required by: 07.

## Summary

The status block at the top of the Main screen that communicates Amplify's live connection to
Spotify. It has three connection states — **connected**, **connecting**, and **error** — and the
connected state has two variants: **Premium** (full control) and **Free** (connected but volume
control unavailable). It surfaces the signed-in account (name, plan, active device) plus a
reconnect affordance when needed.

## User stories

- As a user, I want to see at a glance that Amplify is connected and which account/device it's
  controlling.
- As a free-account user, I want to understand why volume control isn't working and that
  upgrading to Premium fixes it.
- As a user, when something breaks, I want a clear message and a one-click way to reconnect.

## UX / behaviour

*Reference:* `design/project/components/mainapp.jsx` (`StatusBlock`).

- **Connected (Premium):** a card with the account avatar/initials, name, a green success check,
  and "`{Plan} · {Device}`" (e.g. "Premium · DESKTOP-AX31"), plus a green "Connected" label.
- **Connected (Free):** the same card, but for a non-Premium account (`Account.IsPremium == false`):
  the green check is replaced by a **yellow warning triangle** (`--warning`), the "Connected" label
  is **warning-coloured**, and the plan line reads "Free · {Device}". Below the card, a **warning
  `InfoBar`** — title "Volume control unavailable", body "You're on a free Spotify account. Upgrade
  to Premium to control playback volume."
- **Connecting:** an info `InfoBar` (informational) — "Connecting to Spotify…
  Re-establishing the link to your account." with a spinner.
- **Error:** an error `InfoBar` — "Can't reach Spotify. Open Spotify on a device, then
  reconnect." with a **Reconnect** button.
- State is driven by `IAuthService`/`ISpotifyClient`: token validity, reachability, whether there
  is an active device, and `Account.IsPremium` (Premium vs Free presentation).

## Acceptance criteria

- [ ] Reflects the real connection state and updates reactively when it changes.
- [ ] Connected (Premium) shows account name, plan, and active device with the green check/label.
- [ ] Connected (Free) shows the warning triangle, warning-coloured "Connected" label, a "Free"
      plan line, and the "Volume control unavailable" warning `InfoBar`.
- [ ] Error state offers Reconnect, which re-runs the connect/refresh path.
- [ ] Connecting state is transient and resolves to connected or error.
- [ ] Avatar shows initials derived from the account name when no image is available.

## Implementation guidance

- A `StatusViewModel` exposes a `ConnectionState` enum and an `Account` model (name, plan,
  `IsPremium`, device, initials) sourced from `GET /v1/me` and `GET /v1/me/player`
  (confirm fields via the OpenAPI spec, §6).
- Use `InfoBar` (`Severity = Informational/Error`) for connecting/error, a `Severity = Warning`
  `InfoBar` for the Free notice, and a native card layout (`Border`/`Grid` with theme brushes) for
  connected. Use `FontIcon` glyphs (check vs warning triangle); spinner = `ProgressRing`.
- Map service outcomes → state: valid token + reachable + active device → **connected**; refresh
  in progress → **connecting**; failure / no device reachable → **error**.
- Within **connected**, `Account.IsPremium` selects the variant: `true` → Premium (green check),
  `false` → **Free** (warning triangle + "Volume control unavailable" `InfoBar`). Premium-ness is
  not a separate `ConnectionState` (see [`../contracts.md`](../contracts.md)).
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
- Token refresh failure → error state with Reconnect.
- Account has no display name → fall back to a sensible label; initials from available text.

## Dependencies

- Consumes [feature 03](./03-spotify-authentication.md). Shares the "needs active device" concern
  with [feature 07](./07-volume-control.md). Settings shows a compact mirror of this state in
  [feature 12](./12-reset-and-account.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- State mapping: token/reachability/device combinations → correct `ConnectionState`.
- Premium vs Free presentation: `IsPremium == false` while connected → warning visuals, "Free"
  plan line, and the "Volume control unavailable" `InfoBar`; `true` → green check, no notice.
- Initials derivation from various name formats.
- Reconnect command invokes the refresh/connect path and transitions on success/failure.

## Out of scope

- The OAuth flow itself (feature 03) and the volume UI (feature 07).

## Standards reminder

Native `InfoBar`/card + Fluent icons; reactive MVVM; follow Windows theme; OpenAPI as source of
truth for account/player fields.
