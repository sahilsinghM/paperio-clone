# Paper.io 2 Clone — Design Document
Date: 2026-03-04

## Overview
A fully client-side browser game clone of Paper.io 2. Single `index.html` file, no dependencies, no backend. Opens directly in any browser.

## Tech Stack
- **Vanilla JS + Canvas 2D** — chosen over Phaser (overkill) and Three.js (2D game needs no WebGL). Direct `Uint8Array` grid maps perfectly to Canvas pixel operations.

## Data Structures

### World Grid
- Two flat `Uint8Array` arrays of size `GRID_W × GRID_H` (~250×250 = 62,500 cells)
  - `territory[]`: player ID owning each cell (0 = empty)
  - `trail[]`: player ID whose trail is on each cell (0 = no trail)
- Cell index formula: `y * GRID_W + x`
- O(1) lookup, zero GC pressure, trivial flood-fill iteration

### Player Object
```js
{
  id,           // 1-based integer
  x, y,         // current grid position (integer)
  dx, dy,       // direction vector (-1/0/1)
  color,        // hex string
  territory,    // Set<cellIndex>
  trail,        // Set<cellIndex> (cells laid since leaving home)
  alive,        // boolean
  score,        // territory cell count
  isBot,        // boolean
  botState,     // 'expand' | 'attack' | 'retreat'
  botTimer,     // ms until next direction decision
  kills,        // kill count
  name          // display name
}
```

## Core Systems

| System | Responsibility |
|--------|----------------|
| Game Loop | `requestAnimationFrame` + delta-time accumulator, fixed 16ms physics tick |
| Input | Arrow/WASD direction queue, prevents 180° reversal; touch swipe support |
| Movement | Advance all players each tick, detect collisions, handle kills |
| Territory | On trail closure: scanline flood-fill, update grid arrays, animate fill |
| Rendering | Camera transform → grid → trails → players → HUD (minimap, leaderboard, kill feed) |
| AI | State machine per bot: expand → attack → retreat |

## Collision Rules
- Player crosses **own trail** → self-kill
- Player crosses **enemy trail** → enemy dies, attacker scores a kill
- Player head meets **another player head** → both die
- Player reaches **own territory** with active trail → territory claim triggered

## Territory Flood-Fill
- Scanline algorithm on `Uint8Array` (not recursive — avoids stack overflow on large areas)
- Triggered when a trail cell connects back to the player's own territory
- Algorithm: use the trail as a boundary, flood-fill enclosed regions
- Uses a temporary working buffer to avoid mutating the grid mid-fill
- Animated: cells fill in wave pattern over ~300ms

## AI Bot Design
Three states, evaluated every ~200ms per bot:
- **expand**: move toward nearest empty/enemy territory using BFS to find open space
- **attack**: if an enemy trail is adjacent and bot has a short trail, cross it
- **retreat**: if trail length > threshold OR enemy nearby, head directly home via shortest path

## Rendering Pipeline
1. Clear viewport (camera-transformed region only)
2. Draw visible grid cells (culled — only cells in viewport)
3. Draw trails on top (colored rectangles)
4. Draw player heads (circle + direction dot)
5. Draw HUD without camera transform:
   - Minimap (offscreen `ImageData` → scaled `drawImage`)
   - Leaderboard (top-right)
   - Kill feed (top-left, fades out)
   - Score (bottom-center)

### Minimap
- Full `Uint8Array` → `ImageData` (1px per cell) → `drawImage` scaled to ~150×150px
- Updated every frame (fast since it's just typed array iteration)

### Camera
- Follows player head, centered in viewport
- Cell size: 16px at base zoom
- Zoom out slightly as territory grows

## File Structure (single index.html)
```
index.html
  <style>     viewport canvas, overlay screens, HUD styling
  <canvas>    id="game"
  <script>
    // CONFIG       — grid size, cell size, colors, speeds, bot count
    // STATE        — grid arrays, players[], gameState enum
    // INPUT        — keyboard + touch event handlers
    // GAME LOGIC   — movement, collision, territory claim, flood-fill
    // AI           — bot state machine, pathfinding helpers
    // RENDERING    — camera, draw functions, minimap, HUD
    // UI           — start screen, death screen, kill feed, leaderboard
    // LOOP         — requestAnimationFrame, delta time, tick
```

Estimated: ~900 lines of well-structured JS.

## UI Screens
- **Start screen**: name input, color picker (6 presets), "Play" button
- **Death screen**: killer name, territory % at death, kill count, "Respawn" button
- **Kill feed**: sliding list, top-left, entries fade after 4s
- **Leaderboard**: live ranking by territory %, top-right, top 5 shown

## Performance Notes
- Grid operations on `Uint8Array` — no object allocation in hot path
- Rendering culled to viewport — don't draw offscreen cells
- Flood-fill uses iterative scanline — O(n) where n = enclosed area
- Minimap uses `ImageData` direct pixel write — fastest possible
- Bot pathfinding is BFS limited to 50-cell radius — bounded cost
