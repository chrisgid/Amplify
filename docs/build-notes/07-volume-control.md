# Build notes — Feature 07: Volume Control

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-21 — Phase 0 (walking-skeleton sliver) · feat/07-volume-control

Scope: the smallest path that actually moves Spotify's volume from the app — one
`GET /v1/me/player` read + one `PUT /v1/me/player/volume`, wired to ± buttons and to a hard-coded
pair of global hotkeys (`Ctrl+Alt+Up` / `Ctrl+Alt+Down`). The full meter/slider UI, optimistic +
coalescing logic, `IVolumeController`, the real `IHotkeyService`, and `CanControl` gating are
deferred to Phase 1.

- **Deviations from spec/contracts:** None. `ISpotifyClient` and `PlayerState` implemented verbatim
  from `contracts.md` (`GetPlayerStateAsync` / `SetVolumeAsync`, no device id — always the active
  device).

- **Contract changes:** None.

- **Assumptions / decisions:**
  - **`SpotifyClient` lives in `Amplify.Core`, not `Amplify.App`.** It depends only on `HttpClient`
    (BCL) + `IAuthService` (Core) with no Win32/WinUI dependency, so keeping it in the UI-free core
    lets the request/response mapping be unit-tested against a fake `HttpMessageHandler` (the test
    project references Core only). The class is `public` solely so the typed-client registration in
    the app assembly can name it (`AddHttpClient<ISpotifyClient, SpotifyClient>()`); callers still
    depend on the interface. The `AddSpotifyClient()` DI extension stays in App (where
    `Microsoft.Extensions.Http` already lives) and sets the base address `https://api.spotify.com/`.
  - **Bearer set per request**, not via a `DelegatingHandler`: `SpotifyClient` builds an
    `HttpRequestMessage` and stamps `Authorization: Bearer <GetAccessTokenAsync()>` on it, avoiding
    mutation of a shared `DefaultRequestHeaders`. A reusable auth handler can come in Phase 1.
  - **204 → no active device, not an error.** `GetPlayerStateAsync` maps an empty `204` to
    `PlayerState(HasActiveDevice: false, 0, null)`; a `200` with a `device` maps
    `is_active` / `volume_percent` / `name`. `volume_percent` is nullable in the schema → defaults
    to 0.
  - **Global hotkeys = hard-coded `RegisterHotKey`, no abstraction.** `GlobalHotkeyWindow`
    P/Invokes `RegisterHotKey`/`UnregisterHotKey` (user32) and hooks the window with
    `SetWindowSubclass`/`RemoveWindowSubclass`/`DefSubclassProc` (comctl32 v6) to observe
    `WM_HOTKEY`. The `SUBCLASSPROC` delegate is stored in a field so the GC can't collect it while
    native code still holds the pointer. `MOD_NOREPEAT` collapses auto-repeat. **Background/minimised
    firing is intrinsic to `RegisterHotKey` (a system-wide hot key) — it is NOT deferred to
    feature 06.** What feature 06 adds: the `IHotkeyService` seam, rebinding/recording, persistence,
    conflict handling, and the `WH_KEYBOARD_LL` fallback (which also covers the one edge case here —
    an elevated foreground app can swallow the keystroke via UIPI when Amplify runs non-elevated).
  - **`MainWindow` is throwaway scaffolding** (extends the existing Phase-0 connect test): on connect
    it reads player state, shows volume + ±/Refresh, and arms the hotkeys; it now also takes
    `ISpotifyClient` and implements `IDisposable` (to satisfy CA1001 for the owned
    `GlobalHotkeyWindow`) with disposal driven from the window's `Closed` event. Volume is committed
    only after Spotify accepts the change (apply-then-commit), so the displayed value can't drift —
    full optimistic/revert + coalescing is Phase 1.
  - **403/404 on `PUT volume`** (no controllable device / restriction) currently surface as a generic
    "couldn't change the volume" message via `EnsureSuccessStatusCode` → caught `HttpRequestException`.
    Mapping these to the dedicated "no active device" guidance is owned by feature 05 and wired in
    Phase 1.

- **Deferred / known gaps (→ Phase 1):** `IVolumeController` (step math, coalescing, optimistic
  revert, `VolumeChanged` event), `IHotkeyService` + rebinding/persistence/conflicts/LL-hook,
  `CanControl` gating (Premium/active-device), configurable step size from settings, 429 backoff
  honouring `Retry-After`, the live meter/slider UI, and removing the throwaway `MainWindow`/UI.

