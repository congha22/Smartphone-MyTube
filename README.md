# Smartphone-MyTube

A Smartphone app for Stardew Valley that plays a user-pasted YouTube video and can remain visible in Smartphone's pinned HUD while the player continues playing the game.

## Current status

This repository is an early proof of concept. The Smartphone-side integration is implemented and intentionally uses the existing public API only—no Smartphone API changes are required.

Implemented so far:

- Smartphone app registration (`mytube`).
- Landscape app UI.
- Clipboard-based **Paste URL** button.
- YouTube URL/video-ID parsing (`watch`, `youtu.be`, `shorts`, `embed`, and `live` URLs).
- Existing Smartphone passive-HUD/pinning API integration.
- Cross-process player backend abstraction.
- File-backed shared-memory frame transport from browser host to MonoGame `Texture2D`.
- Mouse-click forwarding to the off-screen browser so normal YouTube player controls can be used.
- Separate `BrowserHost` process so the SMAPI mod can remain `net6.0` while the browser runtime evolves independently.
- Initial CEF/CefGlue off-screen renderer skeleton.
- x64 and ARM64 package selection for the browser helper.

Still POC / needs validation:

- A real `dotnet build`/runtime test on a Stardew development machine.
- Windows CEF runtime layout and audio/video validation.
- macOS CEF helper application bundle/subprocess layout.
- Linux distro/Wayland/Steam Deck testing.
- CEF codec coverage for YouTube videos.
- YouTube embed error/status reporting back into the app UI.
- Audio-device behavior across platforms.
- Better pinned controls and lifecycle handling.
- Build/release automation.

## Architecture

```text
Stardew / SMAPI (net6.0)
        |
        | stdin JSON commands
        v
SmartphoneMyTube.BrowserHost (separate process, net8.0)
        |
        | CEF off-screen rendering
        v
YouTube embedded player
        |
        | BGRA frames
        v
file-backed shared memory
        |
        v
BrowserHostBackend -> Texture2D -> Smartphone app / pinned HUD
```

Keeping CEF outside Stardew is deliberate: it avoids loading browser/native dependencies into the game process and makes it possible to publish different browser runtimes for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64` while leaving the mod assembly platform-neutral.

## Repository layout

- `ModEntry.cs` — Smartphone registration and pinned HUD lifecycle.
- `MyTubeScreen.cs` — landscape app UI and video rendering.
- `Playback/YouTubeUrlParser.cs` — accepted YouTube URL formats.
- `Playback/BrowserHostBackend.cs` — launches the helper and receives frames.
- `Shared/BrowserHostProtocol.cs` — JSON/shared-frame protocol used by both processes.
- `BrowserHost/` — off-screen Chromium host POC.
- `assets/default/1x1.png` — temporary 64×64 white icon.

## Browser host development

The SMAPI mod expects a self-contained helper at:

```text
browserhost/<RID>/SmartphoneMyTube.BrowserHost[.exe]
```

Example RIDs: `win-x64`, `linux-x64`, `osx-arm64`.

The helper uses `CefGlue.Common` for x64 runtimes and `CefGlue.Common.ARM64` for ARM64 runtimes. The managed code is only the first part of browser packaging: before distributing the mod, the CEF native runtime/resources and the macOS helper-app layout must be pinned and tested for each target platform.

The scripts in `scripts/` are a starting point for publishing the managed BrowserHost. They intentionally print a warning because CEF runtime packaging is not yet considered finished.

## YouTube approach

MyTube uses YouTube's embedded player rather than downloading/extracting video streams. The browser host supplies an application-style HTTPS Referer (`https://d5a1lamdtd.smartphone-mytube`) and keeps the normal YouTube player UI visible. The intended pinned mode keeps the player visible while Stardew remains playable.

## POC usage

1. Build `Smartphone-MyTube.csproj` and install it as a normal SMAPI mod alongside Smartphone.
2. Build/publish `BrowserHost` for your platform into `browserhost/<RID>/`.
3. Open MyTube on the Smartphone.
4. Copy a YouTube URL to the system clipboard.
5. Press **Paste URL**, then **Play**.
6. Press **Pin** to close the menu and keep the player visible on Smartphone's pinned HUD.

If the helper isn't present, MyTube still loads and shows the expected BrowserHost path in the status text/log instead of crashing Stardew.

## Important POC limitation

This initial code has been statically checked against the current Smartphone API and CefGlue API sources, but it has not been compiled in this environment because the .NET SDK is unavailable here. The first development-machine milestone should therefore be: restore packages, build both projects, run Stardew/SMAPI, and fix any CEF runtime-layout issues before expanding the UI.
