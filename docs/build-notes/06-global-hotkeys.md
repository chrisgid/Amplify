# Build notes — Feature 06: Global Hotkeys

> Append a new dated entry each time a session works on this feature. Add to the end; don't rewrite
> earlier entries.

## 2026-06-27 — Phase 1 (full build) · feat/06-global-hotkeys

- **Storage format decision (user-directed):** the user asked whether persisting hotkeys "as a
  stable string form" is the best approach vs. storing a key number mapped back per keyboard layout.
  Outcome — a **hybrid**, kept the contract's canonical-string persistence but applied the
  layout idea where it actually pays off (display):
  - The runtime model already *is* a key number — `Hotkey.Key` is a Win32 virtual-key code (the
    value `RegisterHotKey` consumes). The canonical string is just a stable, layout-independent
    encoding of `(modifiers, vk)`, anchored to VK identity (VK `0x5A` ↔ `"z"` always, since Windows
    defines VK `0x41`–`0x5A` as A–Z regardless of layout). This keeps `settings.json` human-readable
    and portable across machines, and honours `contracts.md` (`AppSettings.HotkeyVolumeUp/Down` are
    canonical strings) with no contract change.
  - **Layout-awareness lives on the display path only:** `KeyLabelResolver` (App) overrides the
    keycap glyph for *character* keys (letters/digits/OEM punctuation) using
    `MapVirtualKeyExW(vk, MAPVK_VK_TO_CHAR, GetKeyboardLayout(0))`, so a non-US layout shows the
    right glyph. Named keys (arrows, F-keys, editing keys) keep fixed symbols. Chose
    `MapVirtualKeyEx` over `ToUnicodeEx` deliberately — `ToUnicodeEx` is documented to mutate the
    kernel-mode keyboard buffer (dead keys), which is unwanted on a pure display path.
- **Deviations from spec/contracts:** none. `Hotkey`, `KeyModifiers`, `HotkeyAction`,
  `IHotkeyService` implemented exactly per `contracts.md`. Added a few **additive** members not in
  the contract sketch but consistent with it: `Hotkey.TryCreate` (capture validation),
  `Hotkey.IsValid`, `Hotkey.IsModifierVirtualKey`, and `HotkeyDefaults` (Core) — all pure/testable.
- **Canonical fallback for unmapped keys:** keys with no friendly name serialise as `vk{code}`
  (e.g. `ctrl+vk255`) so *any* virtual-key code round-trips through `TryParse` without an exhaustive
  OEM name table. Friendly names cover arrows, A–Z, 0–9, numpad, F1–F24, and common editing keys.
- **Registration architecture — low-level keyboard hook (not `RegisterHotKey`), user-directed:**
  `KeyboardHookHotkeyService` installs a global `WH_KEYBOARD_LL` hook (`SetWindowsHookEx`). The user
  wanted shortcuts that **don't block other applications** — `RegisterHotKey` consumes the key, so a
  combo that's also a shortcut elsewhere would stop working in that app. The hook observes each press
  and always calls `CallNextHookEx` (never returns 1), so the key is **passed through** to the
  foreground app and *also* drives Amplify. An earlier draft of this feature used a message-only
  window + `RegisterHotKey`; it was replaced for the pass-through requirement. The `IHotkeyService`
  abstraction was unchanged, so only the service implementation swapped — registrar, view-models,
  bridge, and UI were untouched.
  - **Hook mechanics (verified via MS docs):** the LL hook runs on the **installing thread, which
    must pump messages** — so it's installed on the UI thread (during the Order-400 registrar) and
    its callback runs there. The callback must return within the `LowLevelHooksTimeout` (≤1s) or
    Windows silently drops the hook, so it does the minimum and defers the event via
    `DispatcherQueue.TryEnqueue`. **`GetAsyncKeyState` is unreliable inside the callback** (docs: the
    hook runs *before* the async key state updates), so modifier state is tracked from the hook's own
    key-up/down events (handling left/right modifier VK variants 0xA0–0xA5/0x5B/0x5C), not queried.
    Auto-repeat is collapsed with a "held non-modifier keys" set (the `MOD_NOREPEAT` equivalent).
  - **Consequences vs `RegisterHotKey`:** (a) **no cross-app conflict** — multiple apps can observe
    the same key, so the "combination in use by another app" error path is gone; only the
    duplicate-between-the-two-Amplify-actions check remains, plus a rare hook-install failure. (b)
    **F12 works** (the debugger reservation only applies to `RegisterHotKey`). (c) a non-elevated
    Amplify can't see input destined for an **elevated window** (Windows UIPI) — shortcuts won't fire
    while such a window is focused. (d) a tiny per-keystroke cost system-wide (kept negligible).
