# Amplify — Shared Contracts

> Parent spec: [`specification.md`](./specification.md). **This file is the single source of truth
> for everything that crosses feature boundaries:** service interfaces, enums, models, events, and
> the full `AppSettings` shape. Because each feature may be built in a separate session, implement
> these **exactly** so the independently-built pieces compose. If a feature needs to change a
> contract, update it here first.

These types live in a shared **`Amplify.Core`** project (see
[`getting-started.md`](./getting-started.md) for the solution layout). Signatures are illustrative
C#; keep names and shapes stable, refine bodies as needed.

---

## Enums

```csharp
public enum ConnectionState
{
    Disconnected,   // no stored/usable session
    Connecting,     // restoring/refreshing/authorising
    Connected,      // token valid and Spotify reachable
    Error           // token/refresh failed or Spotify unreachable
}

public enum OnboardingPhase { Welcome, Authorizing, Verifying }   // feature 04

public enum ThemeMode { System, Light, Dark }                     // feature 11 (default System)

public enum HotkeyAction { VolumeUp, VolumeDown }                 // feature 06
```

> "No active device" is **not** a `ConnectionState` — it is `PlayerState.HasActiveDevice` (below).
> Feature 05 owns its messaging; feature 07 gates the volume control on it.
>
> **There is no Free-vs-Premium distinction in the app.** Amplify requires Premium, but the Web API
> no longer exposes subscription level (the `product` field was removed), and Spotify enforces
> Premium upstream — each user runs their **own** developer app, and a Development-mode app requires
> its owner to have active Premium. So a connectable user is always Premium; the app does not detect
> or branch on it. If a volume call is ever rejected (`403`), feature 07 surfaces that reactively.

---

## Models

```csharp
// Spotify account shown in the status card (feature 05) and settings (feature 12).
// Single source: IAuthService reads GET /v1/me during connect and exposes it via CurrentAccount —
// ISpotifyClient does NOT expose a separate profile call.
public sealed record Account(
    string DisplayName,
    string Initials);     // derived from DisplayName for the avatar
// Subscription level is NOT exposed by the Web API (the product field was removed) and Amplify
// always requires Premium, so there is no plan/IsPremium field.
// Active device label is NOT part of the profile — it comes from PlayerState.DeviceName
// (GET /v1/me/player); the status card composes the device label from it.

// Current playback state (feature 07 reads volume/device; feature 05 reads device presence).
public sealed record PlayerState(
    bool   HasActiveDevice,
    int    VolumePercent,   // 0..100
    string? DeviceName);    // active device label, null/empty when none

// A global shortcut (feature 06). Canonical string form e.g. "ctrl+alt+arrowup".
public sealed record Hotkey(
    KeyModifiers Modifiers,   // [Flags] None/Ctrl/Alt/Shift/Win
    uint Key)                 // Win32 virtual-key code
{
    public string ToCanonicalString();           // stable, persisted form
    public IReadOnlyList<string> ToDisplayTokens(); // e.g. ["Ctrl","Alt","↑"] for keycaps
    public static bool TryParse(string canonical, out Hotkey hotkey);
}

[Flags]
public enum KeyModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }
```

### `AppSettings` (owned by feature 10 — the **complete** shape)

```csharp
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = CurrentSchemaVersion; // first field in JSON
    public const int CurrentSchemaVersion = 1;

    // General (feature 08 / 09)
    public bool LaunchAtStartup { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;   // close-to-tray vs exit

    // First-run tray hint (feature 09) — internal one-shot state, NOT a user-facing toggle.
    // Set true after the "still running in the tray" hint is shown once; never shown again.
    public bool TrayHintShown { get; set; } = false;

    // Spotify (feature 03 / 04) — per-user Client ID entered at onboarding; not a secret.
    // Captured by onboarding, read by auth; cleared only by Reset (feature 12).
    public string SpotifyClientId { get; set; } = "";

    // Appearance (feature 11)
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    // Volume (feature 07)
    public int VolumeStep { get; set; } = 5;                  // 1..25, default 5

    // Hotkeys (feature 06) — canonical strings
    public string HotkeyVolumeUp { get; set; }   = "ctrl+alt+arrowup";
    public string HotkeyVolumeDown { get; set; } = "ctrl+alt+arrowdown";

    // Window state (feature 01, optional)
    public WindowState? Window { get; set; }
}

public sealed record WindowState(int Width, int Height, int X, int Y);
```

