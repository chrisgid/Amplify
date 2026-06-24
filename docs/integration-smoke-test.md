# Amplify — Integration & Smoke Test

> Read first: [`specification.md`](./specification.md), [`getting-started.md`](./getting-started.md),
> [`contracts.md`](./contracts.md).
>
> This is **Phase 2** of the build (see [getting-started §8](./getting-started.md#8-build-order)):
> the first time the whole app is assembled and exercised as a user would. Unit tests (feature 02)
> are logic-only and **cannot** catch the integration bugs below — window/title-bar, tray,
> global-hotkey, OAuth-loopback, packaged-identity, and DI-wiring issues only show up when the real
> app runs. Run this after the features are in and **before** cutting a release
> ([feature 14](./features/14-release.md)).

> **Start by reading the [build notes](./build-notes/).** Each feature's
> `build-notes/NN-*.md` lists its deviations, assumptions, and **deferred/known gaps** — exactly the
> things this pass needs to check. Log the results of this pass to
> `build-notes/integration-smoke-test.md` (append-only, same convention as the per-feature notes).

## Assembly

- [ ] Every feature's `AddXxx()` DI registration is called by the shell host
      ([01](./features/01-application-shell.md)); the app composes with no missing-service
      resolution errors at startup.
- [ ] All `IStartupInitializer`s are registered and run in the documented order
      (theme → tray/window → hotkeys; see [`contracts.md`](./contracts.md) and the launch sequence
      in [feature 01](./features/01-application-shell.md)).
- [ ] The packaged (MSIX) app deploys and launches; logging is writing to
      `LocalFolder\logs\` and contains **no tokens/PII**.

## End-to-end journey

Walk the primary journey from [spec §3](./specification.md#3-primary-user-journey) on a real
machine with a real Spotify account:

1. **First run / onboarding** — [ ] launches to onboarding when no session is stored; the 6 setup
   steps render; the redirect-URI copy chip copies `http://127.0.0.1:49737/callback` with ✓
   feedback; **Connect is disabled until a Client ID is entered**.
2. **Authorize (PKCE)** — [ ] Connect opens the system browser to Spotify; approving redirects to
   the local success page; the app advances to Verifying → Main. The Client ID is persisted to
   `settings.json` before the flow starts.
3. **Denied path** — [ ] declining on Spotify shows the "Access wasn't granted" notice and a
   working retry.
4. **Connected status** — [ ] a connected account shows the green check + "Connected" with the
   active device; with **no active device** it shows the warning triangle and the "No active device"
   `InfoBar`.
5. **Volume — in-app** — [ ] the slider and ± buttons change the **active Spotify device's** volume
   (confirm in the Spotify app); muted at 0; control is dimmed when there is no active device.
6. **Volume — global hotkeys** — [ ] with Amplify **minimised to the tray and another app
   focused**, the default `Ctrl+Alt+↑/↓` change Spotify's volume; the meter stays in sync.
7. **Rebind** — [ ] recording a new combo persists it; it survives a restart and re-registers; a
   combo already owned by another app is rejected with the prior binding kept.
8. **Tray & background** — [ ] minimise hides to the tray and **removes the taskbar button**;
   close-to-tray keeps hotkeys alive; the tray menu Open/Settings/Quit work; Quit fully exits.
9. **Single instance** — [ ] launching a second time surfaces the existing window.
10. **Notifications** — [ ] with "Notify on volume change" on, a hotkey change shows a toast that
    coalesces under rapid presses; off suppresses it.
11. **Theming** — [ ] the app follows the Windows light/dark theme and accent live; the
    System/Light/Dark override applies immediately and persists.
12. **Settings persistence** — [ ] toggles/step/theme persist across a restart; the read-only
    Client ID shows with "Reset Amplify to change it"; the footer "Amplify" name links to the repo.
13. **Reset** — [ ] confirming Reset clears shortcuts + Client ID, disconnects, and returns to
    onboarding; Cancel changes nothing.
14. **Startup** — [ ] "Launch at startup" reflects the real `StartupTask` state; with it enabled the
    app starts on sign-in (and hidden if "start minimized" is on).

## Resilience spot-checks

- [ ] **No active device:** with nothing playing, status guides the user to open Spotify; volume
      control is disabled (`GET /v1/me/player` → `204`, `PUT volume` → `404/403` handled, not a
      crash).
- [ ] **Token refresh:** after the access token expires (or on a forced `401`), the next action
      refreshes silently; a rotated refresh token is persisted; concurrent calls don't double-refresh.
- [ ] **Offline / Spotify unreachable:** the status block shows the error state with Reconnect; the
      app does not crash.
- [ ] **Theme/accent change while hidden:** applied correctly when the window is next shown.

## Exit criteria

- [ ] The full journey above passes on a clean machine (or a fresh user profile).
- [ ] No unhandled exceptions in the logs during the run.
- [ ] `dotnet test` is green (unit tests still pass — Phase 1 didn't weaken them).

Only when all three hold is the app ready for [release (feature 14)](./features/14-release.md).
