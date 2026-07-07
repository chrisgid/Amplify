

# Amplify

[![CI](https://github.com/chrisgid/Amplify/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/chrisgid/Amplify/actions/workflows/ci.yml)
[![CodeQL](https://github.com/chrisgid/Amplify/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/chrisgid/Amplify/actions/workflows/codeql.yml)

**Control Spotify's volume with global keyboard hotkeys.**

A native Windows app that lives in the system tray and lets you
raise or lower your Spotify playback volume from anywhere with a keyboard shortcut.

Inspired by Birath's Spotify Volume controller: https://github.com/Birath/spotify-volume-controller-cpp

## Features

- 🎚️ **Global hotkeys** for volume up / down (default `Ctrl+Alt+↑` / `Ctrl+Alt+↓`) - work
  system-wide, even when Amplify is minimised.
- 🎯 Controls your **active Spotify device** via the Spotify Web API.
- ⚙️ Configurable **step size**, optional **volume-change toast**, **launch at startup**, and
  **start minimized to tray**.

## Requirements

- Windows 11 (Windows 10 1809+ supported).
- A **Spotify Premium** account.
- **Your own Spotify app registration** - The app will walk you through creating one during setup.

## Installation

Download the latest version from the [**Releases**](../../releases) page.

## Getting started (development)


```powershell
dotnet build -c Release
dotnet test  -c Release   # unit tests
```

## Contributing

Issues and pull requests are welcome

## Privacy

Amplify runs entirely on your own machine — no servers, no analytics, no telemetry. It talks only
to Spotify's Web API, using the developer app you create during setup. See [PRIVACY.md](PRIVACY.md)
for the details.

## Disclaimer

Yes, this application was built with the help of AI. From the initial idea, Claude Code and Claude Design were used to build out a specification, feature documentation, and a prototype which was later implemented by Claude Code.

**Not affiliated with, endorsed by, or sponsored by Spotify.** Amplify uses the Spotify Web API to control playback volume on your own account.
