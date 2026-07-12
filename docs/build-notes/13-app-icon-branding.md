# Build notes — Feature 13: App Icon & Branding

> Append a new dated entry each time a session works on this feature. Add to the end; don't rewrite
> earlier entries.

## 2026-07-11 — Phase 1 (full build) · feat/13-app-icon-branding

Replaced the stock Visual Studio template icon/tile placeholders with the custom Amplify "rising
level" mark (four ascending pill bars, no container) — the only custom graphic in the app.

- **Master vector source:** committed `design/logo/amplify-icon.svg` (colour: monochrome-blue
  gradient + seating shadow + top-left highlight) and `amplify-icon-mono.svg` (solid mono), authored
  directly from the parametric `Bars` model in `design/project/Amplify Icon - Windows Fluent.html`
  (48-unit grid → 256 frame; bars width 32, radius 16, baseline y=218.667). Ink bounding box within
  the frame is 192×160 at (32, 58.667).

- **Asset generation — decision & why:** no SVG rasteriser (ImageMagick/Inkscape/rsvg) is installed
  on the machine, and there was no committed vector/ICO source to begin with. Rather than commit
  opaque binaries produced by an ad-hoc step, added a **reproducible dev-only generator**,
  `tools/IconGen` (SkiaSharp, MIT), that renders the whole raster matrix + `AppIcon.ico` from the
  same geometry/colours as the master SVG. It is **deliberately outside `Amplify.slnx` and not
  referenced by any shipped project**, so SkiaSharp never enters the app's dependency graph and the
  app's `dotnet test` / `TreatWarningsAsErrors` are unaffected. `tools/Directory.Build.props`
  (empty) stops the tool inheriting the repo-root strict-analyzer props (MSBuild stops at the first
  `Directory.Build.props` walking up). Regenerate with `dotnet run --project tools/IconGen`.

- **Asset matrix (verified against Microsoft Learn MRT docs):**
  - `Square44x44Logo.scale-{100,125,150,200,400}` → 44/55/66/88/176 px (plated app-list).
  - `Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated` (taskbar/Start/ALT+TAB), plus
    `_contrast-black` / `_contrast-white` high-contrast variants (solid mono mark) at each size.
  - `Square150x150Logo.scale-*` → 150/188/225/300/600 px; `StoreLogo.scale-*` → 50/63/75/100/200 px;
    `SplashScreen.scale-{100,200}` → 620×300 / 1240×600; `AppIcon.ico` embeds 16/24/32/48/64/128/256.
  - Manifest keeps the unqualified logical names (`Square44x44Logo.png`, `Square150x150Logo.png`,
    `StoreLogo.png`, `SplashScreen.png`); MRT resolves them to the qualified files at runtime.

- **Small-size simplification:** at ≤32 px the drop shadow and highlight are dropped (bars only) so
  the four bars stay distinct, per the feature doc's edge-case rule. Applied to targetsize 16/24/32
  and the small `.ico` frames.

- **Tile background:** kept `BackgroundColor="transparent"` (user-confirmed) — the mark is designed
  with no container and sits on the OS accent/theme plate. The blue gradient reads on both light and
  dark taskbars, so a single unplated set is shipped (no `altform-lightunplated`); high-contrast is
  handled by the `contrast-black/white` mono variants.

- **Deviations from the feature doc:**
  - Feature doc listed target-size **unplated 16/24/32/48/256**; shipped exactly that plus the
    high-contrast mono variants (the doc asked for a solid mono high-contrast variant — wired as
    `_contrast-*` qualifiers rather than a loose file).
  - **Removed** the placeholder `Wide310x150Logo` and `LockScreenLogo` assets and the manifest's
    `<uap:DefaultTile Wide310x150Logo=…>` line — the doc says wide/large Start tiles aren't needed
    for a tray utility, and `LockScreenLogo` was an unreferenced template leftover.

- **Consumers:** title bar (`MainWindow.xaml` → `Square44x44Logo.scale-200.png`) and tray
  (`TrayService.cs` → `AppIcon.ico`) already referenced these logical assets from features 01/08, so
  no code change was needed — they now render the real mark. The window/taskbar/exe icon comes from
  the MSIX package logos (no `AppWindow.SetIcon` P/Invoke).

- **csproj wiring:** replaced the explicit per-file `<Content Include>` list with `Assets\*.png` /
  `Assets\*.ico` globs so the checked-in asset set stays in sync with whatever `IconGen` emits
  (these extensions are not auto-included by the SDK for this project, so no duplicate-item errors).

