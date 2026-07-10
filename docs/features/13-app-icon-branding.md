# Feature 13 — App Icon & Branding

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on: none ·
> Used by: 01 (title bar), 08 (tray), packaging.

## Summary

Defines Amplify's visual identity — the **app logo/icon** — which is the **only custom graphic**
in the product (everything else uses Segoe Fluent Icons). Covers the chosen icon direction and
the asset sizes/formats needed for the title bar, system tray, taskbar, and MSIX tiles.

## User stories

- As a user, I want a recognisable Amplify icon in the title bar, tray, taskbar, and Start.
- As the developer, I want a clear icon direction and a complete asset set.

## Brand & icon direction

*Reference:* `design/project/Amplify Icon - Windows Fluent.html`.

**Chosen direction:** the **"rising level" mark** — a **free-form silhouette of four flat,
pill-ended bars ascending left→right** (shortest/quietest → tallest/loudest). It's a single literal
metaphor for *getting louder = amplify*. Deliberately **no container**: no circle, no speaker
glyph, no curved sound waves, no typography — which also keeps it **clear of Spotify's protected
circular badge and waves** (a branding-compliance win, not just aesthetics).

- **Colour:** one monochrome-blue gradient lit from the top-left (~120°), held to a small ramp so
  it stays clean when scaled down: **`#5BB4FF` (light) → `#1E8AF0` (mid) → `#0A5BD6` (dark)**. A
  soft seating drop-shadow (`#062a63`, ~30% opacity) and a top-left white highlight edge give it
  depth so it sits naturally in the Windows shell.
- **Construction:** authored on a **48-unit grid scaled into a 256 viewBox** (k = 256/48). Bars sit
  within a **4px keyline margin**; corners use a 2px exterior / 1px interior radius (at 48×48), and
  the pill ends scale from that rule. See `design/project/Amplify Icon - Windows Fluent.html`
  (`AmplifyIcon`) for the exact geometry.
- **High-contrast / monochrome variant:** the silhouette is strong enough to drop to a **solid
  fill** — **black on light, white on dark** — with no gradient (`AmplifyMono` in the design file),
  for Windows high-contrast modes.

## Acceptance criteria

- [ ] A single custom logo is used app-wide; no other custom iconography exists.
- [ ] The logo is the free-form rising-bars silhouette (four ascending pill bars, no container),
      using the monochrome-blue gradient.
- [ ] Icon is legible at title-bar/tray sizes (16px) through to large tiles.
- [ ] A complete MSIX asset set is generated (see below) and wired into the manifest.
- [ ] The logo appears in the title bar ([01](./01-application-shell.md)) and tray
      ([08](./08-system-tray-background.md)).

## Implementation guidance

- Produce the master icon as **SVG**, then export the required raster assets.
- **MSIX / packaging assets** (Square44x44Logo, Square150x150Logo, StoreLogo, plus scale variants
  `100/125/150/200/400` and target-size unplated 16/24/32/48/256 for the title-bar/taskbar) and a
  multi-resolution **`.ico`** for the window/tray. The wide/large Start tiles (`Wide310x150Logo`,
  `Square310x310Logo`) are **not needed** for a tray utility — skip them unless wide/large tiles are
  ever wanted. Verify the exact asset matrix via the `microsoft-docs:winui3`/`microsoft-docs`
  skills.
- Render the free-form rising-bars silhouette (no container) with the monochrome-blue gradient,
  seating shadow, and top-left highlight, sized within the 4px keyline margin on the square
  tile/icon assets. Ship the **solid monochrome** variant (black on light / white on dark) for
  Windows high-contrast modes.
- Tray icon ([08](./08-system-tray-background.md)) uses the same mark at small size; ensure it
  reads on both light and dark taskbars. At the smallest sizes (16/24px), drop the shadow/highlight
  and simplify so the four bars stay distinct.

## Data & persistence

- None. Static assets shipped with the app.

## Edge cases & error handling

- Small sizes (16px) must remain legible — simplify detail (drop shadow/highlight) in the smallest
  target-size assets so the four bars stay distinct.
- Light vs dark taskbar/tray contrast — verify the mark is visible on both; use the solid
  monochrome variant for high-contrast modes.

## Dependencies

- Consumed by [01 shell](./01-application-shell.md) and
  [08 tray](./08-system-tray-background.md), and by MSIX packaging.

## Testing

> Where a feature has unit tests, they must pass and must not be disabled, skipped, or weakened to
> complete it — see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Not unit-testable. Verify visually at all required sizes and in light/dark, and that the
  manifest references valid assets (build succeeds, icon renders in taskbar/Start/tray).

## Out of scope

- All in-UI iconography (use Segoe Fluent Icons — no custom glyphs).

## Standards reminder

The logo is the **only** custom graphic; everything else is Segoe Fluent Icons. Verify the MSIX
asset matrix via Microsoft docs skills.
