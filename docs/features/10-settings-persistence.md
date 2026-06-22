# Feature 10 — Settings & Persistence

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md) · Required by: 06, 07, 08, 09, 11, 12.

## Summary

The Settings screen and the underlying persistence layer (`ISettingsService`) that every other
feature reads and writes. It groups all user preferences — general (startup/tray/notifications),
appearance (theme), volume (step size), account, and reset — and persists them durably across
restarts. As the first foundational feature with a real, content-heavy screen, it also **stands up
the app's shared `Resources.resw`** (localized strings) that every later screen draws from
(see [Implementation guidance](#implementation-guidance) and
[spec §4](../specification.md#wiring--file-ownership-avoid-cross-feature-collisions)).

## User stories

- As a user, I want one place to configure how Amplify behaves.
- As a user, I want my preferences to be remembered when I reopen the app.

## UX / behaviour

*Reference:* `design/project/components/settings.jsx`.

- A scrollable Settings page with a back affordance to Main, grouped into sections. Prefer the
  Windows Community Toolkit **`SettingsCard`/`SettingsExpander`** pattern for a native look.
- **General**
  - *Launch at startup* — toggle ([feature 08](./08-system-tray-background.md)).
  - *Start minimized to the tray* — toggle ([feature 08](./08-system-tray-background.md)).
  - *Notify on volume change* — toggle ([feature 09](./09-notifications.md)).
- **Appearance**
  - *App theme* — `ComboBox`: Use system / Light / Dark
    ([feature 11](./11-theming-appearance.md)).
- **Volume**
  - *Volume step size* — `Slider` 1–25% with the current value shown
    ([feature 07](./07-volume-control.md)).
- **Account** — shows connected account or "Not connected"; Disconnect/Reconnect
  ([feature 12](./12-reset-and-account.md)).
- **Spotify Client ID** — a **read-only** field showing the stored `SpotifyClientId`, with
  sub-text "From your Spotify Developer app. Reset Amplify to change it." It is captured during
  onboarding ([feature 04](./04-onboarding.md)) and changed only via Reset
  ([feature 12](./12-reset-and-account.md)).
- **Reset** — "Reset Amplify" (danger) ([feature 12](./12-reset-and-account.md)).
- Footer: "Amplify {version} · Not affiliated with Spotify", where the **"Amplify" name is a
  hyperlink** to the GitHub repo (`https://github.com/chrisgid/Amplify`) — use a `HyperlinkButton`/
  `Hyperlink`; the version and "Not affiliated with Spotify" remain plain text.

## Acceptance criteria

- [ ] All listed settings are present, use native controls, and apply immediately on change.
- [ ] Settings persist across restarts and are loaded at startup before features initialise.
- [ ] Changing a setting notifies interested features (e.g. theme, step size, startup) live.
- [ ] Defaults match the prototype (notifications off; tray/startup on; step 5%; theme = system).
- [ ] Back navigation returns to Main.
- [ ] Stands up the shared **`Resources.resw`** and the `x:Uid`/`ResourceLoader` plumbing; the
      Settings screen's user-facing strings are sourced from it via `Settings_*`-prefixed keys (no
      hard-coded UI text).

## Implementation guidance

- **`ISettingsService`** — typed accessors (e.g. `T Get<T>(key, default)`, `Set<T>(key, value)`)
  plus a change notification (event or `IObservable`) so ViewModels react. Back it with a **JSON
  file at `ApplicationData.Current.LocalFolder\settings.json`** (backups + logs alongside in the
  same `LocalFolder`), serialised with `System.Text.Json`. Keep it injectable so it can be faked
  in tests. The canonical `ISettingsService` signature and `AppSettings` shape are defined in
  [`../contracts.md`](../contracts.md).
- **Model:** a strongly-typed `AppSettings` record/POCO is recommended over loose keys
  (`LaunchAtStartup`, `StartMinimizedToTray`, `NotifyOnVolumeChange`, `SpotifyClientId`,
  `ThemeMode`, `VolumeStep`, hotkey bindings, optional window state). Merge missing keys with
  defaults on load.
- **Schema versioning & migration:** the file carries a `schemaVersion` integer (first field) and
  the app knows the current version as a constant. On load, compare them:
  - **equal** → load normally.
  - **older file** → run a chain of **sequential, single-hop migrators** (`v1→v2`, `v2→v3`, …) in
    order until the file reaches the current version, then deserialise. Each release adds exactly
    one new migrator and never edits older ones.
  - **newer file** (e.g. the user downgraded the app) → do **not** attempt to parse the unknown
    shape: back the file up and **start from defaults** (option A — simplest and safest for a small
    utility).
  - Migrators operate on a tolerant JSON tree (`JsonNode`/`JsonObject`) so they can rename, move,
    split or retype fields before the final typed deserialisation. **Additive-only** changes (a new
    optional field) need **no** migrator — the default-merge above covers them; write a migrator
    only for structural changes.
  - **Safety:** before writing a migrated/replaced file, copy the original to a backup (e.g.
    `settings.v{old}.bak`); migrate once and persist immediately (atomic write-then-rename); never
    surface a migration error to the UI — log it, keep the backup, and fall back to defaults so the
    app always launches.
- **`SettingsViewModel`** exposes bound properties that read/write the service and trigger the
  relevant feature service (theme, startup task, etc.).
- Sensitive data (tokens) is **not** stored here — it lives in the Credential Locker
  ([feature 03](./03-spotify-authentication.md)).
- Use native controls only (`ToggleSwitch`, `ComboBox`, `Slider`, `SettingsCard`); `FontIcon`
  glyphs for section/row icons.
- **Shared localized resources (owned here):** create the app's single default-language
  **`Resources.resw`** and the `x:Uid` / `ResourceLoader` access convention — the one shared string
  file every screen uses ([spec §4](../specification.md#wiring--file-ownership-avoid-cross-feature-collisions),
  [§5](../specification.md#5-design-principles--engineering-standards)).
  [Feature 01](./01-application-shell.md) deferred this (its slice strings were throwaway and not
  runtime-verifiable); 10 stands it up because its Settings screen is the first real,
  runtime-verifiable surface. Migrate **this feature's own** Settings strings in first, namespacing
  keys with a **`Settings_`** prefix. Later features add their own prefixed keys (`Onboarding_*`,
  `Status_*`, `Volume_*`, …) to the **same** file — do **not** fork per-feature `.resw` files. Keep
  the shell's one durable string (the "Amplify" brand/title) working as you wire this up.

## Data & persistence

- Single settings store owned here; all features depend on it. Provide atomic, safe writes
  (write-then-rename) to avoid corruption.
- The store includes a `schemaVersion` field driving the migration logic above. Before any
  migration or defaults-reset, the prior file is backed up (`settings.v{old}.bak`) for recovery.

## Edge cases & error handling

- Corrupt/missing settings file → fall back to defaults and rewrite a clean file; log.
- Unknown/legacy keys → ignore or migrate; never crash on deserialisation.
- **Older `schemaVersion`** → migrate in place (sequential migrators), back up the original, and
  persist the upgraded file once; the user sees no error and keeps all preserved settings.
- **Newer `schemaVersion`** (downgrade) → back up and reset to defaults rather than risk parsing an
  unknown shape.
- **Failed migration** → log, retain the backup, start from defaults so the app still launches.
- Concurrent writes (e.g. multiple toggles) → serialise writes.

## Dependencies

- Hosted in the shell ([01](./01-application-shell.md)). Consumed by
  [06](./06-global-hotkeys.md), [07](./07-volume-control.md),
  [08](./08-system-tray-background.md), [09](./09-notifications.md),
  [11](./11-theming-appearance.md), [12](./12-reset-and-account.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Round-trip: set values → persist → reload yields the same values.
- Missing file / missing keys → defaults applied; corrupt file → recovers to defaults.
- Change notifications fire on `Set`.
- Defaults match the documented values.
- **Migration:** an older-version file migrates to the current `AppSettings` correctly; a
  multi-hop chain (e.g. v1→v3) runs migrators in order; a newer-version file falls back to defaults;
  a backup is written before migrating/resetting; a failing migrator results in clean defaults
  rather than a crash.

## Out of scope

- The behaviour each setting drives (covered by the linked features).

## Standards reminder

Native settings controls (`SettingsCard`, `ToggleSwitch`, `ComboBox`, `Slider`) + Fluent icons;
follow Windows theme; never store tokens here; concise, testable service.
