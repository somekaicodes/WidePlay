# WidePlay — Project Context

**App name:** "Wide Play"
**Developer:** Kai Kim (somekaicodes)
**Platform:** iOS + Android, .NET MAUI (C#)
**IDE:** VS Code on Mac

---

## What the app does

Wide Play is a BLE-coordinated Spotify session controller. It lets nearby phones
running the app join a shared session where one phone acts as the "DJ" — 
controlling play/pause/skip — and all other phones play the same Spotify track
simultaneously via Spotify Connect.

No audio streaming. Each phone plays independently from its own Spotify app,
synced by BLE control signals.

---

## Core features

- **BLE** — device discovery and pairing, sending play/pause/skip/song URI signals
- **Spotify** — OAuth login (PKCE), song search, playback control via Spotify Connect
- **MVVM** — CommunityToolkit.Mvvm for all ViewModels
- **Rx.NET** — reactive streams for BLE characteristic updates and playback state
- **Push Notifications** — notify peers when a new session starts nearby

---

## Key decisions

- Spotify only (no Apple Music or YouTube Music — no cross-platform SDK exists for either)
- Spotify Connect requires Premium — this is expected and acceptable
- BLE handles control signals only, not audio streaming
- `ISpotifyService` and `IBleService` interfaces allow mock implementations for
  testing without Premium or hardware
- BLE architecture: **Option B — designated host broadcasts, peers listen**
  - Host advertises control signals (play/pause/skip/song URI) via BLE advertising
  - Peers scan and receive broadcast packets — no GATT connection needed
  - No device limit on listeners
  - One-way by design: host is the DJ, peers listen
  - Peers request songs in person (no in-app permission/handoff flow)
  - v2 consideration: one-shot BLE message from peer → host for song suggestions

---

## NuGet packages installed

- `SpotifyAPI.Web` — Spotify Web API client
- `Plugin.BLE` — BLE central + peripheral roles
- `CommunityToolkit.Mvvm` — MVVM, ObservableObject, RelayCommand
- `System.Reactive` — Rx.NET for reactive streams

---

## Folder structure

| Folder | Purpose |
|--------|---------|
| `Models/` | Data classes: Song, PeerDevice, PlaybackState |
| `ViewModels/` | MVVM logic: SessionViewModel, PlayerViewModel, DeviceViewModel |
| `Views/` | MAUI pages/screens |
| `Services/` | IBleService, ISpotifyService interfaces + implementations |
| `Platforms/` | Android/iOS platform-specific code (MAUI default) |

---

## Progress so far

- [x] MAUI project created
- [x] Folder structure created (Models, ViewModels, Views, Services)
- [x] NuGet packages installed
- [x] Models defined: Song.cs, PeerDevice.cs, PlaybackState.cs
- [x] BLE architecture decided: Option B broadcasting, designated host
- [ ] IBleService interface
- [ ] ISpotifyService interface
- [ ] Mock service implementations
- [ ] ViewModels
- [ ] Views/UI
- [ ] Spotify OAuth flow
- [ ] BLE scan + pairing