- **Manual/integration checks:**
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64` → **0 warnings, 0 errors**.
  - `dotnet test … --filter "Category!=RequiresSpotify"` → **19 passed** (12 existing + 7 new
    `SpotifyClient` tests: 204 → no device, 200 mapping, bearer header, PUT URL, clamp 0–100).
  - `dotnet format --verify-no-changes` → clean.
  - **Live "volume moves from a hotkey" is a manual desktop check** — it needs a real per-user
    Client ID + registered redirect URI, a Spotify **Premium** session with an **active device**, and
    packaged identity (for the connect step's `PasswordVault`). The `RegisterHotKey`/subclass and live
    Web API paths carry no unit tests by design (they can't run headless / in CI); the headless tests
    cover the client's request shaping + response mapping only.

- **Verified facts (Microsoft Learn / Spotify):**
  - `RegisterHotKey` "Defines a **system-wide** hot key"; `WM_HOTKEY` (0x0312) is posted to the
    registering window's queue regardless of focus → works in the background. `RegisterHotKey` fails
    (returns false) if a combo is already owned; `MOD_NOREPEAT` (0x4000) suppresses auto-repeat.
  - `SetWindowSubclass` (comctl32 v6, API set since Win10 14393 — under our 17763 floor) is the
    supported way to observe extra `WM_` messages on a WinUI 3 window; pair with `DefSubclassProc`
    and `RemoveWindowSubclass`. HWND via `WinRT.Interop.WindowNative.GetWindowHandle(this)`. The
    subclass helpers cannot subclass across threads → register on the window's UI thread.
  - Spotify Web API (confirmed against the reference): `GET /v1/me/player` → `200` with
    `device.{is_active,name,volume_percent}` or **`204 No Content`** when nothing is active;
    `PUT /v1/me/player/volume?volume_percent={0..100}` → **`204`** on success, `403`/`429` documented
    (no `404` listed, but spec §6 notes it can occur as "Device not found").

## 2026-06-28 — Phase 1 (full build) · feat/07-volume-control

Scope: replaced the Phase-0 scaffolding with the real `IVolumeController`, the live volume card
(meter + slider + step nudges), configurable step, optimistic/coalescing writes, `CanControl`
gating, and 429/403/404 handling. Retired `DevPlaybackSlice` + `DevHotkeyVolumeBridge`.

- **Contract changes:** `IVolumeController` in [`contracts.md`](../contracts.md) gained
  `Task RefreshAsync()` and `event EventHandler? StateChanged` (the latter so the UI can re-evaluate
  `CanControl`/device context when it changes without a volume change). Approved with the user before
  building. **`DeviceName` was deliberately *not* added** — see UX deviation below.

- **Deviations from spec/contracts:**
  - **Volume card omits the device name.** The feature doc's "Now controlling {device}" is satisfied
    by feature 05's connected status card directly above it (which owns device labelling and the
    "no active device" message); repeating the name on the volume card would duplicate it in this
    layout. So the card shows only the level/slider/nudges and dims (via `CanControl`) when there's
    nothing to control. This also kept the contract addition to the two approved members.

- **Assumptions / decisions:**
  - **Player-state sync = event-driven, no new poll (Option A, agreed with user).** The controller
    reconciles only on connect (`IAuthService.ConnectionStateChanged`), on window-resume
    (`MainWindow.VisibilityChanged`, alongside `StatusViewModel.Resume`), and implicitly after each
    write. Feature 05's existing 5s `GET /v1/me/player` poll stays the sole continuous poll. The two
    read device-presence independently and may transiently disagree, but each card stays internally
    consistent; externally-changed volume reconciles on next connect/resume (doc-sanctioned). Because
    reconciles happen at calm moments, no extra write-race guarding was needed beyond skipping a
    reconcile's volume while a write is in flight.
  - **`VolumeController` lives in `Amplify.Core` and also implements `IStartupInitializer`
    (Order 900).** It depends only on Core interfaces, so it's fully unit-testable. The initializer
    subscribes it to hotkeys/auth and does the first reconcile for a session *restored before it
    existed* (whose `ConnectionStateChanged` had already fired). One instance is registered as the
    shared singleton behind `IVolumeController` **and** `IStartupInitializer`. `MainWindow` no longer
    drives the on-connect refresh (the controller self-subscribes); it only nudges a resume reconcile.
  - **Optimistic UI + single trailing writer.** A change updates `Volume` and raises `VolumeChanged`
    immediately, then records the latest target; one writer task drains it, so a burst of hotkey
    presses collapses to (at most) the in-flight write plus one final write. The "writer running"
    flag is flipped only inside the state lock so a target queued just as the writer drains can't be
    stranded. On failure the optimistic value reverts to the last Spotify-accepted level.
  - **429 backoff = a `RateLimitHandler : DelegatingHandler`** added *outermost* in the typed
    client's pipeline (before the auth handler), so a retry re-runs auth with a fresh bearer. It
    honours `Retry-After` (delta seconds or HTTP date) and otherwise backs off exponentially
    (0.5s/1s/2s), bounded to 3 retries before surfacing the final `429`. Takes an injectable
    `TimeProvider` so the delay is unit-testable; `ComputeDelay` is an `internal static` pure function
    tested directly.
  - **403/404 on `PUT volume` → `DeviceNotControllableException`** (new Core type). The controller
    catches it, reverts the optimistic value, and flips `CanControl` false until the next reconcile
    re-checks device presence; the user-facing "no active device" guidance stays owned by feature 05.
    Other non-success on the PUT still throws `HttpRequestException`.
  - **Slider binding.** `Slider.Value` two-way-binds `VolumeViewModel.SliderValue` (double); a
    source→target update from the controller doesn't loop back through the setter, so no echo guard
    is needed. The muted-vs-volume speaker glyph (U+E74F at 0, else U+E767) is chosen in the
    view-model.
  - New shared string `Volume_Header.Text` ("Volume") added to `Resources.resw`.

- **Deferred / known gaps:** Toasts on volume change (`NotifyOnVolumeChange`) remain feature 09 — this
  feature only emits `VolumeChanged` for it to consume. Graded speaker glyphs (low/med/high) were not
  added; a single mute/volume pair is used.

- **Manual/integration checks:**
  - `dotnet build src/Amplify.App -p:Platform=x64` and Core/Tests → **0 warnings, 0 errors**.
  - `dotnet test` → **164 passed, 0 skipped** (added `VolumeController` step/clamp/gating/revert/
    coalescing/hotkey tests, `RateLimitHandler` retry+backoff tests, `SpotifyClient` 403/404 tests).
  - `dotnet format --verify-no-changes` → clean.
  - **Not unit-testable here / manual:** the volume card UI, the muted glyph mapping, and the live
    Web API path live in the WinUI assembly (the test project references Core only). The end-to-end
    "slider/±/hotkey move Spotify's volume, card dims with no device, rapid presses don't desync"
    check needs a Premium session with an active device and packaged identity — a desktop smoke test.

## 2026-06-28 — Player-state sync switched from Option A to Option C (shared provider)

Supersedes the "Player-state sync = Option A" decision in the entry above. On review, Option A had a
real UX gap: with no active device, opening Amplify and *then* starting playback in Spotify lit up
the status card (which polls every 5s) but **not** the volume control (which only reconciled on
connect/resume/after-write), so the control stayed disabled until a manual refresh.

- **Change:** extracted a shared `IPlayerStateProvider` (Core; see
  [`contracts.md`](../contracts.md)) implemented once in the App layer
  (`Amplify.App/Spotify/PlayerStateProvider.cs`) that owns the single 5s poll + on-demand reads. Both
  `StatusViewModel` (feature 05) and `VolumeController` consume its `PlayerStateChanged`. The 5s poll
  is now the **only** continuous poll and feeds everything, so a device becoming active enables the
  volume control within one poll interval. See the feature-05 build-notes for what moved out of
  `StatusViewModel`.
- **VolumeController changes:** no longer calls `ISpotifyClient.GetPlayerStateAsync` (still uses it
  for `SetVolumeAsync`) and no longer subscribes to `IAuthService.ConnectionStateChanged`. It now
  subscribes to `IPlayerStateProvider.PlayerStateChanged`, seeds from `Current` in its startup hook,
  and `RefreshAsync()` delegates to the provider. `CanControl` still reads `IAuthService.State` (the
  provider only ever reports a device while connected, so the two agree). The **optimistic-write race
  guard matters more now** that a poll lands every 5s: a reconcile skips applying the polled volume
  while a write is in flight (`!_writerRunning`). The brief post-write window where the server may not
  yet reflect the new volume can still cause a one-poll snap-back; accepted as in the Option C
  trade-off, and rare given Spotify reflects volume promptly.
- **Contract:** added `IPlayerStateProvider` and a `PlayerStateChanged` row to the cross-feature
  events table. `IVolumeController` is unchanged from the earlier Phase 1 entry (`RefreshAsync` now
  means "ask the shared provider to re-read").
- **Manual/integration checks:** Core/Tests + `src/Amplify.App` (x64) build → **0 warnings, 0
  errors**; `dotnet test` → **166 passed, 0 skipped** (added `DeviceBecomingActiveEnablesControl`
  proving the gap is closed, and `RefreshDelegatesToTheSharedProvider`); `dotnet format
  --verify-no-changes` → clean. The live "start playback after opening Amplify → control enables
  within ~5s" path is part of the desktop smoke test (needs Premium + a device).