- **Non-change checked:** the `Icon1` entry in `Strings/en-US/Resources.resw` (flagged as a possible
  stray) is part of the **standard ResX schema documentation comment**, not a real `<data>` resource
  — left untouched.

- **Manual/integration checks done:** rendered PNGs eyeballed at 44/32/150/256 px and the mono
  high-contrast variants (`Read` image preview) — mark is crisp, gradient/shadow/highlight correct,
  four bars distinct at small sizes. Packaged `dotnet build src/Amplify.App -c Debug -p:Platform=x64`
  succeeds with **0 warnings** (assets resolve; `resources.pri` regenerates from the qualified
  files). `dotnet test` green (221 passed). **Remaining manual check (needs an interactive desktop
  session):** confirm the mark in the live title bar and system tray on both a light and a dark
  taskbar, and on a Start tile.

- **Window HICON (follow-up, same session):** the taskbar *button* icon (MSIX logos) and the custom
  title-bar glyph (`TitleBar.IconSource`) were correct, but the **taskbar thumbnail preview**,
  Alt+Tab, and Task View read the window's own **HICON**, which was still the generic framework icon.
  Fixed in `MainWindow.ConfigureWindowChrome()` with `AppWindow.SetIcon(iconPath)` (needs a
  fully-qualified path to a `.ico` shipped as **Content** — it is, via the `Assets\*.ico` glob; the
  packaged app's `AppContext.BaseDirectory` is the install dir where `Assets\AppIcon.ico` lands).

- **Icon surfaces map (WinUI 3, packaged) — carry this forward:** there are several *independent*
  icon surfaces; don't assume one API covers all of them.
  - **Custom title-bar glyph** → `TitleBar.IconSource` (XAML).
  - **Thumbnail preview, Alt+Tab, Task View** → the window HICON → `AppWindow.SetIcon(...)`.
  - **Taskbar button + jump list (right-click) header** → drawn by the **Windows shell from the
    manifest `Square44x44Logo`** via the app's registered identity — **not** a runtime API.
    `AppWindow.SetTaskbarIcon(...)` only overrides the live button HICON and does **not** repaint the
    jump list header, so it was tried and reverted (the button already resolved from the manifest
    logo). Confirmed via Microsoft Learn / Q&A: the taskbar + jump list icon come exclusively from
    `Square44x44Logo`.
  - **Jump-list launch-entry icon — KNOWN GAP, deferred.** The right-click jump list's app-launch
    entry ("Amplify") shows **no icon**, while its "Pin to taskbar" / "Close window" rows do. This is
    *not* by design: verified live via desktop automation that both File Explorer and **Windows
    Terminal (a packaged MSIX app)** render an icon on that entry, so packaged apps are expected to.
    Investigation (all confirmed, none of which fixed it):
    - Deployed package is correct — `AppxManifest.xml` references `Square44x44Logo`, all scale +
      targetsize PNGs are present in the install folder, and `makepri dump` shows `resources.pri`
      indexes `Square44x44Logo.png` → every scale/targetsize candidate.
    - Not a cache issue: a full uninstall/reinstall, `ie4uinit.exe -show`, and deleting every
      `%LocalAppData%\...\Explorer\iconcache_*.db` + `IconCache.db` + Explorer restart did **not**
      populate it.
    - `SetIcon`/`SetTaskbarIcon` don't affect it (that entry isn't a live-HICON surface). Note the
      taskbar *button* looking correct proves little here — a running window's button can take the
      window HICON (which we set), so it doesn't confirm the shell resolves the package logo for the
      plated launch-entry/tile surfaces.
    - **Two untested suspects left**, both distinguishing Amplify from the working Terminal:
      (1) `BackgroundColor="transparent"` — documented to blank icons on shell surfaces that plate the
      logo; Terminal uses a solid colour; (2) the **loose/unsigned dev registration** (registered from
      `bin`), in which case a real signed install would be fine. Next step if revisited: flip
      `BackgroundColor` to a solid colour, redeploy, re-check; if still blank it's the dev-deploy
      artifact. Left blank for now by choice — cosmetic, single surface, everything else shows the mark.

- **Verified facts:** latest `SkiaSharp` on NuGet is 4.150.0 (used, with
  `SkiaSharp.NativeAssets.Win32`). MRT rule (Microsoft Learn): `scale-*` and `targetsize-*` can't be
  combined on one file, and at least one variant must be scale-based/unqualified — satisfied (scale
  set + separate targetsize set). Canvas px per scale: app-list 44/55/66/88/176, medium tile
  150/188/225/300/600, store 50/63/75/100/200.
