# Wide Play

> Turn a room full of phones into one synchronized speaker. One phone DJs; every other phone plays the same Spotify track at the same time — coordinated over Bluetooth Low Energy.

<!-- TODO: add a hero screenshot or GIF here, e.g. docs/screenshots/hero.png -->

## What it does

Wide Play lets nearby phones join a shared listening session. One device becomes the **host** (the DJ) and controls playback — play, pause, skip, and song choice. Every other device is a **listener** whose own Spotify app plays the same track in sync.

There is **no audio streaming** between phones. Each phone plays independently from its own Spotify account; BLE only carries small control signals (play/pause/skip and the Spotify track URI). This keeps latency low and avoids re-streaming licensed audio.

## Demo

| Home | Player (host) | Now Playing (listener) | Search |
|------|---------------|------------------------|--------|
| _scan / host / join_ | _controls + listener count_ | _read-only_ | _pick a track_ |

<!-- TODO: drop screenshots of the four screens into docs/screenshots/ and link them here -->

## Architecture

Wide Play is built with **MVVM** — the UI (Views) binds to ViewModels, which talk only to **service interfaces**, never to hardware or network SDKs directly. This is what makes the whole app runnable on a single laptop with no Spotify Premium and no second phone.

```
Views (XAML)              HomePage · PlayerPage · PeerPage · SearchPage
   │  data binding
ViewModels                SessionVM · PlayerVM · PeerVM · SearchVM
   │  depend on interfaces only
Services (interfaces)     IBleService            ISpotifyService
   │                         ├─ MockBleService      ├─ MockSpotifyService   ← dev / no hardware
   │                         └─ BleService          └─ SpotifyService       ← real
Models                    Song · PeerDevice · PlaybackState
```

Reactive streams (**Rx.NET**) carry asynchronous events out of the services — discovered sessions, received BLE commands, and Spotify playback-state changes — which the ViewModels subscribe to and project onto bindable properties.

### BLE design (host broadcasts, peers join)

- The **host** advertises a Wide Play session (a fixed service UUID + the session name) and runs a small **GATT server** with two characteristics:
  - **Join** (write) — a peer writes its device name when joining; the host increments its listener count.
  - **Control** (notify) — the host pushes `play` / `pause` / `skip` / `uri:<spotify-uri>` commands.
- A **peer** scans for the service UUID, connects, writes its name to Join, then subscribes to Control to receive commands and mirror them on its own Spotify app.

## Key engineering decisions

These are the trade-offs worth understanding (and the ones I enjoy talking through):

- **Mockable service interfaces.** `IBleService` and `ISpotifyService` each have a mock and a real implementation, switched by two flags in [MauiProgram.cs](MauiProgram.cs). The full UI and navigation can be built and demoed with **no Premium account and no second device**.
- **BLE control over GATT, not the advertisement.** The original idea was to broadcast commands purely in the advertising packet. A BLE advertisement is capped at **31 bytes**, but a Spotify URI (e.g. `spotify:track:0VjIjW4GlUZAMYd2vXMi3b`) is ~36 characters — it doesn't fit. So control signals travel over a GATT **notification** instead, and the advertisement is used only for discovery.
- **Shiny.BluetoothLE over Plugin.BLE.** Plugin.BLE only implements the BLE *central* role (scanning/connecting) — it cannot advertise or run a GATT server, which the host needs. Shiny supports **both** the central and peripheral roles, cross-platform.
- **Spotify auth with PKCE.** Mobile apps can't keep a client secret, so login uses the **OAuth PKCE** flow via the system browser ([SpotifyService.cs](Services/SpotifyService.cs)). The refresh token is stored in the OS secure store so the session survives restarts.
- **Spotify-only, Premium required.** Playback control via Spotify Connect requires Premium — an accepted constraint, since no cross-platform SDK exists for Apple Music or YouTube Music.

## Tech stack

| Concern | Choice |
|---------|--------|
| UI framework | .NET MAUI (C#), iOS + Android |
| Pattern | MVVM — CommunityToolkit.Mvvm |
| Bluetooth | Shiny.BluetoothLE + Shiny.BluetoothLE.Hosting |
| Spotify | SpotifyAPI.Web (Web API, PKCE OAuth) |
| Async/events | System.Reactive (Rx.NET) |

## Project layout

| Folder | Purpose |
|--------|---------|
| [Models/](Models/) | Plain data: `Song`, `PeerDevice`, `PlaybackState` |
| [ViewModels/](ViewModels/) | MVVM logic for each screen |
| [Views/](Views/) | MAUI XAML pages |
| [Services/](Services/) | BLE + Spotify interfaces, mocks, and real implementations |
| [Converters/](Converters/) | Small XAML value converters |
| [Platforms/](Platforms/) | iOS / Android platform glue (permissions, OAuth callback) |

## Getting started (5-minute path, with mocks)

You can run the full app — all four screens and navigation — with no Spotify account and no Bluetooth hardware, using the mock services.

```bash
git clone https://github.com/somekaicodes/WidePlay.git
cd WidePlay
dotnet restore

# Android emulator:
dotnet build -t:Run -f net10.0-android
# or iOS simulator (macOS + Xcode):
dotnet build -t:Run -f net10.0-ios
```

The mock toggles in [MauiProgram.cs](MauiProgram.cs) (`useMockSpotify`, `useMockBle`) default to `true`.

## Running on a real device (Spotify + BLE)

Real Spotify playback needs a Premium account and a Spotify Developer app; real BLE needs **two physical phones**. Full step-by-step setup — Android sideload, iOS provisioning, and Spotify dashboard configuration — is in **[INSTALL.md](INSTALL.md)**.

## Status

- [x] MVVM architecture, DI, navigation
- [x] Four screens (Home, Player, Peer, Search)
- [x] Mock BLE + Spotify services (full UI demoable on one machine)
- [x] Real Spotify integration (PKCE OAuth, search, playback control)
- [x] Real BLE integration (advertise + GATT host, scan + join peer)
- [ ] Push notifications when a session starts nearby
- [ ] On-device testing pass on physical iOS + Android hardware

---

Built by Kai Kim ([@somekaicodes](https://github.com/somekaicodes)).
