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
- **[Design README](design/README.md)** — the Claude Design handoff bundle; HTML/CSS/JS mockups
  under [`design/project`](design/project) (visual reference only, not to be copied literally).

## Key facts

- The mockups are **reference only** — build with native WinUI controls and Segoe Fluent Icons;
  the Amplify logo is the only custom graphic.
- Default theme follows the user's Windows theme and accent colour.
- Spotify integration uses **Authorization Code with PKCE** and the Web API; see
  [specification §6](docs/specification.md#6-spotify-web-api-client-standards) for the binding rules.
