# Build notes — Feature 08: System Tray & Background Operation

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-07-02 — Phase 1 (full build) · feat/08-system-tray-background

- **Deviations from spec/contracts:** none material. `ITrayService` was previously only declared in
  `contracts.md`; it is now implemented verbatim (`Initialize`/`ShowWindow`/`HideToTray`/`Quit`) in
  `Amplify.Core/Tray/ITrayService.cs`. Added a **Close-to-tray** toggle (`MinimizeToTrayOnClose`) to
  the Settings General section — the setting already existed in `AppSettings`, but the design
  reference (`settings.jsx`) only shows launch/start-minimized, so exposing it is an addition.
- **Contract changes:** none to `contracts.md`. Introduced a **feature-local** helper interface
  `IStartupTaskManager` (+ `StartupState` enum, `StartupTaskReconciler`, `TrayConstants`) in
  `Amplify.Core/Tray/`. It does not cross feature boundaries (only feature 08 uses it), so it lives in
  Core purely so the reconciliation logic is unit-testable without the platform `StartupTask` API — it
  was deliberately **not** added to `contracts.md`.
- **Assumptions / decisions:**
  - **Minimise detection** uses `Window.VisibilityChanged` (`Visible == false`) combined with
    `OverlappedPresenter.State == Minimized`, then `AppWindow.Hide()`. Chosen because the existing
    codebase already relies on `VisibilityChanged` firing on minimise (see `MainWindow`), and
    `AppWindow.Hide()` removes the taskbar button (satisfies "no taskbar presence while hidden").
    Close-to-tray leaves the presenter `Restored`, so it doesn't trip the minimise branch.
  - **Close-to-tray** intercepts `AppWindow.Closing` (`args.Cancel = true` + hide) unless a real Quit
    is in progress or `MinimizeToTrayOnClose` is off. **Quit** sets a flag so `Closing` doesn't cancel,
    disposes the tray icon, then `MainWindow.Close()` — which runs the shell's existing `Closed`
    handler that disposes the host (releasing the hotkey hook and HttpClient handlers).
  - **Start-minimized** is honoured in the shell's launch sequence: the tray initializer (Order 200)
    creates the icon, then `App.OnLaunched` skips `MainWindow.Activate()` when
    `StartMinimizedToTray` is set — the window is constructed but never shown.
  - **Launch-at-startup** is treated as OS-owned: the toggle calls `TryEnableAsync`/`DisableAsync` and
    reflects the *actual* resulting `StartupState` back into the toggle + settings (so a
    `DisabledByUser` flips it back off). At launch the tray initializer reconciles the OS state into
    `AppSettings.LaunchAtStartup` (`StartupTaskReconciler.ShouldPersist`).
  - **Single instance:** custom `Program.Main` (enabled via `DefineConstants` +=
    `DISABLE_XAML_GENERATED_MAIN`) decides redirection *before* the app/host is built, per the official
    WinUI pattern: `AppInstance.FindOrRegisterForKey` + `IsCurrent` + `RedirectActivationToAsync` on a
    thread-pool thread with a `CoWaitForMultipleObjects` non-pumping wait to avoid an STA deadlock.
    Re-activation is handled in `App` via `AppInstance.GetCurrent().Activated` →
    `ITrayService.ShowWindow` (marshalled to the UI thread).
  - **Manifest:** `windows.startupTask` `desktop:Extension` with `Enabled="true"` to match the
    `LaunchAtStartup = true` default; `TaskId="AmplifyStartupTask"` matches `TrayConstants.StartupTaskId`
    used by `StartupTask.GetAsync`; `Executable="Amplify.App.exe"`.
- **Deferred / known gaps:**
  - Single-instance redirect and all tray/window/minimise/startup behaviours require a **packaged**
    run and are **not** unit-tested (static `AppInstance`, WinUI window, real `StartupTask`). They are
    covered by the manual checklist in the plan and belong to the Phase 2 integration smoke test.
  - **The packaged manual smoke test has not yet been run in this build session** — the code compiles
    and unit tests are green, but the tray/hotkey/startup behaviours still need a hands-on packaged
    verification (see checklist below). Flagging so a later session/integration pass runs it.
  - Launch-at-startup toggle reflects OS reality but does not surface an explanatory message when the
    OS refuses to enable it (e.g. `DisabledByUser`); the toggle simply reverts. Acceptable for now.
- **Manual/integration checks:** unit tests green (`dotnet test`, 186 passing incl. new
  `StartupTaskReconcilerTests`); `dotnet build` clean under `TreatWarningsAsErrors`; `dotnet format`
  clean. Packaged behavioural checks (tray menu, minimise/close-to-tray, start-minimized,
  launch-at-startup ↔ Task Manager, second-instance surfacing) — **pending** a packaged run.
