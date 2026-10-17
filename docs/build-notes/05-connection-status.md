# Build notes — Feature 05: Connection Status & Account

> Append a new dated entry each time a session works on this feature. Add to the end; don't
> rewrite earlier entries.

## 2026-06-26 — Phase 1 (full build)

- **Deviations from spec/contracts:** None to `contracts.md` itself, but — mirroring how
  `ShellRouter`/`OnboardingFlow` carry their feature's pure state rules — the state-combination
  logic (which card/`InfoBar` to show for a given `ConnectionState` + last-known `PlayerState`)
  was factored into a new `Amplify.Core.ConnectionStatus.StatusPresentation` (a small
  `readonly record struct`), with `StatusViewModel` (`Amplify.App`) as a thin adapter that just
  projects it for `x:Bind`. This wasn't optional: `tests/Amplify.Tests` only references
  `Amplify.Core` (plain `net10.0`, no WinUI), so any logic worth unit-testing has to live there —
  same constraint noted in the feature 04 build notes.
- **Contract changes:** none. `IAuthService`/`ISpotifyClient`/`Account`/`PlayerState` consumed
  verbatim.
- **Assumptions:**
  - **Reconnect calls `IAuthService.ConnectAsync()`.** `IAuthService` exposes no separate
    "retry the refresh path" method — `ConnectAsync()` is the only user-initiated reconnect entry
    point, and it already runs PKCE login or token refresh depending on whether a refresh token is
    present, so this satisfies the doc's "re-runs the connect/refresh path."
  - **Connecting spinner: a `ProgressRing` inside the `InfoBar`'s content area, not its
    `IconSource`.** `InfoBar.IconSource` only accepts an `IconSource`-derived type
    (`FontIconSource`/`SymbolIconSource`/etc.) — there's no built-in spinning one — so the
    `ProgressRing` is placed as the `InfoBar`'s child content instead, which renders below the
    title/message.
  - **No active device yet vs. no active device confirmed are presented identically.**
    `StatusPresentation` treats `Connected` + `PlayerState == null` (the read hasn't completed yet)
    the same as `HasActiveDevice == false`, rather than briefly flashing the green-check card before
    the real player-state read lands. Covered by
    `ConnectedWithNoPlayerStateYetIsTreatedAsNoActiveDevice`.
  - **No `IValueConverter` introduced** — same precedent as feature 04: every `Visibility` binding
    uses WinUI's implicit bool→`Visibility` `x:Bind` conversion; the "Connected" label's
    success/warning colour swap uses two `TextBlock`s toggled by `Visibility` rather than a bound
    brush.
  - **`DevPlaybackSlice` and its volume buttons in `MainPage.xaml` are untouched** — they're
    feature 07's scope; this feature only adds the status block above them, replacing the
    temporary raw `DeviceText`/`VolumeText` strings' connection-state half (volume display stays
    as-is until 07 lands).
  - **`StatusViewModel` is registered as a DI singleton**, matching `OnboardingViewModel`/
    `ShellViewModel`/`SettingsViewModel`'s lifetime.
- **Deferred / known gaps:** none specific to this feature — the account/no-device/error/
  connecting states are all implemented per the doc.
- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test tests/Amplify.Tests` → **100 passed** (7 new `StatusPresentation` tests).
  - `dotnet format Amplify.slnx --verify-no-changes` → clean.
  - **Live manual walk (connected/no-device/error/reconnect) not run this session** — needs a real
    per-user Spotify Client ID + Premium account + active device, same constraint noted in the
    feature 04 and 07 build notes. Should be re-verified against a real account before the Phase 2
    integration pass.
- **Verified facts:** `InfoBar` accepts arbitrary child content (rendered below the title/message
  text) distinct from `IconSource`/`ActionButton` — used here to host the connecting spinner.

## 2026-06-26 — Manual-check feedback: no-active-device is not a warning + live polling

Manual check on a real account surfaced two UX issues with the doc's "no active device = warning"
treatment, and one functional gap:

- **Deviation from the feature doc (user-requested):** "connected, no active device" is **no
  longer presented as a warning**. It's a perfectly normal state (the user just hasn't started
  playback anywhere yet), not something gone wrong, so the card now always shows the green check +
  green "Connected" label regardless of device presence, and the separate "No active device"
  warning `InfoBar` was removed entirely. `StatusPresentation.ShowNoActiveDeviceWarning` and the
  `Status_NoActiveDevice.Title`/`.Message` resw entries were deleted (the device-line hint text,
  `Status_DeviceLine_NoActiveDevice`, is kept — there's still a "No active device" caption under
  the name when there's no device, just not styled as a warning).
- **Functional gap fixed: detecting a device becoming active.** The original implementation only
  read player state once on becoming `Connected`, so starting Spotify playback *after* connecting
  never updated the card — Spotify has no push event for "a device became active." Added a
  `DispatcherQueueTimer` (`Microsoft.UI.Dispatching`) in `StatusViewModel` that polls
  `ISpotifyClient.GetPlayerStateAsync()` every 5 seconds while `ConnectionState.Connected`,
  started/stopped alongside the existing connect-triggered refresh. Confirmed via Microsoft Learn:
  `DispatcherQueue.CreateTimer()` / `DispatcherQueueTimer.Interval`/`IsRepeating`/`Tick`/`Start`/
  `Stop` is exactly this API.
- **Manual/integration checks:** `dotnet build`/`dotnet test` (100 passed)/`dotnet format
  --verify-no-changes` all clean after the change. The polling fix has **not** yet been re-walked
  live on a real account in this session — that re-check (start Spotify playback after connecting,
  confirm the card picks up the device within one poll interval without a manual refresh) is still
  outstanding before this is considered fully verified.

## 2026-06-26 — Pause polling while the window is minimised

- **Added `StatusViewModel.Suspend()`/`Resume()`**, called from `MainWindow.OnVisibilityChanged`
  (`Window.VisibilityChanged`, `Microsoft.UI.Xaml`) so the 5-second poll stops while the window is
  minimised (`args.Visible == false`) instead of continuing to hit the Web API for a screen nobody
  can see. `Resume()` does an immediate refresh on top of restarting the timer, so the card catches
  up on anything that changed while suspended (e.g. playback started while minimised) without
  waiting out a full interval. `MainWindow` took a new `StatusViewModel` constructor parameter
  (resolved via DI like its other dependencies) to wire this.
- **Consideration for feature 08 (system tray & background):** this only covers OS-minimise.
  Feature 08 adds minimise-**to-tray**, which fully hides the window rather than just minimising it
  — check whether `Window.VisibilityChanged` already fires for that case (it may, since hiding the
  window is presumably implemented via the same `Visible` mechanism) before adding separate
  plumbing; if it doesn't, `ITrayService`'s `HideToTray()`/`ShowWindow()` will need to call
  `StatusViewModel.Suspend()`/`Resume()` too so polling actually stops once the window is in the
  tray, not just when it's minimised. Left a comment to this effect in
  `MainWindow.OnVisibilityChanged`.
- **Manual/integration checks:** `dotnet build`/`dotnet test` (100 passed)/`dotnet format
  --verify-no-changes` all clean. **Not yet re-walked live** — minimising/restoring the window to
  confirm polling actually stops/resumes (e.g. via a log line or a debugger breakpoint on the timer
  tick) is still outstanding.

## 2026-06-26 — Connecting InfoBar: drop the spinner instead of aligning it

- Tried aligning the connecting `InfoBar`'s `ProgressRing` inline with the title/message by moving
  both into custom `Content` (`InfoBar.Content` always renders on its own line below `Title`/
  `Message` by design, per the official docs, so that was the only way to get them side by side).
  On reflection the spinner wasn't worth the extra layout indirection, so it's removed instead:
  the bar reverted to the plain `x:Uid="Status_Connecting"`/`Title`/`Message` form (the
  `Status_Connecting.Title`/`.Message` resw keys are unchanged from Phase 1), with no custom
  `Content` and no `ProgressRing`. `Severity="Informational"` already gives the bar its icon, so
  the connecting state is still visually distinct from the error state without it.
- **Manual/integration checks:** `dotnet build` (0 warnings/errors), `dotnet test` (100 passed),
  `dotnet format --verify-no-changes` all clean.

## 2026-06-26 — Centre the main page content (real fix: nest a Grid inside the ScrollViewer)

- **Bug fix:** the status block/volume controls sat slightly right of centre once the window was
  wider than ~520px. First tried (and reverted) switching the outer `StackPanel` from
  `HorizontalAlignment="Stretch"` to `Center` — per the official alignment docs, `Stretch` on a
  `MaxWidth`-capped element is supposed to already behave like `Center`, so that wasn't the right
  fix and was reverted. The actual cause: the `StackPanel` sat directly inside the `ScrollViewer`
  with no intermediate `Grid`. `SettingsPage.xaml` already wraps its own `MaxWidth` `StackPanel` in
  a `Grid` between the `ScrollViewer` and the panel, with a comment noting it avoids a layout
  glitch — `MainPage` didn't have that wrapping `Grid` in the right place (it had a `Grid` at the
  `Page` root, outside the `ScrollViewer`, which doesn't help). Moving the `Grid` to wrap the
  `StackPanel` *inside* the `ScrollViewer`, matching `SettingsPage` exactly, fixed the offset.
  `HorizontalAlignment="Stretch"` on the `StackPanel` was correct all along and is unchanged.
  `OnboardingPage.xaml` doesn't wrap its `StackPanel` in a `Grid` either — worth checking there too
  if the same offset is ever noticed on that screen.
- **Manual/integration checks:** `dotnet build` (0 warnings/errors), `dotnet test` (100 passed),
  `dotnet format --verify-no-changes` all clean. Centring confirmed live by resizing/snapping the
  running app to several widths.

## 2026-06-27 — Code review: catch HttpClient timeouts in the poll, not just HttpRequestException

- **Bug fix:** `StatusViewModel.RefreshPlayerStateAsync` only caught `HttpRequestException`, so an
  `HttpClient` request timeout — `TaskCanceledException`, not `HttpRequestException` — escaped the
  fire-and-forget poll `Tick` task unobserved and unlogged, leaving the status card silently stuck
  on stale data with no diagnostic trail. Broadened the catch to
  `ex is HttpRequestException or TaskCanceledException`. Safe to catch unconditionally here because
  this call path never receives a caller-supplied `CancellationToken`, so a `TaskCanceledException`
  can only be `HttpClient`'s own timeout, never a real cancellation that should propagate.
- **Manual/integration checks:** `dotnet build` (0 warnings/errors), `dotnet test` (100 passed),
  `dotnet format --verify-no-changes` all clean.

## 2026-06-28 — Player-state polling moved out to a shared provider (feature 07 work)

- **Why:** feature 07 needs the *same* active-device/volume reading the status card already polls;
  having the volume controller read separately meant a device that became active while Amplify was
  open lit up the status card (which polls) but not the volume control. Rather than a second poller,
  the 5s poll moved into a shared `IPlayerStateProvider` (see [`contracts.md`](../contracts.md))
  consumed by both.
- **What changed in this feature's code:** `StatusViewModel` **no longer owns the
  `DispatcherQueueTimer`, the `ISpotifyClient` read, or `Suspend()`/`Resume()`.** It now takes
  `IPlayerStateProvider`, seeds `_playerState` from its `Current`, and updates on its
  `PlayerStateChanged` event; the connection-state handling (card/InfoBar visibility, clearing the
  device on disconnect) is unchanged. The poll/visibility logic that used to live here now lives
  verbatim in `Amplify.App/Spotify/PlayerStateProvider.cs`, including the
  `TaskCanceledException`-or-`HttpRequestException` catch from the 2026-06-27 fix.
- **Suspend/Resume moved:** `MainWindow.OnVisibilityChanged` now calls
  `PlayerStateProvider.Suspend()/Resume()` instead of `StatusViewModel`'s — same pause-while-hidden
  behaviour, one owner. The provider runs as an `IStartupInitializer` (Order 250) so it begins
  polling at launch independent of which screen is shown. `StatusPresentation` and its tests are
  untouched.

## 2026-07-25 — Device line explains a device that can't be volume-controlled (feature 07 fix)

- **Why:** feature 07 now disables the volume card when the active device reports it can't take
  volume commands (see [`build-notes/07`](./07-volume-control.md)). Without a message the card is
  simply dead with a named device sitting above it, which reads as a bug.
- **Where the copy went:** the existing device line, not a new `InfoBar` — the warning-styled
  `Status_NoActiveDevice` InfoBar was deliberately removed in the 2026-06-26 entry below and is not
  coming back. `DeviceLineText` is now a three-way switch: no device → `Status_DeviceLine_NoActiveDevice`
  (unchanged); controllable → the plain device name (unchanged); uncontrollable → new
  `Status_DeviceLine_VolumeNotSupported` ("{0} · volume control not supported"), formatted with
  `CultureInfo.CurrentCulture`. The card keeps its green check either way — this is a device
  capability, not a connection problem.
- **Nameless devices need their own string.** `PlayerState.DeviceName` is nullable, and formatting an
  empty name into "{0} · …" leaves a dangling separator. A second key,
  `Status_DeviceLine_VolumeNotSupportedUnnamed` ("Volume control not supported"), covers that case —
  the separator lives in the resource, so it can't be assembled in code where a translator can't move
  it. Caught in review.
- **`StatusPresentation` gained `DeviceSupportsVolume`** (false both when there's no device and when
  the device can't set volume) so the branch rule stays unit-tested outside WinUI, matching how
  `HasActiveDevice`/`DeviceName` already work.
- **Note for a future session:** this feature's doc still describes the yellow warning triangle and
  no-active-device `InfoBar` that were removed in June; only the device-line bullets have been
  brought in line with the code.

## 2026-10-17 — Spotify-unreachable is surfaced (Phase 2 blocker)

- **The bug:** with the network pulled, nothing changed on screen. `PlayerStateProvider.RefreshAsync`
  logged the failure and returned *before* `Publish(...)`, so consumers saw **no event at all** — the
  card sat on "Connected" plus a device and volume from the last successful read, indefinitely. The
  catch block's comment said "the status card owns surfacing persistent failures", but the card had
  no way to observe a read failure. The only failing check of the eighteen in the Phase 2 pass.
- **Design decision — a separate signal, not `ConnectionState.Error`.** `ConnectionState.Error` is
  set in six places, all auth/token failures, and it is genuinely *latching*: the token is dead and
  only re-authorisation fixes it. Being unreachable is the opposite — transient, and it must clear
  itself. Conflating them invites the fix that would be **worse than the bug**: enter an error state,
  stop the timer, wait for a click. So `ConnectionState` is untouched and
  `IPlayerStateProvider` gained `IsUnreachable` + `ReachabilityChanged` instead (see
  [`contracts.md`](../contracts.md)).
- **Auto-recovery is the load-bearing requirement, and nothing gates it.** The new
  `Amplify.Core.Spotify.ReachabilityTracker` records read outcomes and reports a flag; it has **no**
  influence on the timer. `UpdatePollingState` is byte-for-byte unchanged, so the 5s poll runs right
  through an outage and the first successful read clears the flag with no user action.
  `ReachabilityTrackerTests.ASingleSuccessClearsTheUnreachableState` and
  `RecoveryIsImmediateHoweverLongTheOutageRan` are the regression guards for that — if either ever
  needs a `Reset()`/reconnect call to pass, the app has stopped healing itself.
- **Threshold: 3 consecutive failures (~15s).** Short enough to be prompt, long enough that one
  dropped request doesn't flap the UI. `FailuresMustBeConsecutiveToCount` pins that intermittent
  failures never accumulate. Transitions are reported once, not once per poll, so a long outage
  doesn't re-notify every 5 seconds.
- **The Reconnect trap, avoided.** `ReconnectAsync` calls `IAuthService.ConnectAsync()` — the full
  PKCE flow, including `Launcher.LaunchUriAsync` to the system browser. That is right for a dead
  token and badly wrong for a Wi-Fi blip, where the token is fine. The unreachable bar therefore gets
  its **own** `RetryCommand`, which only calls `_playerStateProvider.RefreshAsync()`. It is an
  optional shortcut for an impatient user; walking away recovers just as well.
- **The connected card is suppressed while unreachable** (`ShowConnectedCard` now also requires
  `!IsUnreachable`). Leaving it up is the actual complaint in the bug report — stale device and
  volume presented as current. This matches how `Connecting`/`Error` already replace the card rather
  than sitting beside it.
- **Severity is `Warning`, not `Error`.** The two bars now mean different things to the user: the
  error bar needs them (sign in again), the warning bar doesn't (the app is already recovering).
- **`Status_Error.Title` retitled** from "Can't reach Spotify" to "Can't sign in to Spotify". Two
  bars can't share a title, and the old one described the *network* case while the bar it sits on is
  raised only by auth/token failures. New keys: `Status_Unreachable.Title`/`.Message` and
  `Status_Unreachable_Retry.Content` ("Try again").
- **The unreachable message tells the user what to do, not what's still fine.** First draft was
  "Your account is still signed in. Amplify keeps trying…" — reassurance about something the
  user isn't worried about yet. Reworded to "Check your internet connection. Amplify will
  reconnect automatically once it's back.": the likely cause and the one thing they can act on
  first, then the promise that stops them hunting for a button. The "still signed in" fact is
  carried by the bar's severity instead — yellow means wait, the red sign-in bar means act.
- **Precedence:** `IsUnreachableNotice` requires `ConnectionState.Connected`, so an auth error keeps
  its own bar even while reads are failing — the Try again button could never recover from a dead
  token.
- **Not covered by unit tests:** `tests/Amplify.Tests` references only `Amplify.Core`, so
  `PlayerStateProvider` (WinUI `DispatcherQueueTimer`) and `StatusViewModel` can't be tested here, as
  with every other App-layer type. "Polling continues while unreachable" is held by code structure
  (the tracker never touches the timer) and by the packaged run; the threshold, self-clearing, and
  precedence rules are all unit-tested in Core.
- **Deliberately out of scope:** the volume card still enables off `IVolumeController.CanControl`,
  which doesn't consult reachability, so during an outage it stays interactive showing the
  last-known level. A write then fails and `VolumeController` reverts it optimistically — existing,
  already-tested behaviour. Wiring reachability into the controller would widen the blast radius
  into feature 07's state machine for a cosmetic gain; worth a separate look.
- **Checks:** `dotnet build Amplify.slnx -c Debug -p:Platform=x64` → 0 warnings / 0 errors.
  `dotnet test Amplify.slnx -c Debug -p:Platform=x64` → **285 passed, 0 skipped** (269 before this
  change — the bug report's "254 baseline" was stale — plus 16 new: 10 `ReachabilityTracker` cases
  and 6 `StatusPresentation` cases). `dotnet format --verify-no-changes` clean. **Not yet verified
  on a packaged run** — the live network-pull checks in
  [`integration-smoke-test.md`](../integration-smoke-test.md) are still outstanding.

## 2026-10-17 — Code-review fixes to the unreachable work (same day)

A high-effort review found six issues in the entry above. All six were confirmed against the code
and fixed; the first made the feature not work at all in the most common real outage.

- **Correction to the entry above: "Error is set in six places, all auth/token failures" was wrong.**
  `SpotifyAuthService.RefreshCoreAsync` caught *every* `HttpRequestException` — a pure transport
  failure included — and called `SetState(ConnectionState.Error)`. Spotify access tokens last ~1h,
  so any outage spanning a token refresh flipped auth to `Error`, which (a) stopped the poll timer,
  since `PlayerStateProvider` polls only while `Connected`, (b) reset the reachability tracker, so
  the new warning bar could never appear, and (c) offered Reconnect and its browser flow. That is
  precisely the "enter an error state, stop the timer, wait for a click" outcome the entry above
  claims to have avoided — it just needed a longer outage than the smoke test's ~100s to show.
  **Fix:** `RefreshCoreAsync` now splits its catch on `SpotifyRequestFailure.IsTransport`. A request
  that never reached Spotify leaves the connection state alone and rethrows, so the poll keeps
  running and the read failures surface as unreachable; only a refresh Spotify actually *rejected*
  latches `Error`. See [`build-notes/03`](./03-spotify-authentication.md).
- **New `Amplify.Core.Spotify.SpotifyRequestFailure`** carries that distinction for both callers.
  `IsTransport` = DNS/TCP/TLS/proxy, decided on `HttpRequestException.HttpRequestError` rather than
  on a null `StatusCode` alone, because `SpotifyTokenClient` also throws one by hand with a null
  status. `MeansUnreachable` = transport, timeout, `408`, or `5xx`.
- **A persistent `403` is no longer reported as a network outage.** `GetPlayerStateAsync` calls
  `EnsureSuccessStatusCode()`, so a user whose own Spotify app registration lacks
  `user-read-playback-state` gets a `403` every poll — which the first cut turned into a permanent
  "check your internet connection" over a hidden account card, blaming the wrong thing and promising
  a recovery that could never arrive. `4xx` now stays out of the outage count. It reverts to the
  pre-existing stale-card behaviour there, which is a known gap, but it no longer states a false
  cause.
- **The tracker resets on `Suspend()`.** Hiding the window stops the poll without clearing the flag,
  so an outage that ended while minimised stayed on screen until `Resume`'s read returned — and a
  `StatusViewModel` constructed in that window seeded straight into it. The question stops applying
  while nothing is reading, exactly as on disconnect.
- **"Try again" is no longer a silent no-op.** `RefreshAsync` returned immediately when a read was
  already in flight, which during a slow outage is most of the time, so the click did nothing and
  said nothing. It now returns the in-flight task, so an awaiting caller waits for a real result —
  and because `[RelayCommand]` async commands disallow concurrent execution by default
  (`AsyncRelayCommandOptions.None`, verified against the CommunityToolkit.Mvvm 8.4.2 docs), the
  button disables itself while the read runs, which is the honest signal: already trying. Keyed on
  `Task.IsCompleted` rather than a flag needing a clear, so a missed clear cannot wedge the poll.
- **"~15s" was an over-promise and the docs now say so.** The threshold counts failed *reads*, not
  ticks, and reads collapse onto one in-flight request, so a hanging request swallows every tick in
  its window. `SocketsHttpHandler.ConnectTimeout` is now 10s to bound the blackhole/captive-portal
  case; see [`build-notes/07`](./07-volume-control.md) for why it is not `HttpClient.Timeout`.
- **`Status_Error` copy revisited.** With `RefreshCoreAsync` fixed, that bar really is raised only by
  a rejected session or a failed interactive sign-in, so "Can't sign in to Spotify" now holds. A
  `Status_Error.Message` was added — "Reconnect to sign in again. If that doesn't work, check your
  internet connection." — because `ConnectAsync` can still reach the bar from a network failure
  during an explicit sign-in.
- **Not unit-tested:** `SpotifyAuthService` is App-layer (PasswordVault, `Launcher`), so the
  `RefreshCoreAsync` split has no direct test; the classifier it delegates to has 17. The end-to-end
  path was traced by hand instead — nothing between `RefreshCoreAsync` and
  `PlayerStateProvider.ReadAsync` catches, so the rethrown transport failure does reach the tracker.
- **Checks:** build 0 warnings / 0 errors; `dotnet test` **302 passed, 0 skipped** (285 before, 17
  new `SpotifyRequestFailure` cases); `dotnet format --verify-no-changes` clean. The packaged
  verification is still outstanding and now needs the long-outage case in particular.
