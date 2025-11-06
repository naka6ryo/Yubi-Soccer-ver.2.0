# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Yubi Soccer** is a Unity-based multiplayer mobile game that combines hand gesture recognition with soccer gameplay. Players control their characters using hand tracking (via MediaPipe) and joystick controls, competing in 1v1 matches over Photon networking.

## Key Technologies

- **Unity 6000.0.47f1** - Game engine
- **Photon PUN 2** - Real-time multiplayer networking
- **MediaPipe Unity Plugin (v0.16.2)** - Hand tracking and gesture recognition
- **WebGL** - Primary deployment target (mobile browser-based)
- **Universal Render Pipeline (URP)** - Graphics pipeline

## Project Structure

### Core Scenes

Build-enabled scenes are configured in `ProjectSettings/EditorBuildSettings.asset`:

1. **GameTitle** - Title screen with matchmaking UI. Contains `NetworkManager` for Photon connection and room joining
2. **Multi Player** - Main gameplay scene where 1v1 matches occur. Contains `PlayerCreator` to spawn networked player instances
3. **Player Move Test mobile** - Mobile testing scene for joystick and hand tracking integration
4. **HandTrack** - Hand tracking development/testing scene (disabled in builds)

### Scripts Architecture

#### Network Layer (`Assets/Scripts/Network/`)

- **NetworkManager.cs** - Photon PUN 2 integration
  - Manages Photon connection lifecycle (connect, join room, create room)
  - Implements quick match system (1v1 matchmaking via `JoinRandomRoom`)
  - Auto-loads game scene when room reaches 2 players (master client triggers scene load)
  - Optional runtime AppId override via Inspector (useful for not committing credentials)

- **MatchmakingUI.cs** - UI bridge between buttons and NetworkManager

#### Player Layer (`Assets/Scripts/Player/`)

- **PlayerController.cs** - Player movement and network synchronization
  - Input sources: keyboard (W/A/D), joystick, and hand gestures
  - Networked via `IPunObservable` - serializes position/rotation to remote clients
  - Integrates with `HandStateReceiver` for gesture-based movement (e.g., "RUN" state triggers forward movement)
  - Local player controls directly; remote players interpolate smoothly via `lerpRate`

- **PlayerCreator.cs** - Player spawning in game scene
  - Spawns local player using `PhotonNetwork.Instantiate` from Resources folder
  - Player prefab must be in `Assets/Resources/Player.prefab`
  - Handles spawn position randomization and cleanup

#### Hand Tracking Integration

- **HandStateReceiver.cs** - Receives hand gesture data from JavaScript/browser layer
  - Listens for Unity `SendMessage` calls from WebGL JavaScript context
  - Parses JSON payloads with gesture state (KICK, RUN, NONE) and confidence score
  - Exposes `currentState` and `currentConfidence` as public fields for other scripts
  - Auto-creates fallback UI text overlays for WebGL builds if no UI is assigned

- **JoystickPositionController.cs** - Dynamic joystick repositioning
  - Adjusts joystick position based on device orientation (via JavaScript callbacks)
  - Positions joystick on opposite side of hand camera for optimal ergonomics

### WebGL Embedding

- **Custom Template**: `Assets/WebGLTemplates/yubi-soccer/`
- **Embedded Version**: `Assets/WebGLTemplates/yubi-soccer/TemplateData/embedded_yubi/`
- Browser-to-Unity communication via `SendMessage` API for hand tracking data

## Development Commands

### Opening the Project

```bash
# Open in Unity Editor
open -a Unity "/Users/hide/Projects/Yubi-Soccer-ver.2.0"
```

Or use Unity Hub to open the project directory.

### Building

Unity builds are performed through the Editor UI:
1. File → Build Settings
2. Select **WebGL** platform
3. Ensure scenes are enabled: GameTitle, Multi Player, Player Move Test mobile
4. Set WebGL template to "yubi-soccer" in Player Settings
5. Click "Build" and choose output directory

### Deployment (WebGL to Netlify)

```powershell
# Deploy to Netlify (requires Netlify CLI: npm i -g netlify-cli)
# Interactive deploy (choose/create site):
.\deploy_to_netlify.ps1

# Deploy to specific site (draft):
.\deploy_to_netlify.ps1 -SiteId <your-site-id>

# Production deploy:
.\deploy_to_netlify.ps1 -SiteId <your-site-id> -Prod
```

Deployment publishes `Assets/WebGLTemplates/TemplateData` directory (configured in `netlify.toml`).

### Testing Multiplayer

