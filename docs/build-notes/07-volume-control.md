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
