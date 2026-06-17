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

*Reference:* `design/project/Amplify Icon Directions.html`.

**Chosen direction:** the **"Current" mark** — a **speaker with concentric sound waves inside a
circular badge**. This is a standard, widely-used volume motif that clearly communicates what
Amplify does. Use the blue gradient from the design (`#3aa0ff → #0061cf`) as the brand colour.

The design file's source for this mark (`IconCurrent`): a circle filled with the blue gradient,
a white speaker glyph, and two white concentric arcs (sound waves) — see
`design/project/Amplify Icon Directions.html`.

The other explorations in the design file (Amplitude bars, Boost arrow, Keycap) are **not** being
used and can be ignored.

## Acceptance criteria

- [ ] A single custom logo is used app-wide; no other custom iconography exists.
- [ ] The logo is the speaker + concentric waves in a circular badge, using the blue gradient.
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
- Render the circular badge (speaker + concentric waves) with the brand gradient, centred on the
  square tile/icon assets with appropriate padding.
- Tray icon ([08](./08-system-tray-background.md)) uses the same mark at small size; ensure it
  reads on both light and dark taskbars.

## Data & persistence

- None. Static assets shipped with the app.

## Edge cases & error handling

- Small sizes (16px) must remain legible — simplify detail in the smallest target-size assets.
- Light vs dark taskbar/tray contrast — verify the mark is visible on both.

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
