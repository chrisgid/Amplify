# Amplify

A **WinUI 3** desktop app for Windows 11 that controls **Spotify's** playback volume using
**global keyboard hotkeys**. The user signs in once with Spotify (OAuth PKCE; Premium required for
volume control), then raises/lowers volume system-wide via shortcuts while the app runs in the tray.

## Documentation

- **[Specification](docs/specification.md)** — whole-app overview, tech stack, architecture,
  engineering standards, Spotify API rules, and the feature index.
- **[Getting started](docs/getting-started.md)** — solution layout, exact SDK/package versions,
  config, and Spotify app registration. **Read before building.**
- **[Contracts](docs/contracts.md)** — canonical service interfaces, enums, models, events, and the
  full `AppSettings` shape. **Implement against these exactly.**
- **[Feature docs](docs/features)** — one detailed, self-contained document per feature
  (build these individually; each links back to getting-started + contracts).
- **[Integration & smoke test](docs/integration-smoke-test.md)** — Phase 2 end-to-end checklist:
  assemble all features and verify the app actually runs before release.
- **[Build notes](docs/build-notes/README.md)** — per-feature decision log (append-only); writing an
  entry is part of the definition of done. Read these when building a dependent feature.
- **[Design README](design/README.md)** — the Claude Design handoff bundle; HTML/CSS/JS mockups
  under [`design/project`](design/project) (visual reference only, not to be copied literally).

## Key facts

- The mockups are **reference only** — build with native WinUI controls and Segoe Fluent Icons;
  the Amplify logo is the only custom graphic.
- Default theme follows the user's Windows theme and accent colour.
- Spotify integration uses **Authorization Code with PKCE** and the Web API; see
  [specification §6](docs/specification.md#6-spotify-web-api-client-standards) for the binding rules.

## Working in this repo

- **Pre-implementation:** this repo is currently **docs + design only** — there is no solution or
  source yet. Features are built **one at a time in isolated sessions** against the docs above;
  the build order and phased plan are in
  [getting-started §8](docs/getting-started.md#8-build-order).
- **Definition of done** (every feature): the solution **builds** and `dotnet test` is **green**
  — never skip, weaken, or delete tests to pass — **and** a dated entry is appended to the
  feature's [build-notes](docs/build-notes/README.md) file. Strict by default:
  `Nullable=enable`, `TreatWarningsAsErrors=true`.
- **Verify, don't guess:** confirm WinUI 3 / Windows App SDK / .NET APIs with the
  `microsoft-docs:winui3` and `microsoft-docs` skills; take Spotify endpoints from the OpenAPI spec
  ([specification §6](docs/specification.md#6-spotify-web-api-client-standards)).
