# Feature 02 — CI: PR Tests

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md) (a buildable solution + test project) ·
> **Build this second**, right after the shell, so it guards every feature merged afterward.

## Summary

A **GitHub Actions** workflow that builds the solution and runs the **unit tests on every pull
request** (and on pushes to the default branch). It enforces the "all unit tests pass" definition
of done from [spec §5](../specification.md#5-design-principles--engineering-standards), so broken
changes can't merge. Built early — right after [feature 01](./01-application-shell.md) scaffolds
the solution and test project — so it's working throughout the rest of the project.

## User stories

- As a maintainer, I want every PR built and tested automatically so a red build blocks merge.
- As a contributor, I want fast feedback that my change compiles and passes tests.

## Behaviour

- **Workflow:** `.github/workflows/ci.yml`.
- **Triggers:** `pull_request`, and `push` to the default branch.
- **Runner:** `windows-latest` (WinUI 3 / Windows App SDK needs Windows).
- **Steps:** checkout → setup .NET 10 SDK → `dotnet restore` → `dotnet build -c Release` →
  `dotnet test -c Release`.
- **Scope:** runs **unit tests only**. Tests that need a live Spotify account, Premium, an active
  device, or the UI are tagged (e.g. a `RequiresSpotify`/`Integration` trait) and **excluded** from
  CI (see [spec §5](../specification.md#5-design-principles--engineering-standards)).
- **Gate:** the workflow is a **required status check** for merging.

## Acceptance criteria

- [ ] Opening or updating a PR triggers a Windows CI run that builds and runs the unit tests.
- [ ] A failing build or unit test marks the check red and blocks merge.
- [ ] Integration/`RequiresSpotify` tests are excluded from the CI run.
- [ ] The workflow is required for merge to the default branch.
- [ ] Runs complete reasonably fast (NuGet caching enabled).

## Implementation guidance

- Use `actions/checkout` and `actions/setup-dotnet` (pin **.NET 10**). Enable NuGet caching
  (`setup-dotnet` cache or `actions/cache`) to keep PR runs quick.
- Exclude integration tests via a filter, e.g. `dotnet test --filter "Category!=RequiresSpotify"`
  (match the trait/category used by the test projects).
- Keep build settings (TFM, analyzers, `TreatWarningsAsErrors`) in `Directory.Build.props` so CI
  and local builds behave identically (see [getting-started.md](../getting-started.md)).
- This can be authored as soon as the solution + `Amplify.Tests` project exist (even with a single
  smoke test); it then guards all subsequent feature work.

## Data & persistence

- None. Produces CI run results / status checks.

## Edge cases & error handling

- Compile error or failing unit test → workflow fails red; no merge.
- Flaky/timeouts → investigate; never paper over by disabling tests
  ([spec §5](../specification.md#5-design-principles--engineering-standards)).

## Dependencies

- Needs a buildable solution + test project from [feature 01](./01-application-shell.md) and
  [getting-started.md](../getting-started.md). The packaging/release side is
  [feature 14](./14-release.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- This feature *is* the automated test gate; validate by opening a draft PR and confirming the
  workflow runs and goes green/red appropriately. No app-level unit tests of its own.

## Out of scope

- Building/packaging/publishing releases — that's [feature 14](./14-release.md).
- Microsoft Store submission and auto-update.

## Standards reminder

Open-source repo conventions; CI enforces the all-unit-tests-pass definition of done; never commit
secrets; verify any packaging/runner specifics via the Microsoft docs skills.
