# Feature 01 — Application Shell & Window

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on: none ·
> Required by: every other feature.

## Summary

The application shell is the single main window that hosts all of Amplify's screens. It provides
the WinUI 3 window with a **Mica** backdrop, a **custom title bar** (Amplify logo + title +
native caption buttons), and an in-window navigation mechanism that swaps between the three
top-level screens: **Onboarding**, **Main**, and **Settings**. It also wires up the app's
host/DI container, theme application, and single-window lifecycle.

## User stories

- As a user, I want a small, native-looking window that feels like part of Windows 11.
- As a user, I want the window to remember a sensible size/position and behave like other apps
  (minimise, maximise/restore, close).
- As a user, I want screens to transition smoothly without the window resizing jarringly.

## UX / behaviour

- **Window:** compact, roughly **480px** content width (the prototype's width); height fits
  content up to a max, with the content area scrolling when needed. Resizable within sensible
  min/max bounds. Mica backdrop behind a translucent surface.
  *Reference:* `design/project/Amplify.html` (`.amp-window`, `.titlebar`, `.amp-scroll`).
- **Custom title bar:** left-aligned **Amplify logo** (the only custom icon) + the text
  "Amplify"; native **minimise / maximise / close** caption buttons on the right via the system,
  not hand-drawn. Title bar is draggable.
- **Navigation/routing:** three screens — `Onboarding`, `Main`, `Settings`. On launch, route to
  **Onboarding** if not connected, otherwise **Main**. Settings is reached from Main and returns
  via a back affordance. Use a `Frame` + navigation, or a bound `ContentControl`/`DataTemplate`
  switch driven by a `ShellViewModel.CurrentRoute`.
- **Transitions:** subtle entrance transitions (use built-in `NavigationThemeTransition` /
  `EntranceThemeTransition`) instead of the prototype's bespoke CSS keyframes.

## Acceptance criteria

- [ ] App launches to a single Mica window with a custom title bar showing the logo + "Amplify".
- [ ] Native caption buttons work (minimise, maximise/restore, close) and the title bar is
      draggable; content is correctly inset so it doesn't overlap caption buttons.
- [ ] The shell shows Onboarding when not connected and Main when connected.
- [ ] Navigating Main → Settings → back works and preserves state.
- [ ] Content scrolls when it exceeds the window height; the title bar stays fixed.
- [ ] Window respects the active theme (see [feature 11](./11-theming-appearance.md)).

## Implementation guidance

- **Project:** WinUI 3, packaged (MSIX), .NET 10. App bootstrap configures the
  `Microsoft.Extensions.Hosting` host and registers all services + ViewModels in DI.
- **Window/title bar:** use `Window.ExtendsContentIntoTitleBar = true` and set a custom
  `TitleBar` region; obtain the `AppWindow` for sizing/min-max and presenter config. Verify the
  current title-bar API with the `microsoft-docs:winui3` skill (`AppWindowTitleBar` vs
  `TitleBar` control) before implementing.
- **Backdrop:** `SystemBackdrop = new MicaBackdrop()`. Fall back gracefully if unsupported.
- **Routing:** a `ShellPage` with a `Frame`, or a `ContentControl` whose content is selected by
  the active route enum. Prefer `Frame` if you want built-in back-stack behaviour for Settings.
- **DI:** `ShellViewModel` receives `ISettingsService`, `IAuthService`, `IThemeService`,
  `ITrayService`; decides the initial route from auth state.
- **Logo:** ship the Amplify logo as the one custom asset (see
  [feature 13](./13-app-icon-branding.md)); everything else is `FontIcon` (Segoe Fluent Icons).

## Data & persistence

- Optionally persist window size/position via `ISettingsService`
  ([feature 10](./10-settings-persistence.md)). Initial route is derived from auth state, not
  stored.

## Edge cases & error handling

- Backdrop/Mica unavailable on the OS → fall back to a solid themed background.
- Very small screens → enforce a minimum window size and rely on the scrollable content area.
- Launching while already running → defer to single-instance handling in
  [feature 08](./08-system-tray-background.md) (surface the existing window).

## Dependencies

- Foundation for all features. Coordinates with [10 settings](./10-settings-persistence.md)
  (window state, initial route inputs), [11 theming](./11-theming-appearance.md), and
  [08 tray](./08-system-tray-background.md) (show/hide, single instance).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- `ShellViewModel` initial-route logic: not-connected → Onboarding; connected → Main.
- Navigation commands update `CurrentRoute` and the back action returns to Main.
- (UI/title-bar behaviour is validated manually.)

## Out of scope

- The contents of each screen (covered by their own feature docs).
- Tray/background behaviour and startup (feature 08).

## Standards reminder

Native WinUI controls + Segoe Fluent Icons only (logo excepted); follow the Windows theme by
default; keep code concise; verify WinUI APIs via the Microsoft docs skills.