> Defaults above are the canonical defaults referenced by features 06/07/08/09/11.
> `SpotifyClientId` is the per-user, non-secret Client ID (feature 04 writes it, feature 03 reads
> it, feature 12 clears it on reset). Tokens (refresh token) are **never** in `AppSettings` — they
> live in the Credential Locker (feature 03).

---

## Service interfaces

Each service is implemented by its owning feature and registered via an `AddXxx()` DI extension
(see [`specification.md` §4](./specification.md#wiring--file-ownership-avoid-cross-feature-collisions)).

```csharp
// feature 10 — persistence + migration
public interface ISettingsService
{
    AppSettings Current { get; }
    T Get<T>(Func<AppSettings, T> selector);
    void Update(Action<AppSettings> mutate);   // mutates, persists (atomic), raises Changed
    void Reset();                               // restore all defaults, persist, raise Changed (feature 12)
    event EventHandler<AppSettings> Changed;
    Task LoadAsync();                           // load + migrate at startup
}

// feature 12 — coordinates a full reset back to first-run state: resets ISettingsService to defaults
// (clearing the Client ID + restoring default hotkeys/step) then IAuthService.DisconnectAsync(). The
// confirmation prompt is a UI concern; ResetAsync runs only after the user has confirmed.
public interface IResetService
{
    Task ResetAsync();
}

// feature 03 — OAuth/PKCE, token lifecycle
public interface IAuthService
{
    ConnectionState State { get; }
    Account? CurrentAccount { get; }
    event EventHandler<ConnectionState> ConnectionStateChanged;

    Task<bool> RestoreSessionAsync();           // silent, from stored refresh token
    Task<AuthResult> ConnectAsync(CancellationToken cancellationToken = default); // interactive PKCE flow
    Task<string> GetAccessTokenAsync();         // valid token, auto-refreshing
    Task DisconnectAsync();                      // clear tokens + account
}

// Denied (user declined consent) and a non-null Error are the failure cases. A caller-cancelled
// attempt (cancellationToken) is also surfaced this way — Success: false, Denied: false, with an
// Error describing the cancellation — rather than throwing OperationCanceledException, so callers
// don't need a try/catch for the common "user gave up waiting" case.
public sealed record AuthResult(bool Success, bool Denied, string? Error);

// feature 07 — Spotify Web API (typed HttpClient, no SDK)
public interface ISpotifyClient
{
    Task<PlayerState?> GetPlayerStateAsync();   // GET /v1/me/player
    Task SetVolumeAsync(int percent);           // PUT /v1/me/player/volume (active device)
}

// Shared single source of player state (active device + volume). One App-layer implementation polls
// GET /v1/me/player on a timer (while connected + window visible) and on demand; the status card (05)
// and the volume controller (07) both observe it so there is exactly one set of polling requests.
public interface IPlayerStateProvider
{
    PlayerState? Current { get; }                    // last reading, null when unread/disconnected
    event EventHandler<PlayerState?> PlayerStateChanged;
    Task RefreshAsync();                             // read now (e.g. on focus); coalesces overlaps
}

// feature 06 — global hotkeys
// Shortcuts are observed, not consumed (a bound key still reaches the foreground app), so the
// service does not detect cross-app conflicts; Register/TryRegister fail only if the shortcut
// mechanism can't be set up. Not thread-safe — call registration from one thread (the UI thread).
public interface IHotkeyService
{
    void Register(HotkeyAction action, Hotkey combo);   // replaces existing; throws if it can't be set up
    bool TryRegister(HotkeyAction action, Hotkey combo); // false only if it can't be set up
    void Unregister(HotkeyAction action);
    bool IsSuspended { get; set; }   // when true, HotkeyPressed isn't raised (e.g. while recording a new combo)
    event EventHandler<HotkeyAction> HotkeyPressed;
}

// feature 07 — step math + orchestration; subscribes to IHotkeyService
public interface IVolumeController
{
    int Volume { get; }                          // last known 0..100
    bool CanControl { get; }                      // connected + HasActiveDevice
    Task SetVolumeAsync(int percent);
    Task NudgeAsync(int direction);              // +1 / -1 * step, clamped 0..100. When !CanControl but
                                                 // connected, does one throttled on-demand read first to
                                                 // catch a device that became active while polling was
                                                 // suspended (window minimised); no-op if still none.
    Task RefreshAsync();                          // re-read player state (called on connect/focus)
    event EventHandler<int> VolumeChanged;        // for UI
    event EventHandler? StateChanged;             // CanControl changed without a volume change
}

// feature 11 — theme/accent
public interface IThemeService
{
    void Apply(ThemeMode mode);                  // System => follow OS live
    event EventHandler ThemeChanged;
}

// feature 08 — tray, window visibility, single instance, startup task
public interface ITrayService
{
    void Initialize();
    void ShowWindow();
    void HideToTray();
    void Quit();
    event EventHandler HiddenToTray;                        // raised each time the window hides to
                                                            // the tray (minimise or close-to-tray)
    void ShowTrayNotification(string title, string message); // balloon via the tray TaskbarIcon
}

// feature 09 — first-run tray hint (one-time "still running in the tray" notification)
public interface INotificationService
{
    // Subscribes to ITrayService.HiddenToTray. The first time the window hides to the tray,
    // shows the hint via ITrayService.ShowTrayNotification and sets AppSettings.TrayHintShown.
    // No-op once TrayHintShown is true. Wired at startup (IStartupInitializer).
    void ShowFirstMinimizeHintIfNeeded();
}

// startup hooks invoked by the shell (feature 01) at launch — features implement as needed.
// The shell resolves all registered IStartupInitializer instances and runs them **in ascending
// Order**, AFTER it has loaded settings (ISettingsService.LoadAsync) and restored the session
// (IAuthService.RestoreSessionAsync) as explicit pre-steps. Lower Order runs earlier. Use the
// bands below so independently built features slot in deterministically:
//   100  theme            (apply before the first frame — feature 11)
//   200  tray + window     (single-instance already handled pre-window; feature 08)
//   400  hotkeys           (register after settings are loaded — feature 06)
//   900  everything else
public interface IStartupInitializer
{
    int Order { get; }                           // ascending; see bands above
    Task OnLaunchedAsync(CancellationToken ct);
}
```

---

## Cross-feature events (who raises / who listens)

| Event | Raised by | Consumed by |
| --- | --- | --- |
| `IAuthService.ConnectionStateChanged` | 03 | 01 (routing), 04, 05, 12 |
| `IHotkeyService.HotkeyPressed` | 06 | 07 |
| `IPlayerStateProvider.PlayerStateChanged` | 07 (shared provider) | 05 (status card), 07 (volume) |
| `IVolumeController.VolumeChanged` | 07 | 07 UI |
| `ISettingsService.Changed` | 10 | 06 (re-register), 07 (step), 08 (startup/tray), 11 (theme) |
| `IThemeService.ThemeChanged` | 11 | 01 (window) |
| `ITrayService.HiddenToTray` | 08 | 09 (first-run tray hint) |

---

## Notes for implementers

- Implement against these interfaces; **do not** rename methods/events or change signatures
  without updating this file.
- Services are interface-first so they can be unit-tested with **NSubstitute** (or hand-written
  fakes). ViewModels depend on the interfaces, never on Win32/network directly.
- All Spotify request/response shapes must still be confirmed against the OpenAPI spec
  ([`specification.md` §6](./specification.md#6-spotify-web-api-client-standards)); the models here
  are app-facing projections, not the raw API DTOs.
