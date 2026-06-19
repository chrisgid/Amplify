# Build notes — Feature 02: CI / PR Tests

> Append a new dated entry each time a session works on this feature (Phase 0 sliver,
> Phase 1 completion, later fixes). Add to the end; don't rewrite earlier entries.

## 2026-06-20 — Phase 0 · branch `feat/02-ci-pr-tests`

Added `.github/workflows/ci.yml`: a `windows-latest` GitHub Actions workflow that restores, builds
(`-c Release`), and runs the unit tests on `pull_request` and on `push` to `main`. The YAML covers
the obvious mechanics; recorded below is only the non-obvious.

- **Deviations from spec/contracts:** none. The feature doc's recommended step shape
  (checkout → setup .NET 10 → restore → build → test) is followed verbatim.

- **Contract changes:** none.

- **Assumptions / decisions (docs were silent):**
  - **`-p:Platform=x64` on build *and* test.** The doc's example commands omit a platform, but the
    packaged WinUI App (`Amplify.App`) exposes only `x86;x64;ARM64` — no Any CPU — so a plain
    `dotnet build Amplify.slnx` defaults to the first declared platform (ARM64) and produces output
    that can't run on the x64 runner. `x64` matches `windows-latest`. This mirrors the Phase 0
    build-notes for feature 01 (`dotnet build Amplify.slnx -p:Platform=x64`). Both build and test
    pass the flag so the test step's `--no-build` resolves the same `bin/x64/Release/...` output.
  - **NuGet caching via `actions/cache@v4`, not `setup-dotnet`'s `cache: true`.** The latter needs a
    `packages.lock.json`; the repo doesn't use lock files, and adding `RestorePackagesWithLockFile`
    would change restore behaviour for every project/dev — out of scope for the CI feature. Instead
    the workflow caches `~/.nuget/packages` keyed on `hashFiles('**/*.csproj', '**/Directory.Build.props')`.
  - **`--filter "Category!=RequiresSpotify"`** is included now even though **no test carries that
    trait yet** (Phase 0 has only logic-only unit tests). On a non-existent category the filter is a
    no-op (runs all tests); it's in place so later features that add `[Trait("Category",
    "RequiresSpotify")]`/integration tests are excluded from CI automatically without touching the
    workflow.
  - **`concurrency` cancel-in-progress** on `github.ref` and **`permissions: contents: read`** added
    (least privilege; faster feedback). Not required by the doc but standard hygiene.
  - **SDK pinned to `10.0.x`** via `setup-dotnet` (no `global.json` added — keeps SDK selection out
    of local builds, per the doc's note to keep build settings in `Directory.Build.props`).

- **Deferred / known gaps:**
  - **Required-status-check / branch protection is a GitHub repo setting, not a file** — it can't be
    committed here. The acceptance criterion "workflow is required for merge to the default branch"
    must be enabled by a maintainer in Settings → Branches (require status check `build-and-test`).
    The doc's accompanying TODO. Until then the workflow runs but does not *block* merge.
  - First real green/red validation happens when this branch's PR opens (the feature's own test, per
    the doc — it has no app-level unit tests).

- **Manual/integration checks (local, mirroring the CI steps exactly):**
  - `dotnet restore Amplify.slnx` → up to date.
  - `dotnet build Amplify.slnx -c Release -p:Platform=x64 --no-restore` → **0 warnings, 0 errors**
    (all three projects, incl. the WinUI App, compile from the command line headless — no Visual
    Studio).
  - `dotnet test Amplify.slnx -c Release -p:Platform=x64 --no-build --filter "Category!=RequiresSpotify"`
    → **3 passed**.

- **Verified facts:**
  - Installed SDK is **10.0.301** (build notes for feature 01 saw 10.0.201; .NET 10 has advanced).
    `setup-dotnet` `dotnet-version: '10.0.x'` selects the latest 10.0 patch on the runner.
  - The WinUI App builds in **Release** from the CLI cleanly (the feature-01 notes had only
    explicitly recorded a Debug/x64 build; Release is confirmed green here after the PR-#1 removal of
    `PublishTrimmed`/`PublishReadyToRun`).
