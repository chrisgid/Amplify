# Feature 11 — Theming & Appearance

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md), [10 settings](./10-settings-persistence.md).

## Summary

Makes Amplify look native to Windows 11. By **default the app follows the user's Windows theme
(light/dark) and the Windows accent colour**, and updates live when the user changes them in
Windows. A manual override (System / Light / Dark) is available in Settings. Uses the **Mica**
backdrop and system theme resources rather than the prototype's hard-coded palette.

## User stories

- As a user, I want Amplify to match my Windows light/dark setting automatically.
- As a user, I want it to use my Windows accent colour like other built-in apps.
- As a user, I want to force Light or Dark if I prefer, independent of Windows.

## UX / behaviour

*Reference:* `design/project/components/settings.jsx` (App theme combo),
`components/app.jsx` (`accentVars` — illustrative only).

- **Default = system:** the window theme tracks the OS app theme and recolours instantly when
  Windows switches light/dark.
- **Accent:** controls and highlights use the **Windows accent colour** (system accent brushes),
  not a fixed palette. The prototype's 5-colour accent picker is **not** required; the OS accent
  is the source of truth. (If an in-app accent override is ever desired, treat it as optional and
  out of scope here.)
- **Manual theme override:** Settings → App theme = Use system / Light / Dark, applied
  immediately and persisted.
- **Backdrop:** Mica, consistent across themes.

## Acceptance criteria

- [ ] On first run with no override, the app matches the current Windows theme and accent.
- [ ] Changing Windows light/dark updates Amplify live (no restart).
- [ ] Changing the Windows accent colour is reflected in Amplify.
- [ ] The Settings override (System/Light/Dark) applies immediately and persists.
- [ ] All surfaces use theme-aware system brushes (no hard-coded colours that break in a theme).
- [ ] High-contrast themes render correctly (native controls handle this).

## Implementation guidance

- **Theme application:** drive `FrameworkElement.RequestedTheme` (or leave default to follow
  system) from an `IThemeService` based on the `ThemeMode` setting
  (`Default/System → follow OS`, `Light`, `Dark`).
- **Follow OS live:** observe system theme changes (e.g. `UISettings.ColorValuesChanged` /
  app theme listener) and reapply; verify the correct WinUI 3 mechanism via the
  `microsoft-docs:winui3` skill.
- **Accent:** use the built-in system accent brushes/theme resources
  (`AccentFillColorDefaultBrush`, `SystemAccentColor`, etc.) so the OS accent flows through
  automatically — do **not** re-implement the prototype's accent maths.
- **Backdrop:** `MicaBackdrop` on the window ([feature 01](./01-application-shell.md)).
- The prototype's CSS tokens are **reference for hierarchy/spacing only**; map to native theme
  resources.

## Data & persistence

- `ThemeMode` (System/Light/Dark) via [10 settings](./10-settings-persistence.md). Accent is not
  stored (sourced from the OS).

## Edge cases & error handling

- OS theme/accent changes while the window is hidden → applied when shown / on next event.
- Mica unsupported → graceful fallback ([feature 01](./01-application-shell.md)).
- Override = System should resume live OS-following immediately.

## Dependencies

- Applied by the shell ([01](./01-application-shell.md)); option stored in
  [10 settings](./10-settings-persistence.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- `IThemeService` maps `ThemeMode` → resolved theme correctly (System resolves to current OS
  theme; Light/Dark fixed).
- Setting change triggers reapply.
- (Live OS theme/accent following verified manually.)

## Out of scope

- Window/backdrop setup (feature 01) and the settings control wiring (feature 10).
- Any custom/in-app accent palette (intentionally not implemented; OS accent is used).

## Standards reminder

Follow the Windows theme **and** accent by default; system brushes only (no hard-coded palette);
native controls; verify theme-change APIs via Microsoft docs skills; concise code.
