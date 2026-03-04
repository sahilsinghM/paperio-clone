# Paper.io 2 — 3D Rendering Design

Date: 2026-03-04

## Goal
Replace the Canvas 2D rendering layer with Three.js WebGL to match the Paper.io 2 visual style: flat ground plane in perspective 3D, 3D player characters, fixed follow camera, low-poly aesthetic with lighting.

## What Changes vs What Stays

### Unchanged (100% reused)
- `Uint8Array` grid (`territory[]`, `trail[]`)
- Flood-fill (`claimTerritory`)
- All collision + movement logic (`movePlayer`, `killPlayer`, `tickPlayers`)
- AI bots (`tickBot`, `bfsDirection`)
- Input system (keyboard, touch, d-pad)
- Game loop structure (fixed-step accumulator, delta time cap)
- Kill feed, leaderboard, score (HTML DOM overlays)
- Start/death screens

### Replaced
- Canvas 2D context + all `draw*` functions → Three.js WebGL scene
- `updateCamera` → Three.js `PerspectiveCamera` with smooth lerp follow
- Minimap 2D `ImageData` → `OrthographicCamera` + `WebGLRenderTarget`

### Added
- Three.js r165 via CDN importmap (no build step)
- `THREE.Scene`, `THREE.WebGLRenderer`, lights, fog
- `InstancedMesh` for territory + trail cells
- Per-player `Mesh` (RoundedBoxGeometry) + name label (CSS2DRenderer)

## File Structure

```
index.html          — HTML shell, screens, HUD overlay (CSS unchanged)
src/
  main.js           — entry point, wires everything together
  config.js         — CFG constants (grid size, colors, speeds)
  state.js          — territory[], trail[], players[], game state vars
  input.js          — keyboard / touch / d-pad handlers
  logic.js          — movement, collision, claimTerritory, flood-fill
  ai.js             — bot state machine, bfsDirection
  renderer3d.js     — Three.js scene setup, instanced meshes, camera
  minimap.js        — orthographic camera, WebGLRenderTarget, minimap canvas
  ui.js             — kill feed, leaderboard, score, start/death screens
```

Three.js loaded via `<script type="importmap">` pointing to CDN. No bundler needed — works directly in browser via `http://` (Vercel or local server).

## Rendering Architecture

### Scene
- `WebGLRenderer` with `antialias: true`, `shadowMap.enabled: true`
- Background: `#0d1b2a` (deep navy)
- `FogExp2` for depth fade at grid edges
- `AmbientLight(0xffffff, 0.5)` — soft fill
- `DirectionalLight(0xffffff, 1.0)` — top-right angle, casts shadows

### Ground
- One large `PlaneGeometry(GRID_W, GRID_H)` centered at origin
- Material: `MeshLambertMaterial({ color: #16213e })` with subtle grid via custom vertex colors or texture

### Territory + Trail — InstancedMesh
- `territoryMesh`: `InstancedMesh` of `BoxGeometry(0.92, 0.08, 0.92)` — 62,500 instances
- `trailMesh`: `InstancedMesh` of `BoxGeometry(0.78, 0.18, 0.78)` — 62,500 instances
- Each frame: iterate only dirty cells (cells changed since last frame) and update instance matrix + color
- Dirty tracking: a `Uint8Array dirtyFlags` — set on every `territory[]`/`trail[]` write
- Invisible instances: scale matrix to (0,0,0) for empty cells
- Both meshes receive + cast shadows

### Player Meshes
- One `Group` per player containing:
  - Body: `BoxGeometry(0.7, 0.7, 0.7)` with rounded appearance (or `CapsuleGeometry`)
  - Direction cone: small `ConeGeometry` child, rotated to face `(dx, 0, dy)`
  - Name label: `CSS2DObject` (Three.js CSS2DRenderer) — `<div>` rendered in screen space above the mesh
- Player group position updated each frame to `(p.x, 0.35, p.y)` (grid coords → world coords 1:1)
- Glow: `PointLight` attached to each player group, color = player color, low intensity

### Camera
- `PerspectiveCamera(60°, aspect, 0.1, 1000)`
- Follows human player: target = `(p.x, 0, p.y)` offset by `(0, 18, 12)` (above and behind)
- Smooth lerp: `cam.position.lerp(target, 0.1)` each frame
- `cam.lookAt(p.x, 0, p.y)` — always looks at player
- Fixed angle — no user rotation (matches Paper.io 2)

### Minimap
- Second `OrthographicCamera` positioned directly above, looking straight down
- Renders to `WebGLRenderTarget(256, 256)`
- Result drawn as a scaled `<canvas>` DOM overlay (top-right corner)
- Updated every 3 frames to reduce GPU load

## Coordinate System
- Grid cell `(x, y)` maps to Three.js world `(x - GRID_W/2, 0, y - GRID_H/2)`
- Origin (0,0,0) is the center of the grid
- Y axis is up; ground plane is at Y=0

## Performance Strategy
- `InstancedMesh` for cells: 1 draw call for all territory, 1 for all trails (vs 62,500 fillRect calls)
- Dirty-flag system: only update changed cell instances per frame
- CSS2DRenderer for name labels: no texture atlas needed
- Minimap at 3-frame interval
- Player PointLights: intensity 0 when player is dead, removed on respawn cycle
- Bot players beyond camera frustum: still simulated, mesh updated but culled by Three.js automatically

## Dependencies
- Three.js r165 (importmap CDN): `three`, `three/addons/renderers/CSS2DRenderer.js`, `three/addons/geometries/RoundedBoxGeometry.js`
- No build step, no npm install
