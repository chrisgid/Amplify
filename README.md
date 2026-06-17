<div align="center">

# Amplify

**Control Spotify's volume with global keyboard hotkeys.**

A lightweight, native **WinUI 3** app for Windows 11 that lives in the system tray and lets you
raise or lower your Spotify playback volume from anywhere with a keyboard shortcut.

</div>

---

## Features

- 🎚️ **Global hotkeys** for volume up / down (default `Ctrl+Alt+↑` / `Ctrl+Alt+↓`) — work
  system-wide, even when Amplify is in the tray.
- 🎯 Controls your **active Spotify device** via the Spotify Web API.
- ⚙️ Configurable **step size**, optional **volume-change toast**, **launch at startup**, and
  **start minimized to tray**.
- 🌗 Looks built-in: native Windows 11 controls, **follows your Windows theme and accent colour**.

## Requirements

- Windows 11 (Windows 10 1809+ supported).
- A **Spotify Premium** account (volume control is a Premium-only Web API capability).
- **Your own Spotify app registration** — Amplify uses a per-user Client ID (no shared quota). The
  app walks you through creating one during setup; it takes a minute and needs no client secret.

## Install

Download the latest `.msix` from the [**Releases**](../../releases) page and install it. The
package is signed; if you're prompted, install the bundled certificate / enable sideloading
(instructions accompany each release).

## Getting started (development)

Amplify is built as a WinUI 3 / .NET 10 app. See **[docs/getting-started.md](docs/getting-started.md)**
for the solution layout, exact SDK/package versions, and the Spotify app-registration steps.

```powershell
dotnet build -c Release
dotnet test  -c Release   # unit tests
```

## Documentation

- **[Specification](docs/specification.md)** — overview, architecture, standards.
- **[Contracts](docs/contracts.md)** — service interfaces, models, settings shape.
- **[Feature docs](docs/features)** — one document per feature.

## Contributing

Issues and pull requests are welcome. CI builds and runs the unit tests on every PR, and all unit
tests must pass before merge.

## License

Released under the [MIT License](LICENSE). Third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

> **Not affiliated with, endorsed by, or sponsored by Spotify.** Spotify is a trademark of
> Spotify AB. Amplify uses the Spotify Web API to control playback volume on your own account.