For local multiplayer testing:
1. Build WebGL version to a directory
2. Run Unity Editor and open GameTitle scene
3. Click Play in Editor
4. Open the built WebGL in browser
5. Click "QuickMatch" on both clients - they should find each other and load Multi Player scene

Alternatively, use Unity's **ParrelSync** package to run multiple Editor instances simultaneously.

## Photon Configuration

### App ID Setup

**Option A (Recommended)**: Set in PhotonServerSettings asset
- Window → Photon Unity Networking → PUN Wizard
- Paste App ID from [Photon Dashboard](https://dashboard.photonengine.com)

**Option B**: Runtime override in NetworkManager
- Add `NetworkManager` component to scene
- Set `appId` field in Inspector
- Only affects runtime; doesn't persist to PhotonServerSettings

**Security Note**: App ID is sensitive. If using Option B, ensure `appId` field is not committed with real credentials for public repositories.

## MediaPipe Hand Tracking

Hand tracking runs in browser JavaScript layer (outside Unity):
1. Browser captures camera feed
2. MediaPipe processes frames and recognizes gestures
3. JavaScript sends gesture state to Unity via `SendMessage('HandStateReceiver', 'OnEmbeddedState', jsonPayload)`
4. Unity scripts (e.g., `PlayerController`) read `HandStateReceiver.currentState` to control gameplay

Gesture states:
- **RUN** - Triggers forward movement when confidence > 0.7
- **KICK** - Reserved for future kick action implementation
- **NONE** - No gesture detected

## Architecture Notes

### Network Flow

1. Player opens GameTitle scene
2. `NetworkManager` auto-connects to Photon on Start (if `autoConnectOnStart = true`)
3. Player clicks QuickMatch → `NetworkManager.QuickMatch()` calls `PhotonNetwork.JoinRandomRoom()`
4. If no room exists, fallback creates new room with `MaxPlayers = 2`
5. When 2nd player joins, master client loads Multi Player scene via `PhotonNetwork.LoadLevel()`
6. Multi Player scene's `PlayerCreator` spawns networked player prefabs for both clients
7. Each client controls their own player; `PlayerController.OnPhotonSerializeView` syncs transforms to remote clients

### Input Handling Priority

`PlayerController.HandleInput()` checks inputs in this order:
1. Keyboard (W/A/D)
2. Hand gesture state (via `HandStateReceiver`)
3. Joystick (via `FixedJoystick`)

### Scene Persistence

`NetworkManager` should persist across scenes using `DontDestroyOnLoad` (check if implemented) or be present in both GameTitle and Multi Player scenes. Currently, architecture assumes it's in GameTitle only, which may cause issues if players disconnect during gameplay.

## Common Workflows

### Adding New Gestures

1. Update MediaPipe JavaScript to recognize new gesture
2. Send new state string via `SendMessage` to `HandStateReceiver`
3. Add color mapping in `HandStateReceiver.ColorForState()` for UI feedback
4. Handle new state in `PlayerController.HandleInput()` or other gameplay scripts

### Creating New Player Abilities

1. Add logic to `PlayerController` or create new component on Player prefab
2. Read from `HandStateReceiver.currentState` and `currentConfidence` if gesture-triggered
3. For networked abilities, use `PhotonView.RPC()` to synchronize actions across clients
4. Update `PlayerController.OnPhotonSerializeView()` if new state needs continuous sync

### Debugging WebGL Builds

- Enable Development Build in Build Settings
- Use browser console to see Unity `Debug.Log` output
- `HandStateReceiver` logs all incoming JSON payloads for visibility
- Check Network tab for Photon WebSocket connection issues

## Dependencies

Key Unity packages (see `Packages/manifest.json`):
- `com.unity.render-pipelines.universal` - URP graphics
- `com.unity.inputsystem` - New Input System
- `com.unity.ugui` - UI system
- `com.unity.ai.navigation` - NavMesh (future AI opponents?)
- Custom: `com.github.homuler.mediapipe` - MediaPipe plugin

Photon PUN 2 is imported via Asset Store (not in package manifest).

## Important Constraints

- **Mobile-first**: Designed for mobile browsers with portrait/landscape orientation handling
- **WebGL limitations**: No threading, limited file I/O, all assets must be included in build
- **Photon CCU limits**: Free tier has concurrent user limits; monitor usage in Photon Dashboard
- **Hand tracking performance**: MediaPipe runs in browser, may impact frame rate on low-end devices

## Code Style Notes

- Japanese comments are common in this codebase (as seen in copilot-instructions.md)
- Public fields used extensively for Inspector configuration
- Defensive null checks before accessing components
- Extensive logging with `[ClassName]` prefixes for debugging WebGL deployments
