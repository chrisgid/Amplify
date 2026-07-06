# Feature 14 — Release: Build & Publish

> Read first: [`getting-started.md`](../getting-started.md) and [`contracts.md`](../contracts.md).
>
> Parent spec: [`../specification.md`](../specification.md) · Depends on:
> [01 shell](./01-application-shell.md), [02 CI](./02-ci-pr-tests.md) ·
> **Build this last** — once the app is feature-complete and shippable.

## Summary

A **GitHub Actions** workflow that, on a version tag, builds and packages the **MSIX**, signs it,
and publishes it to a **GitHub Release** for users to download. Distribution is via **GitHub
Releases** only — no Microsoft Store. Also defines the **versioning** scheme.

## User stories

- As a maintainer, I want tagging a version to produce a signed, downloadable build with no manual
  steps.
- As a user, I want to download and install Amplify from the project's Releases page.

## Behaviour

- **Workflow:** `.github/workflows/release.yml`.
- **Trigger:** push of a tag matching `v*.*.*`.
- **Runner:** `windows-latest`.
- **Steps:** checkout → setup .NET 10 → restore/build → run unit tests → **stamp the version** into
  `Package.appxmanifest` from the tag → build the **MSIX** (`msbuild` with
  `GenerateAppxPackageOnBuild`, per-arch `x64`/`arm64`) → **sign** the package → create a **GitHub
  Release** for the tag and upload the `.msix`/`.msixbundle` (+ checksums) as assets.

### Versioning

- **Semantic Versioning** (`MAJOR.MINOR.PATCH`); the **git tag `vX.Y.Z` is the source of truth**.
- The MSIX manifest version is 4-part — the tag maps to `X.Y.Z.0`. CI stamps it at build time so
  the in-app footer (`Package.Current.Id.Version`,
  [feature 10](./10-settings-persistence.md)) matches the released build.
- Maintain [`CHANGELOG.md`](../../CHANGELOG.md) (Keep a Changelog format); the GitHub Release notes
  mirror the section for that version.
- Optional: derive the version with **MinVer** (build-time, tag-based, no committed version file)
  instead of manual stamping.

## Acceptance criteria

- [ ] Pushing a `vX.Y.Z` tag builds, tests, packages, signs, and publishes an MSIX to a GitHub
      Release.
- [ ] The released package's version equals the tag (`X.Y.Z.0`) and shows in the app footer.
- [ ] The package is signed; the release notes/README explain installing the certificate /
      sideloading.
- [ ] `CHANGELOG.md` has an entry for each released version.
- [ ] Unit tests run as part of the release build and must pass before publishing.

## Implementation guidance

- Use `actions/checkout`, `actions/setup-dotnet` (pin .NET 10), and `microsoft/setup-msbuild` for
  the packaging build. Confirm MSIX CI packaging specifics with the
  `microsoft-docs:winui3`/`microsoft-docs` skills.
- **Code signing:** store the signing cert as an encrypted **GitHub Actions secret** (base64 PFX +
  password); import it in the job and sign with `signtool` or the MSBuild appx signing properties.
  A self-signed cert is acceptable for open-source side-loading (document installing the public
  cert / enabling sideloading in the README). **Never commit the certificate.**
- Stamp the manifest version from `github.ref_name` (strip the leading `v`, append `.0`). This
  feature only **versions and packages** the manifest — feature-specific manifest extensions
  (e.g. the `windows.startupTask` for [08](./08-system-tray-background.md)) are authored by those
  features, not here. (Feature 09's tray hint uses the H.NotifyIcon `TaskbarIcon` and needs no
  notification activator.)
- Reuse the same build settings as [feature 02](./02-ci-pr-tests.md) (`Directory.Build.props`).

## Data & persistence

- None. Produces build artifacts and GitHub Releases.

## Edge cases & error handling

- A failing build/test → no release is published.
- Signing secret missing/expired → fail clearly; never publish an unsigned package.
- Re-tagging an existing version → treat as an error; require a new version rather than
  overwriting a published release.

## Dependencies

- Needs a feature-complete, buildable app and the CI foundation from
  [feature 02](./02-ci-pr-tests.md); reads the version into the footer via
  [feature 10](./10-settings-persistence.md).

## Testing

> Unit tests must pass and must not be disabled, skipped, or weakened to complete the feature —
> see [spec §5](../specification.md#5-design-principles--engineering-standards).

- Validate by cutting a pre-release tag and confirming a signed artifact is produced and attached
  to the GitHub Release. No app-level unit tests of its own.

## Out of scope

- Microsoft Store submission and auto-update (GitHub Releases download only for now).
- The PR test gate ([feature 02](./02-ci-pr-tests.md)) and app feature code.

## Standards reminder

Open-source repo conventions; never commit secrets/certificates; the release build runs the unit
tests; verify MSIX/packaging APIs via the Microsoft docs skills.