- **Single-key shortcuts allowed (deviation from the feature doc, user-directed):** the doc's
  acceptance criterion "≥1 modifier and a single non-modifier key" was relaxed — `Hotkey.IsValid`
  now accepts a bare non-modifier key (e.g. F11/F12), while a modifier-only press is still rejected.
  This only makes sense with the pass-through hook (a single typing key both types and nudges
  volume); it would be a severe footgun under the swallowing `RegisterHotKey`. Feature doc acceptance
  criteria updated to match.
- **Registration flow:** hotkeys register at launch via a `HotkeyRegistrar` `IStartupInitializer`
  (Order 400, the hotkeys band) and re-register on `ISettingsService.Changed` (idempotent). With the
  hook, `TryRegister` only fails if the hook can't be installed (catastrophic); the editor VM still
  calls `TryRegister` *before* persisting, and rejects a duplicate of the other action up front.
- **Removed Phase 0 scaffolding:** deleted `Interop/GlobalHotkeyWindow.cs` (the hard-coded
  Ctrl+Alt+Up/Down skeleton) and the hotkey-arming logic in `MainWindow`. Registration now lives in
  the service/registrar; `MainWindow.OnConnected` only does the playback refresh now.
- **Interim consumer (temporary):** feature 06 raises `HotkeyPressed` but performing the volume
  change is feature 07. To keep the app working, `DevHotkeyVolumeBridge` (Order 900, in `Dev/`,
  alongside the existing `DevPlaybackSlice` scaffolding) subscribes `HotkeyPressed` →
  `DevPlaybackSlice.NudgeAsync(±1)`. **Feature 07 must remove this bridge** and have
  `IVolumeController` subscribe to `IHotkeyService.HotkeyPressed` directly (it already owns the
  step math). The two on-screen volume buttons on the main page are still the Phase-0 temp controls,
  also pending feature 07's real meter/slider.
- **UI matches the design mockup:** a "Keyboard shortcuts" caption title over two CommunityToolkit
  `tk:SettingsCard` rows, each with a circled +/− `HeaderIcon` (Segoe Fluent `AddTo` `ECC8` /
  `RemoveFrom` `ECC9`, glyphs verified against the icon list), `Header`/`Description` for the
  label/sub-text, and the keycaps + edit button as the card content. The recording prompt and
  conflict message share one status line under both cards (the row VMs push to a shared sink on
  `HotkeysViewModel`), matching the mockup's single helper line.
- **Recording UI capture:** key capture uses
  `Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey)` to read modifier
  state on a non-modifier `KeyDown` (the UWP `CoreWindow.GetKeyState` returns null in WinUI 3 —
  confirmed via MS docs; Alt is `VirtualKey.Menu`). The capture host is a focusable `ContentControl`
  **nested inside** the `SettingsCard` (the card is a `ButtonBase`, so capturing on it directly would
  risk Space/Enter activating it). `LostFocus` only cancels when focus actually leaves the host's
  subtree (it bubbles, so it also fires as focus shifts onto the host); Esc cancels explicitly.
