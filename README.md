

# Amplify

[![CI](https://github.com/chrisgid/Amplify/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/chrisgid/Amplify/actions/workflows/ci.yml)
[![CodeQL](https://github.com/chrisgid/Amplify/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/chrisgid/Amplify/actions/workflows/codeql.yml)

**Control your Spotify volume with keyboard shortcuts from your Windows PC**

Inspired by Birath's Spotify Volume controller: https://github.com/Birath/spotify-volume-controller-cpp

## Features

- **Global hotkeys** for turning your volume up and down
- Controls the volume of your **active Spotify device**\* (via Spotify's Web API)
- Minimizes to the system tray by default, with an option to **launch at startup**
- Configurable volume step size

\*Note that not all devices allow remote volume control

## Requirements

- Windows 11 (Windows 10 1809+ supported)
- A **Spotify Premium** account
- A **Spotify development mode app** - Amplify will walk you through creating one during setup

## Installation

Download the latest version from the [**Releases**](../../releases) page.

## Contributing

Issues and pull requests are welcome!

## Privacy

Amplify runs entirely on your own PC with no analytics or telemetry. It talks only
to Spotify's Web API, using the developer app you create during setup. See [PRIVACY.md](PRIVACY.md)
for more information.

## Disclaimer

**Not affiliated with, endorsed by, or sponsored by Spotify.**

Yes, this application was built with the help of AI. "AI slop" you say? Well... you might be right. However, this is exactly the kind of development side-project I'd have abandoned, unfinished, after a couple of weeks without it. So here you go, enjoy!