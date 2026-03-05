# Paper.io Clone — Unity C# Setup Guide

This guide walks you through creating the `MainGame.unity` scene and wiring up
all prefabs, scripts, and materials from scratch in the Unity Editor.

---

## Prerequisites

| Item | Version |
|------|---------|
| Unity | 2022.3 LTS or 6 (6000.x) |
| Render Pipeline | Universal Render Pipeline (URP) |
| TextMeshPro | Included via Package Manager |
| Input System | Legacy (built-in) — new Input System optional |

---

## 1. Project Setup

1. Create a new Unity project using the **URP** template.
2. Import **TextMeshPro** (Window → Package Manager → TextMeshPro).
3. Copy the entire `Assets/Scripts/` folder from this repository into your
   project's `Assets/` directory.
4. In **Edit → Project Settings → Player**, set the Company Name and
   Product Name, then configure build targets for **Android**, **iOS**, and
   **WebGL**.

---

## 2. Create the ScriptableObject Config

1. Right-click in the Project window → **Create → PaperIO → GameConfig**.
2. Name it `GameConfig` and place it in `Assets/ScriptableObjects/`.
3. Tune the values in the Inspector (defaults match the original JS game).

---

## 3. Scene Hierarchy

Create a new scene `Assets/Scenes/MainGame.unity` and build this hierarchy:

```
MainGame (scene root)
├── GameManager          ← GameManager.cs + (reference holder)
├── InputManager         ← InputManager.cs
├── Systems
│   ├── TerritorySystem  ← TerritorySystem.cs
│   ├── TrailSystem      ← TrailSystem.cs
│   └── CollisionSystem  ← CollisionSystem.cs
├── Map                  ← MapManager.cs
│   ├── TerritoryPlane   ← MeshFilter + MeshRenderer (territory texture)
│   └── (walls, grid generated at runtime)
├── Player               ← PlayerController.cs + visual children
│   ├── Body             ← Cube mesh, MeshRenderer (player colour)
│   ├── Arrow            ← Cone mesh, MeshRenderer (direction indicator)
│   └── PlayerLight      ← Point Light
├── BotSpawner           ← BotSpawner.cs
├── Main Camera          ← Camera + CameraController.cs
└── UI Canvas            ← Canvas (Screen Space – Overlay)
    ├── StartScreen
    │   ├── Title (TMP_Text)
    │   ├── NameInput (TMP_InputField)
    │   └── StartButton (Button + TMP_Text)
    ├── GameHUD
    │   ├── Leaderboard (Vertical Layout Group)
    │   ├── ScorePanel
    │   │   ├── TerritoryText (TMP_Text)
    │   │   └── KillsText (TMP_Text)
    │   ├── DangerGauge
    │   │   ├── GaugeFill (Image, fill horizontal)
    │   │   └── WarningText (TMP_Text — "⚠ RETURN TO BASE!")
    │   ├── KillFeed (Vertical Layout Group)
    │   ├── Minimap (RawImage, bottom-right)
    │   └── Joystick (optional, bottom-left)
    │       ├── JoystickBackground (Image)
    │       └── JoystickHandle (Image)
    └── DeathScreen
        ├── KillsText (TMP_Text)
        ├── TerritoryText (TMP_Text)
        └── RespawnButton (Button + TMP_Text)
```

---

## 4. GameManager Inspector Wiring

Select the **GameManager** GameObject and assign:

| Field | Value |
|-------|-------|
| Config | `Assets/ScriptableObjects/GameConfig` |
| Territory System | drag TerritorySystem GameObject |
| Trail System | drag TrailSystem GameObject |
| Collision System | drag CollisionSystem GameObject |
| Map Manager | drag Map GameObject |
| Bot Spawner | drag BotSpawner GameObject |
| UI Manager | drag UI Canvas GameObject |
| Camera Controller | drag Main Camera |
| Player Controller | drag Player GameObject |
| Player Prefab | drag the Player prefab |
| Bot Prefab | drag the Bot prefab |

---

## 5. Player Prefab

Create a prefab at `Assets/Prefabs/Player.prefab`:

1. Create an empty GameObject named **Player**.
2. Add **PlayerController.cs**.
3. Add a child cube (`GameObject → 3D Object → Cube`) named **Body**:
   - Scale: `(0.7, 0.7, 0.7)`
   - Remove the BoxCollider (collision is handled in code).
   - Assign a Material with **Emission** enabled (colour set at runtime).
4. Add a child capsule named **Arrow** (or import a cone mesh):
   - Scale: `(0.25, 0.4, 0.25)`
   - Position: `(0.5, 0, 0)` (in front of the player).
5. Add a **Point Light** child named **PlayerLight**:
   - Intensity: `1.5`
   - Range: `8`
6. Wire **Body → bodyRenderer**, **Arrow → arrowRenderer**, **PlayerLight → playerLight**
   on the PlayerController component.

---

## 6. Bot Prefab

1. Duplicate the Player prefab, rename to **Bot**.
2. Replace **PlayerController** with **BotController**.
3. Assign the same visual children (bodyRenderer, arrowRenderer, playerLight).
4. Assign the Bot prefab to **BotSpawner → botPrefab**.

---

## 7. TerritoryPlane Setup

