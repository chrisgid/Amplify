# Build notes

A short **decision log per feature**, written by the Claude Code session(s) that build it. Because
each feature is built in an isolated, stateless session, these notes are how decisions travel
forward — to the session that builds a *dependent* feature, to the Phase 2 integration pass, and to
a human maintainer.

## Rule

- **One file per feature, named to match its feature doc** —
  `features/07-volume-control.md` → `build-notes/07-volume-control.md`.
- **Append-only.** Each session that touches a feature **adds a new dated entry** (creating the file
  on first touch). Add to the end; **never overwrite or rewrite earlier entries**. A feature touched
  in Phase 0 (sliver) and again in Phase 1 (completion) ends up with two entries in one file — no
  filename collisions.
- The **Phase 2 integration pass** is not a feature; it logs to its own file,
  `build-notes/integration-smoke-test.md`.
- Writing the entry is part of the **definition of done**
  ([spec §5](../specification.md#5-design-principles--engineering-standards)).

## What to record (and what not to)

Record only the **non-obvious** — things a future session can't recover from the code or git diff:

- **Deviations** from the spec/contracts, and *why*.
- **Assumptions** made where the docs were silent.
- **Contract changes** — link the `contracts.md` edit (the contract is updated there first; the note
  just points to it).
- **Deferred / known gaps** — TODOs, Phase 0 bits left for Phase 1, edge cases not handled.
- **Manual / integration checks** done by hand (CI is logic-only, so this is the only record).
- **Verified facts** — API/version/behaviour confirmed via the Microsoft docs skills, worth not
  re-deriving (e.g. a package version that resolves, how a fiddly WinUI API actually behaves).

Do **not** restate what the diff already shows ("added a ViewModel, wired DI") — that's noise.

## Template

```markdown
# Build notes — Feature 07: Volume Control

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-12 — Phase 0 (sliver) · <commit/PR>
- **Deviations from spec/contracts:** … (or "none")
- **Contract changes:** link to contracts.md edit (or "none")
- **Assumptions:** …
- **Deferred / known gaps:** …
- **Manual/integration checks:** …
- **Verified facts:** …

## 2026-06-18 — Phase 1 (full build) · <commit/PR>
- … (same fields; what changed since Phase 0)
```