- **Verified facts:**
  - `H.NotifyIcon.WinUI` **2.4.1** resolves against `Microsoft.WindowsAppSDK` 2.2.1 (restored clean).
  - In H.NotifyIcon's default `ContextMenuMode.PopupMenu`, the native popup invokes each
    `MenuFlyoutItem.Command` (via `Command.TryExecute`), **not** `Click` — so tray menu items are wired
    with `Command`, and `TaskbarIcon.ForceCreate()` is required when created in code (confirmed against
    the library source).
  - `OverlappedPresenterState` = `{ Maximized, Minimized, Restored }`; `OverlappedPresenter.Restore()`
    and `AppWindow.Hide()`/`IsShownInSwitchers` confirmed via MS Learn.
  - Packaged desktop `StartupTask.RequestEnableAsync` shows **no** consent dialog; a user-disabled task
    reports `DisabledByUser` and cannot be re-enabled programmatically (MS Learn).
  - Gotcha: a C# compile error in `Program.cs` surfaced as a misleading XAML internal error
    (`WMC9999` / `WMC1509`); it disappeared once the C# error was fixed.

## 2026-07-02 — Phase 1 (follow-up: onboarding & manual-launch behaviour) · feat/08-system-tray-background

- **Deviations from spec/contracts:** none. Two behavioural refinements beyond the original acceptance
  criteria (requested during review):
  1. **Never use the tray during onboarding** (first run / post-reset). While
     `ShellViewModel.CurrentRoute == ShellRoute.Onboarding`, the tray icon is **hidden**, minimise does
     a normal taskbar minimise, and close **exits** (no close-to-tray). This is gated live off the shell
     route, so it re-enables the moment the user connects.
  2. **Manual launch ignores `StartMinimizedToTray`.** Only an automatic sign-in start honours it; a
     shortcut/Start/exe launch always shows the window.
- **Contract changes:** none. Added pure helper `LaunchWindowPolicy.ShouldStartHidden` in
  `Amplify.Core/Tray/` (feature-local, unit-tested) — `App.OnLaunched` now decides show-vs-hide through
  it instead of a raw `StartMinimizedToTray` check.
- **Assumptions / decisions:**
  - **Onboarding signal** = the shell route, not a separate auth flag — it's the single source for
    "the onboarding screen is showing" and stays aligned with what's on screen. Note `ShellRouter` only
    advances `Onboarding → Main` today and never back at runtime; returning to onboarding on reset is
    feature 12's job, at which point these route-based guards apply automatically.
  - **Chose to hide the tray icon during onboarding** (via `TaskbarIcon.Visibility`) rather than keep it
    and disable the Settings menu item. Both are viable (PopupMenu mode honours `MenuFlyoutItem.IsEnabled`),
    but a visible tray icon that the window won't minimise into is incongruous; hiding is more coherent.
  - **Auto-start vs manual** is read from the activation kind captured in `Program.Main`
    (`AppActivationArguments.Kind == ExtendedActivationKind.StartupTask`) and exposed as
    `Program.LaunchedAtStartup`. It must be captured there because `GetActivatedEventArgs` returns the
    args only on its **first** call for packaged apps (already called for single-instancing), and because
    the XAML `LaunchActivatedEventArgs` reports `Launch` unconditionally (MS Learn).
- **Deferred / known gaps:** the route-based tray guards and the activation-kind read are UI/OS-bound
  and only unit-tested at the policy level (`LaunchWindowPolicyTests`); the packaged behavioural checks
  (onboarding = no tray/normal minimise/close-exits; manual vs auto start) are still **pending** a
  packaged run, same as the earlier entry.
- **Manual/integration checks:** `dotnet build` clean, `dotnet test` green (194, +8 policy tests),
  `dotnet format` clean. Also updated [`integration-smoke-test.md`](../integration-smoke-test.md) with
  the matching Phase 2 checks (onboarding suppresses the tray persona; reset returns to it; start-minimized
  honoured only for an automatic sign-in start, not a manual launch).
- **Verified facts:** `TaskbarIcon.Visibility = Collapsed` removes the tray icon / `Visible` re-adds it,
  and PopupMenu mode copies `MenuFlyoutItem.IsEnabled` into the native menu (both confirmed against the
  H.NotifyIcon source). `ExtendedActivationKind.StartupTask` distinguishes an auto-start from a manual
  `Launch` (MS Learn).