1. Under the **Map** GameObject, create `GameObject → 3D Object → Plane`.
2. Rename it **TerritoryPlane**.
3. Scale: `(40, 1, 40)` (Unity's default Plane is 10×10 units → this makes it 400×400).
4. Position: `(200, 0.002, 200)` (centred over the grid; slightly above ground to avoid z-fighting).
5. Remove the MeshCollider.
6. Assign the **TerritoryPlane MeshRenderer** to **TerritorySystem → Territory Plane**.
7. The TerritorySystem will create and assign its own material at runtime.

---

## 8. TrailSystem Materials

Create two materials in `Assets/Materials/`:

| Material | Shader | Settings |
|----------|--------|----------|
| `TrailCore` | `Sprites/Default` | Colour White, no texture |
| `TrailGlow` | `Sprites/Default` | Colour White, Rendering Mode: Transparent |

Assign them to **TrailSystem → Trail Core Material** and **Trail Glow Material**.

---

## 9. Camera Setup

1. Select **Main Camera**.
2. Attach **CameraController.cs**.
3. Set the camera's **Clear Flags** to **Solid Color**, background: very dark grey.
4. The camera height and tilt are controlled by **GameConfig.cameraHeight** (60) and
   **GameConfig.cameraTilt** (25°).

---

## 10. Lighting

1. Window → Rendering → Lighting → set **Ambient Color** to a dark blue-grey.
2. Add a **Directional Light** (already in scene by default):
   - Intensity: `1.0`
   - Rotation: `(50, -30, 0)`
   - Shadow Type: **Soft Shadows**
   - Shadow Resolution: **2048**

---

## 11. UI Canvas Setup

1. Create a **Canvas** (UI → Canvas):
   - Render Mode: **Screen Space – Overlay**
   - Canvas Scaler: **Scale With Screen Size**, Reference 1920×1080, Match 0.5
2. Add a **Canvas Group** to each screen panel for future fade animations.
3. The joystick background image should be anchored bottom-left with a size of ~120×120 px.

---

## 12. Input (Touch Joystick Wiring)

Select **InputManager** and assign:
- **Joystick Background** → the `JoystickBackground` RectTransform
- **Joystick Handle**     → the `JoystickHandle` RectTransform
- **Joystick Radius**: 35
- **Joystick Dead Zone**: 10

---

## 13. Build Settings

### Android
- Edit → Project Settings → Player → Android tab
- Minimum API Level: 22 (Android 5.1)
- Target API: 34
- Graphics API: **Vulkan** (primary), **OpenGLES3** (fallback)
- In URP Asset: disable shadows for mobile if targeting low-end devices.

### iOS
- Minimum iOS: 13.0
- Architecture: ARM64

### WebGL
- Compression: **Brotli**
- Memory Size: 256 MB
- Linker Target: **WebAssembly**
- In URP Asset: disable MSAA, set Shadow Distance to 50.

---

## 14. Performance Tips (Mobile 60 FPS)

These match the Voodoo-style optimisations mentioned in the brief:

| Problem | Solution |
|---------|----------|
| Territory draw calls | Single Texture2D on one plane (1 draw call) |
| Trail geometry | LineRenderer with `numCapVertices = 4` (avoids overdraw) |
| Trail GC | `List<Vector2>` reused, not reallocated on close |
| Spatial collision | Hash grid → O(1) per-player lookup |
| Bot updates | 200 ms decision interval (not every frame) |
| Texture upload | `texture.Apply(false)` only when `_textureDirty` |
| Camera | `LateUpdate` lerp, no physics camera |
| Render Scale | URP Render Scale 0.85 on mobile for ~20% GPU gain |

---

## 15. Gameplay Summary (for QA)

| Mechanic | Spec |
|----------|------|
| Grid size | 400 × 400 cells |
| Normal speed | 52 units/sec |
| Boost speed (own territory, no trail) | 72 units/sec |
| Turn speed | 3.5 rad/sec |
| Trail limit (auto-kill) | 800 points |
| Trail warning threshold | 280 points |
| Home territory stamp | 9 × 9 cells |
| Max bots | 9 |
| Bot respawn delay | 2 s |
| Territory capture | BFS flood-fill from map edges |

---

## File Reference

```
Assets/
  Scripts/
    Core/
      GameManager.cs        — game state machine + physics loop
      GameConfig.cs         — ScriptableObject config (all constants)
      CameraController.cs   — smooth follow + screen shake
      InputManager.cs       — keyboard / touch / joystick input
    Player/
      PlayerBase.cs         — shared movement, trail integration, visuals
      PlayerController.cs   — human player (reads InputManager)
    AI/
      BotController.cs      — FSM AI: Expand / ReturnHome / ChaseTrail / AvoidEnemy
      BotSpawner.cs         — spawns and respawns bots
    Map/
      MapManager.cs         — ground plane, grid lines, boundary walls
    Systems/
      TerritorySystem.cs    — grid ownership, flood-fill capture, Texture2D render
      TrailSystem.cs        — trail point lists + LineRenderer pairs per player
      CollisionSystem.cs    — spatial-hash collision detection
    UI/
      UIManager.cs          — all HUD: leaderboard, score, danger, kill feed, minimap
      LeaderboardEntry.cs   — data class for leaderboard rows
  Prefabs/
    Player.prefab
    Bot.prefab
  Materials/
    TrailCore.mat
    TrailGlow.mat
    Ground.mat
    TerritoryPlane.mat (created at runtime by TerritorySystem)
  Scenes/
    MainGame.unity
  ScriptableObjects/
    GameConfig.asset
```
