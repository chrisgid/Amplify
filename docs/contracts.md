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
> **A Free (non-Premium) account is also not a separate `ConnectionState`** — it is
> `Connected` + `Account.IsPremium == false`. Features 05/07/12 branch on `IsPremium` to show the
> "Free · Volume control unavailable" presentation and to gate the control. Connecting a Free
> account is a **success**, not a failure.

---

## Models

```csharp
// Spotify account shown in the status card (feature 05) and settings (feature 12).
public sealed record Account(
    string DisplayName,
    string Plan,          // e.g. "Premium"
    bool   IsPremium,     // from GET /v1/me -> product == "premium"
    string DeviceName,    // active device label, may be empty when none
    string Initials);     // derived from DisplayName for the avatar

// Current playback state (feature 07 reads volume/device; feature 05 reads device presence).
public sealed record PlayerState(
    bool   HasActiveDevice,
    int    VolumePercent,   // 0..100
    string? DeviceId,
    string? DeviceName,
    bool   IsPlaying);

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
    public bool NotifyOnVolumeChange { get; set; } = false;

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
    event EventHandler<AppSettings> Changed;
    Task LoadAsync();                           // load + migrate at startup
}

// feature 03 — OAuth/PKCE, token lifecycle
public interface IAuthService
{
    ConnectionState State { get; }
    Account? CurrentAccount { get; }
    event EventHandler<ConnectionState> ConnectionStateChanged;

    Task<bool> RestoreSessionAsync();           // silent, from stored refresh token
    Task<AuthResult> ConnectAsync();            // interactive PKCE flow
    Task<string> GetAccessTokenAsync();         // valid token, auto-refreshing
    Task DisconnectAsync();                      // clear tokens + account
}

// NotPremium == true does NOT imply failure: a Free account still connects (Success == true,
// NotPremium == true). Denied/Error are the failure cases.
public sealed record AuthResult(bool Success, bool Denied, bool NotPremium, string? Error);

// feature 07 — Spotify Web API (typed HttpClient, no SDK)
public interface ISpotifyClient
{
    Task<Account?> GetProfileAsync();           // GET /v1/me
    Task<PlayerState?> GetPlayerStateAsync();   // GET /v1/me/player
    Task SetVolumeAsync(int percent, string? deviceId = null); // PUT /v1/me/player/volume
}

// feature 06 — global hotkeys
public interface IHotkeyService
{
    void Register(HotkeyAction action, Hotkey combo);   // throws/returns false on conflict
    bool TryRegister(HotkeyAction action, Hotkey combo);
    void Unregister(HotkeyAction action);
    event EventHandler<HotkeyAction> HotkeyPressed;
}

// feature 07 — step math + orchestration; subscribes to IHotkeyService
public interface IVolumeController
{
    int Volume { get; }                          // last known 0..100
    bool CanControl { get; }                      // connected + Premium + HasActiveDevice
    Task SetVolumeAsync(int percent);
    Task NudgeAsync(int direction);              // +1 / -1 * step, clamped 0..100
    event EventHandler<int> VolumeChanged;        // for UI + notifications
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
}

// feature 09 — toasts
public interface INotificationService
{
    void ShowVolume(int percent, int direction);  // only when NotifyOnVolumeChange
}

// startup hooks invoked by the shell (feature 01) at launch — features implement as needed
public interface IStartupInitializer
{
    Task OnLaunchedAsync();
}
```

---

## Cross-feature events (who raises / who listens)

| Event | Raised by | Consumed by |
| --- | --- | --- |
| `IAuthService.ConnectionStateChanged` | 03 | 01 (routing), 04, 05, 12 |
| `IHotkeyService.HotkeyPressed` | 06 | 07 |
| `IVolumeController.VolumeChanged` | 07 | 07 UI, 09 (toast) |
| `ISettingsService.Changed` | 10 | 06 (re-register), 07 (step), 08 (startup/tray), 09, 11 (theme) |
| `IThemeService.ThemeChanged` | 11 | 01 (window) |

---

## Notes for implementers

- Implement against these interfaces; **do not** rename methods/events or change signatures
  without updating this file.
- Services are interface-first so they can be unit-tested with **NSubstitute** (or hand-written
  fakes). ViewModels depend on the interfaces, never on Win32/network directly.
- All Spotify request/response shapes must still be confirmed against the OpenAPI spec
  ([`specification.md` §6](./specification.md#6-spotify-web-api-client-standards)); the models here
  are app-facing projections, not the raw API DTOs.