- **`HotkeyDefaults` placed in `Amplify.Core`** (not App) so the default-fallback resolution is unit
  testable; it derives the defaults from a fresh `AppSettings()` rather than duplicating the VK
  codes, keeping one source of truth.
- **Tests:** Core-only (the test project references `Amplify.Core`, matching prior features). Cases
  cover canonical round-trips (incl. a single-key `f11`), modifier ordering,
  case/order/whitespace-tolerant parsing, invalid-input rejection, `vk{n}` fallback, display tokens,
  `TryCreate` validation (single key accepted, modifier-only / no-key rejected), modifier-VK
  identification, record equality (duplicate detection), and `HotkeyDefaults` fallback/override.
  Total suite 145 green. The hook service and WinUI capture path are Win32/UI and aren't unit-tested.
- **Manual/integration checks (still to do on a real machine):** global firing while another app is
  focused / window minimised; **pass-through** (the bound key still works in another app while also
  changing volume); single-key bindings incl. F12; rebinding + persistence across restart;
  non-US-layout keycap glyphs; the elevated-window (UIPI) limitation. CI is logic-only.
- **Verified facts:** `WH_KEYBOARD_LL` runs on the installing thread (needs a message loop) and
  `GetAsyncKeyState` is stale inside the callback; `LowLevelHooksTimeout` ≤1s; `MapVirtualKeyEx`
  MAPVK_VK_TO_CHAR vs `ToUnicodeEx` side-effects; `InputKeyboardSource.GetKeyStateForCurrentThread`
  is the WinUI 3 modifier-state API; Segoe Fluent `ECC8`/`ECC9` = AddTo/RemoveFrom. Build is
  `-p:Platform=x64` (the packaged app exposes no AnyCPU), `Nullable`/`TreatWarningsAsErrors` clean,
  `dotnet format` clean.

### Code-review follow-ups (same PR)

- **Hook registration state must stay single-threaded.** The hook callback enumerates the service's
  `_registered` map on the UI thread, so all registration mutations must too. `HotkeyRegistrar` now
  marshals its `ISettingsService.Changed` handling onto the UI dispatcher (mirroring
  `HotkeysViewModel`), since `SettingsService.Update` raises `Changed` synchronously on the caller's
  thread — a future off-UI-thread settings write (e.g. reset/disconnect) would otherwise race the
  hook callback. Documented the single-thread expectation on `IHotkeyService`.
- **Contract docs corrected for the hook backend.** `IHotkeyService` (and the `contracts.md` comment)
  previously promised `RegisterHotKey`-style cross-app conflict detection; reworded to "observed, not
  consumed — registration fails only if the shortcut mechanism can't be set up." The rebind failure
  message/key was renamed `Hotkey_Conflict_InUse` → `Hotkey_RegisterFailed` and reworded, since the
  only path that reaches it is a hook-install failure (all shortcuts down), not a per-combo conflict.
- **Declined:** extracting the two near-identical hotkey-row blocks in `MainPage.xaml` into a
  templated control — for two static rows the DP/template indirection costs more than the duplication
  saves, and the codebase consistently inlines such rows (`SettingsPage`). Revisit if a third row appears.
- **Recording no longer triggers the action (`IsSuspended` added to `IHotkeyService`).** The global
  hook is independent of the WinUI capture, so the combo pressed to *set* a binding also matched a live
  binding and fired its volume nudge. `HotkeysViewModel` now sets `IHotkeyService.IsSuspended` to the
  aggregate recording state. Suspend only mutes raising `HotkeyPressed`; the hook stays installed
  (pass-through unaffected). The held-key set also tracks keys pressed while suspended, so a combo
  still physically held when recording ends doesn't fire on auto-repeat until released. (Named
  `IsSuspended` rather than `Suspend()`/`Resume()` — `Resume` trips analyzer CA1716 as a VB keyword.)
  Contract updated in `contracts.md`.
