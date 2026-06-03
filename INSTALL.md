# Installing Wide Play on a real device

This guide covers building Wide Play from source and running it on physical iPhone and Android hardware, plus the Spotify setup needed for real playback. For a quick mock-only run on a simulator, see the "Getting started" section in the [README](README.md) instead.

> **What you need real hardware for**
> - **Spotify playback** → a Spotify **Premium** account + a Spotify Developer app (free to create).
> - **Bluetooth** → **two physical phones**. BLE advertising/scanning does not work between simulators.

---

## 1. Prerequisites

| Tool | Notes |
|------|-------|
| **.NET 10 SDK** | `dotnet --version` should print `10.x`. |
| **.NET MAUI workload** | `dotnet workload install maui` |
| **VS Code** | with the **C# Dev Kit** and **.NET MAUI** extensions |
| **Android SDK** | Required for Android builds. Easiest via Android Studio, or `dotnet workload`'s Android tooling. Set `ANDROID_HOME` if the build can't find it. |
| **Xcode** | Required for iOS builds (macOS only). Install from the App Store, then run `xcodebuild -runFirstLaunch`. |

Verify the toolchain:

```bash
dotnet workload list   # should include "maui"
```

---

## 2. Create a Spotify Developer app

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) and log in.
2. **Create app**. Name/description can be anything.
3. In the app's **Settings**, add this exact **Redirect URI**:
   ```
   wideplay://callback
   ```
4. Copy the **Client ID** (you do **not** need the client secret — PKCE doesn't use one).

> The redirect URI must match `RedirectUri` in [Services/SpotifyConfig.cs](Services/SpotifyConfig.cs) **character-for-character**, or login will fail.

---

## 3. Configure the app

1. Open [Services/SpotifyConfig.cs](Services/SpotifyConfig.cs) and paste your Client ID:
   ```csharp
   public const string ClientId = "your_client_id_here";
   ```
2. Open [MauiProgram.cs](MauiProgram.cs) and flip the toggles for whichever real services you want:
   ```csharp
   bool useMockSpotify = false;  // real Spotify (needs Premium + Client ID)
   bool useMockBle     = false;  // real BLE (needs two physical phones)
   ```
   You can switch them independently — e.g. test real Spotify on one phone while leaving BLE mocked.

---

## 4. Run on an Android device

1. On the phone: enable **Developer options** (tap *Build number* 7× in Settings → About), then turn on **USB debugging**.
2. Connect the phone by USB and accept the debugging prompt.
3. Confirm it's visible:
   ```bash
   adb devices
   ```
4. Build, deploy, and launch:
   ```bash
   dotnet build -t:Run -f net10.0-android
   ```
5. On first launch the app will request Bluetooth (and, on older Android, Location) permissions — accept them.

**Distributing a standalone APK** (to hand to a friend's phone for the second device):

```bash
dotnet publish -f net10.0-android -c Release
# Output: bin/Release/net10.0-android/publish/*-Signed.apk
```
Copy the `-Signed.apk` to the device and open it (the device must allow "install from unknown sources").

---

## 5. Run on an iOS device

iOS requires code signing, even for personal sideloading. A **free** Apple ID works — apps signed this way last 7 days before needing a re-deploy.

1. Open the project's signing in **Xcode** (or VS Code's MAUI signing UI):
   - Sign in with your Apple ID under **Accounts**.
   - Set the **Team** to your personal team and confirm the **Bundle Identifier** `com.somekaicodes.wideplay` (change it if it collides).
   - Let Xcode create a provisioning profile automatically.
2. Connect the iPhone by USB and **trust** the computer.
3. Build and deploy:
   ```bash
   dotnet build -t:Run -f net10.0-ios -p:RuntimeIdentifier=ios-arm64
   ```
4. On the phone, go to **Settings → General → VPN & Device Management** and **trust** your developer certificate.
5. Launch the app; accept the Bluetooth permission prompt.

---

## 6. Testing a real session (two phones)

1. Install the app on **both** phones (any mix of iOS/Android).
2. Make sure Bluetooth is on, and the Spotify app is installed and **playing/available** on each (so it counts as an active Spotify Connect device).
3. On **Phone A**: connect Spotify, tap **Host a Session**, search and play a track.
4. On **Phone B**: tap **Join a Session**, pick Phone A's session from the list.
5. Use play / pause / skip / search on Phone A — Phone B should mirror within a moment.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `XA5300: The Android SDK directory could not be found` | Install the Android SDK (Android Studio) and set `ANDROID_HOME`, or pass `-p:AndroidSdkDirectory=/path/to/sdk`. |
| Spotify login opens then immediately fails | The Redirect URI in the dashboard doesn't exactly match `wideplay://callback`. |
| Playback calls succeed but nothing plays | Spotify needs an **active device** — open the Spotify app on that phone first. Playback control also requires **Premium**. |
| `codesign ... Disallowed xattr com.apple.FinderInfo` on a **Mac Catalyst** build | Only affects the macOS build (not iOS/Android). Run `xattr -rc Resources bin` and rebuild. |
| No nearby sessions appear | Both phones must have Bluetooth on and the BLE permissions granted; some Android phones also need Location enabled to scan. |
| iOS app won't launch after a week | Free provisioning profiles expire after 7 days — re-deploy with `dotnet build -t:Run`. |
