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

## 2026-06-20 — Repo made public: action pinning hardening · branch `feat/02-ci-pr-tests`

When the repo flipped from private to public, two Actions policies were tightened (Settings →
Actions → General): `allowed_actions` → `local_only` and `sha_pinning_required` → `true`. Both are
incompatible with tag-pinned third-party actions, so the workflow was updated to comply.

- **Pinned all three actions to full commit SHAs** (with a `# vX.Y.Z` comment) instead of moving
  `@v4` tags — required by `sha_pinning_required`, and good supply-chain practice (a compromised tag
  can't silently change what runs). Took the latest majors while pinning:
  `actions/checkout` → **v7.0.0** (`9c091bb…`), `actions/setup-dotnet` → **v5.3.0** (`9a946fd…`),
  `actions/cache` → **v5.0.5** (`27d5ce7…`). Our usage (checkout defaults; `dotnet-version`;
  `path`/`key`/`restore-keys`) is unchanged across these majors.
- **`allowed_actions: local_only` must be relaxed to `selected` with GitHub-owned actions allowed**
  (or `all`) — `local_only` blocks every action not defined in this repo, including `actions/*`, so
  SHA-pinning alone does **not** unblock CI. This is a repo setting, not a file; logged here as a
  required maintainer action. Until changed, CI fails at the policy check before any step runs.
- **Verified facts:** the v4 tags of all three actions still exist, but the current latest majors are
  checkout v7 / setup-dotnet v5 / cache v5 (resolved via the GitHub API; annotated tags dereferenced
  to their commit SHA).

## 2026-07-07 — SAST via CodeQL · `.github/workflows/codeql.yml`

Added a **separate** CodeQL (SAST) workflow rather than extending `ci.yml`. The YAML mechanics
(triggers, x64 build mirroring CI) are self-explanatory; recorded below is only the non-obvious.

- **Separate workflow, not a job in `ci.yml`** — different trigger cadence (adds a weekly
  `schedule` cron so the code is re-scanned against updated queries when idle), different permissions
  (`security-events: write` vs. `ci.yml`'s locked `contents: read`), and different failure semantics
  (advisory, see below). Keeping them apart preserves `ci.yml` as a least-privilege merge gate. Cost
  is the C# build running twice per PR — negligible at this size, and the standard CodeQL pattern.

- **Advisory, not blocking (per maintainer decision).** The workflow itself does not block merge;
  making it a merge gate is a branch-protection setting. To promote later: add the **"Analyze (C#)"**
  check to Settings → Branches required checks. Until then findings inform review only. Mirrors the
  same "required check is a repo setting, not a file" gap noted for `ci.yml` above.

- **CodeQL is GitHub-owned → policy-compliant.** `github/codeql-action/{init,analyze}` satisfies the
  `allowed_actions: selected` (GitHub-owned) + `sha_pinning_required` policies from the entry above.
  Pinned both to **`c35d1b1…`** (`codeql-bundle-v2.25.6`, the current release, resolved via the
  GitHub API — an annotated tag dereferenced to its commit SHA).

- **Manual build, not `autobuild`.** Same reason as CI: `Amplify.App` exposes only `x86;x64;ARM64`
  (no Any CPU), so CodeQL must observe a `-p:Platform=x64` build on `windows-latest`. The workflow
  reuses CI's restore/build steps verbatim so CodeQL analyzes the same compilation the gate does.

- **Query suite:** `security-and-quality` (security + maintainability queries). Drop to
  `security-extended` or the default suite if it proves noisy on first rollout.

- **Deferred / known gaps:** not yet validated against a live run — first green/red confirmation
  happens when this change's PR opens (CodeQL builds on GitHub-hosted runners; nothing to validate
  locally). No baseline findings triaged yet.

## 2026-07-08 — Switch CodeQL to buildless (build-mode: none) to drop generated-file noise

The first CodeQL run on `main` produced ~100 alerts, **91 of them false positives in
`src/Amplify.App/obj/**/*.g.cs`** — XAML-compiler-generated files (`XamlTypeInfo.g.cs`,
`MainPage.g.cs`, per-page `*.g.cs`) that the `build-mode: manual` x64 build emitted into `obj/` and
CodeQL then extracted. Confirmed the breakdown via the code-scanning API before fixing.

- **Rejected first attempt (`paths-ignore` alone, keeping `build-mode: manual`).** Initially added a
  config file with `paths-ignore: [**/obj/**, **/bin/**]` on the assumption that CodeQL filters
  reported alerts by source path for compiled languages. **That is wrong.** Per the GitHub docs
  ("Specifying directories to scan"), for compiled languages `paths`/`paths-ignore` take effect
  **only in `build-mode: none`**; "for analysis where code is built ... you must specify appropriate
  build steps." Under a real build the filters are ignored for C#, so that change would have been a
  no-op — caught in code review, verified against the docs.
- **Fix: `build-mode: none` (buildless C# analysis).** With no build, `obj/` is never populated, so
  the generated `*.g.cs` files are never analysed — the noise is removed at the root rather than
  filtered. `paths-ignore` stays in the config as defensive belt-and-braces (and now actually takes
  effect). The `dotnet build` step is gone: buildless extraction needs no compiler, so the WinUI
  x64-only platform problem that forced `build-mode: manual` no longer applies.
- **Runner stays `windows-latest` + `setup-dotnet` + `dotnet restore` (corrected in review).** A first
  cut moved this to `ubuntu-latest` with no SDK, on the reasoning that buildless needs no toolchain.
  Wrong for *accuracy*: the docs state buildless C# "functions ... without requiring .NET SDK
  compilation, though accurate analysis benefits from proper NuGet dependency restoration." On a bare
  Linux runner the `net10.0-windows` / WinUI / WindowsAppSDK dependencies won't restore, so the
  extractor would analyse the ~9 real-source files with unresolved references — silently degraded, not
  errored. So: Windows runner, .NET 10 SDK, and an explicit `dotnet restore` (mirroring ci.yml, which
  resolves these deps cleanly). Still no build, so the generated-file noise stays gone; restore writes
  only `obj/*.json`/`.props`, never `*.g.cs`.
- **Tradeoff:** buildless analysis can have slightly lower data-flow fidelity than a built analysis.
  Accepted — it's GitHub's supported mode for C# and the only way to exclude the generated files via
  config. If deeper analysis is ever wanted, the alternative is `build-mode: manual` + dismissing the
  generated-file alerts through alert management (not config).
- **Verified facts:** `microsoft/win-dev-skills` — a WinUI repo, our closest analog — uses
  `build-mode: none` + `config-file` with `paths-ignore` `**/obj/**` + `**/bin/**`. It runs on
  `ubuntu-latest` with no restore; we deliberately deviate (Windows + restore) because our C# is a
  full `net10.0-windows` WinUI *app* whose deps need resolving for accuracy, whereas their C# is a
  small tool. `Humanizr/Humanizer` pairs `build-mode: manual` with `paths-ignore`, which per the docs
  does not actually filter — a misconfiguration, not a precedent to copy.
- **Deferred / known gaps:** the ~9 alerts in real source files are untriaged — this change only
  removes the generated-file noise; it does not assess the genuine findings.

## 2026-07-10 — Narrow CodeQL to security-extended (drop maintainability noise)

Once the generated-file noise was gone, the live run settled at **53 open alerts — all
`security_severity_level: none`** (maintainability/quality, not security). Confirmed via the
code-scanning API. Dominated by `cs/unmanaged-code` + `cs/call-to-unmanaged-code` (28 — the global
keyboard hook's P/Invoke, inherent and unavoidable), `cs/path-combine` (12), and
`cs/catch-of-all-exceptions` (8).

- **Switched the suite `security-and-quality` → `security-extended`** in `codeql-config.yml`. As a
  SAST gate the aim is genuine security findings; the maintainability queries were pure churn for a
  P/Invoke desktop app. This drops all 53 quality alerts (zero were security-severity) and leaves
  CodeQL security-focused. Bump back if the maintainability queries are ever wanted.
- **Handled separately (not via CodeQL):** the 8 `cs/catch-of-all-exceptions` + 1
  `cs/empty-catch-block` alerts touch the [spec §5](../specification.md#5-design-principles--engineering-standards)
  "catch specific exceptions / no empty catch" standard, so they were surfaced to the maintainer for
  a hand-check independent of the query-suite change (they disappear from CodeQL under
  `security-extended` regardless, being maintainability queries).
- **Verified facts:** all 53 alerts had `security_severity_level: none` — there were **no** security
  findings under `security-and-quality` at this point.

## 2026-07-11 — Triage of the 9 catch-related alerts · branch `fix/exception-handling-standards`

Hand-checked all nine `cs/catch-of-all-exceptions` (8) + `cs/empty-catch-block` (1) alerts from the
entry above against [spec §5](../specification.md#5-design-principles--engineering-standards)
("catch specific exceptions; no empty or silent catch blocks; log the exception").

- **8 of 9 left as-is — legitimate resilience boundaries, not violations.** Each `catch (Exception ex)`
  (SpotifyAuthService restore, TrayService icon creation, Onboarding/Status/Settings VM command
  boundaries, NotificationService balloon, SettingsMigrationRunner) wraps interop/OS/HTTP whose
  failure modes aren't enumerable, carries a rationale comment, and **logs** the exception (or, in the
  logger-less Core `SettingsMigrationRunner`, degrades to defaults via its return value). Spec §5 bars
  *empty/silent* catches, not documented+logged boundary handlers; narrowing these to specific types
  would risk crashing a resilience path on an unlisted exception. Deliberately not churned.
- **1 fixed — `FileLogWriter.AppendLine`.** It had an empty, undocumented `catch
  (UnauthorizedAccessException) {}` beside a documented `catch (IOException)`. Merged the two into one
  `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` with a single
  rationale comment — behaviourally identical, no longer an empty/silent block. `ex` is consumed by
  the `when` filter, so no unused-variable warning under `TreatWarningsAsErrors`. (There is nowhere to
  log from inside the log writer itself, hence swallow-with-comment rather than log.)
- **Manual/integration checks:** `dotnet build … -c Release -p:Platform=x64` → **0 warnings, 0 errors**;
  `dotnet test … --filter "Category!=RequiresSpotify"` → **221 passed**.
