# Privacy

Amplify is a desktop app that runs **entirely on your own computer**. It has no
backend, no servers, no analytics, and no telemetry. The only network calls it
makes are **directly from your machine to Spotify's Web API**, using the Spotify
developer app *you* create during setup.

## What Amplify accesses

To show your connection status and change your volume, Amplify reads — over the
Spotify Web API, only while it's running:

- your **account profile** (display name), and
- your **current playback state** (volume level and the active device name).

It requests the **minimum Spotify permissions** needed for this:
`user-read-playback-state` and `user-modify-playback-state`. Nothing else.

This information is used **only in memory** to render the app and adjust volume.
It is not cached, logged, profiled, sold, or shared, and it never leaves your
device except as part of the direct request to Spotify.

## What Amplify stores, and where

| Data | Location | Notes |
| --- | --- | --- |
| Your Spotify **Client ID** and app **preferences** (hotkeys, theme, toggles) | `settings.json` in your local app-data folder | Non-secret; stays on your PC. |
| Your Spotify **refresh token** | **Windows Credential Locker** (Credential Manager) | Stored by the OS; never written to settings, logs, or source. |

Amplify keeps **no other records**. It does not store your listening history or
build any database of Spotify content.

## What Amplify does *not* do

- It does **not** send your data to us or to any third party — there is no
  "us" to receive it; the project has no servers.
- It does **not** use ad networks, data brokers, or trackers.
- It does **not** use your data, or any Spotify content, to train AI/ML models.
- It does **not** retain Spotify content beyond the immediate moment it's used.

## Disconnecting and deleting your data

- **In Amplify:** **Settings → Reset Amplify** disconnects your account and
  deletes the stored refresh token and Client ID from your machine.
  **Disconnect** alone removes the token while keeping your Client ID for an easy
  reconnect.
- **In Spotify:** you can revoke Amplify's access at any time from your
  [Spotify account's Apps page](https://www.spotify.com/account/apps/).

## Your own Spotify developer app

Because you register your **own** Spotify developer app during onboarding, your
use of the Spotify Web API is governed by
[Spotify's Developer Terms](https://developer.spotify.com/terms) and
[Developer Policy](https://developer.spotify.com/policy), which you accept when
you create that app. Using Amplify also requires a Spotify account, which is
subject to Spotify's own minimum-age requirements.

## Questions or changes

This policy may change as Amplify evolves; material changes will be noted in the
project's changelog. Questions can be raised on the project's
[GitHub issues](https://github.com/chrisgid/Amplify/issues).
