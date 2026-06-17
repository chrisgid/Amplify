# Feature 12 — Reset & Account Management

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [03 auth](./03-spotify-authentication.md), [10 settings](./10-settings-persistence.md).

## Summary

The account and reset controls in Settings. Lets the user **disconnect** their Spotify account
(or **reconnect** when not connected) and perform a full **Reset Amplify** that clears shortcuts
and disconnects, returning the app to a first-run state. Includes the confirmation dialog for the
destructive reset.

## User stories

- As a user, I want to disconnect my Spotify account from Amplify.
- As a user, I want to reset everything back to defaults if things go wrong.
- As a user, I want a confirmation before a destructive reset so I don't do it by accident.

## UX / behaviour

*Reference:* `design/project/components/settings.jsx` (Account + Reset sections),
`components/app.jsx` (`doReset`, reset dialog).

- **Account row:** when connected on **Premium**, shows account name + "{Plan} · Spotify" with a
  green check and a **Disconnect** button. A **Free** account is also treated as connected — it
  shows the account name and a **Disconnect** button, but with the **warning icon** (not the green
  check) and sub-text **"Free · Volume control unavailable"**. When not connected, shows
  "Not connected" with a **Reconnect** button.
- **Spotify Client ID (read-only):** the stored Client ID is shown read-only with the note
  "From your Spotify Developer app. Reset Amplify to change it." (rendered by
  [feature 10](./10-settings-persistence.md)). It is captured at onboarding
  ([feature 04](./04-onboarding.md)) and the **only** way to change it is a full Reset.
- **Disconnect:** clears tokens and returns the app to the onboarding/not-connected state. It does
  **not** clear the stored Client ID, so Reconnect can reuse it without re-entry.
- **Reset Amplify (danger):** row with a "Reset…" button → opens a **`ContentDialog`** confirming:
  "This removes your keyboard shortcuts and Client ID, and disconnects your Spotify account. You'll
  need to set up Amplify again. This can't be undone." with **Reset everything** (destructive) and
  **Cancel**.
- **On confirm:** restore default hotkeys, default volume step, default settings, **clear the
  stored Client ID**, disconnect Spotify, and route back to onboarding (which re-prompts for a
  Client ID).

## Acceptance criteria

- [ ] Disconnect removes stored tokens ([feature 03](./03-spotify-authentication.md)) and moves
      the app to the not-connected state, **without** clearing the stored Client ID.
- [ ] The stored Client ID is shown read-only; it cannot be edited in place (only Reset changes it).
- [ ] A Free account is shown as connected (account name + Disconnect) with the warning icon and
      "Free · Volume control unavailable" sub-text.
- [ ] Reconnect (when disconnected) re-runs the connect flow, reusing the stored Client ID.
- [ ] Reset shows a confirmation dialog and only proceeds on explicit confirm.
- [ ] Reset restores default shortcuts (`Ctrl+Alt+↑/↓`), default step (5%), and default settings,
      **clears the stored Client ID**, and disconnects Spotify.
- [ ] After reset, the app behaves as first-run (routes to onboarding).
- [ ] Cancel makes no changes.

## Implementation guidance

- **Disconnect:** `IAuthService.DisconnectAsync()` (clears Credential Locker + in-memory tokens);
  raise state change so the shell routes to onboarding ([01](./01-application-shell.md)).
- **Reset:** a coordinating action (e.g. `IResetService` or a `SettingsViewModel.ResetCommand`)
  that: resets `ISettingsService` to defaults (which clears `SpotifyClientId` back to `""`),
  re-applies default hotkeys via [feature 06](./06-global-hotkeys.md), disconnects via
  [feature 03](./03-spotify-authentication.md), and triggers navigation to onboarding.
- **Confirmation:** native `ContentDialog` with a destructive primary button; use Fluent warning
  glyph. Never reset without confirmation.
- Reuse the prototype's copy.

## Data & persistence

- Disconnect clears tokens (Credential Locker). Reset additionally clears/over-writes the settings
  store with defaults ([feature 10](./10-settings-persistence.md)). Ensure no stale state remains
  (hotkeys re-registered to defaults).

## Edge cases & error handling

- Reset while disconnected → still resets settings/shortcuts; no-op for the already-cleared
  tokens.
- Failure mid-reset → attempt to leave the app in a coherent state (prefer
  defaults-applied + disconnected) and surface an error.
- Disconnect should also unregister/keep hotkeys consistent (hotkeys can remain bound but inert
  until reconnected — match the prototype where shortcuts persist but volume control is gated).

## Dependencies

- Uses [03 auth](./03-spotify-authentication.md) (disconnect), [10 settings](./10-settings-persistence.md)
  (defaults), [06 hotkeys](./06-global-hotkeys.md) (default rebind), and
  [01 shell](./01-application-shell.md) (route to onboarding).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Disconnect clears tokens and flips connection state (mock `IAuthService`); the stored Client ID
  is preserved.
- Reset restores all defaults (settings, step, hotkeys), clears the Client ID, and disconnects.
- Reset is a no-op without confirmation; Cancel changes nothing.

## Out of scope

- The OAuth/connect mechanics (feature 03) and the settings persistence layer internals
  (feature 10).

## Standards reminder

Native `ContentDialog` + Fluent icons; confirm destructive actions; concise code; clear default
state after reset.
