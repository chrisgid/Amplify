# IconGen — Amplify logo asset generator

Dev-only tool. Renders the Amplify **"rising level" mark** (four ascending pill bars, no container)
into every MSIX icon/tile asset and a multi-resolution `AppIcon.ico`, from the parametric geometry
that matches the master vector source [`design/logo/amplify-icon.svg`](../../design/logo/amplify-icon.svg).

It is **not** part of `Amplify.slnx` and is **not** referenced by the shipped app, so its only
dependency (SkiaSharp, MIT) never enters the app's dependency graph. `tools/Directory.Build.props`
isolates it from the repo-wide strict analyzer settings.

## Run

```sh
dotnet run --project tools/IconGen              # writes into src/Amplify.App/Assets
dotnet run --project tools/IconGen  <outDir>    # or an explicit output directory
```

## What it generates (into `src/Amplify.App/Assets`)

| Asset | Sizes | Notes |
| --- | --- | --- |
| `Square44x44Logo.scale-{100,125,150,200,400}` | 44/55/66/88/176 | app-list / plated |
| `Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated` | raw px | taskbar / Start / ALT+TAB |
| …`_contrast-black` / `_contrast-white` | raw px | high-contrast (solid mono mark) |
| `Square150x150Logo.scale-{100,125,150,200,400}` | 150/188/225/300/600 | medium tile |
| `StoreLogo.scale-{100,125,150,200,400}` | 50/63/75/100/200 | installer / Store |
| `SplashScreen.scale-{100,200}` | 620×300 / 1240×600 | launch splash |
| `AppIcon.ico` | 16/24/32/48/64/128/256 | window + system tray |

The colour mark uses the monochrome-blue gradient + seating shadow + top-left highlight. At small
sizes (≤32 px) the shadow and highlight are dropped so the four bars stay distinct. The
high-contrast variants are the solid monochrome mark (black or white).

Wide/large Start tiles and the lock-screen logo are intentionally not produced (not needed for a
tray utility).
