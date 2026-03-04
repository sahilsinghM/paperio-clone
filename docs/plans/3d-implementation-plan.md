# Paper.io 3D Implementation Plan

## Goal
Convert the existing 2D Paper.io clone to 3D using Three.js, matching Paper.io 2's visual style.

## Current State
- `index.html` - Working 2D game (~864 lines)
- No `src/` directory exists yet
- 2D canvas rendering

---

## Phase 1: Project Setup

### Task 1.1: Create Project Structure
```
src/
├── config.js      # CFG constants (move from index.html)
├── state.js       # territory[], trail[], players[], game state
├── logic.js       # movement, collision, flood-fill
├── ai.js          # bot state machine
├── input.js       # keyboard/touch handlers
├── ui.js          # kill feed, HUD
├── renderer3d.js   # Three.js scene, camera
├── cells.js       # InstancedMesh for territory/trail
├── players3d.js   # 3D player meshes
└── main.js        # entry point, game loop
```

### Task 1.2: Set Up ES Modules
- Convert `index.html` to load modules via importmap
- Add Three.js importmap pointing to CDN

---

## Phase 2: Extract Game Logic

### Task 2.1: Extract Config & State
- Move CFG object to `src/config.js`
- Move territory/trail arrays to `src/state.js`
- Move helper functions (idx, inBounds, hexToRgb, etc.)

### Task 2.2: Extract Logic Module
- Move player creation, movement, collision
- Move territory flood-fill algorithm
- Keep using 2D canvas temporarily (canvas shim)

### Task 2.3: Extract AI Module
- Move bot state machine
- Move BFS pathfinding

### Task 2.4: Extract Input & UI
- Move keyboard/touch/d-pad handlers
- Move kill feed, leaderboard, score rendering
- Create HUD overlay canvas

### Task 2.5: Wire & Verify
- Import all modules in `main.js`
- Verify game works identically before 3D work

---

## Phase 3: Three.js Integration

### Task 3.1: Scene Setup
- Create `src/renderer3d.js`
- Initialize WebGLRenderer with shadows
- Add scene, fog, ambient light, directional light
- Add ground plane
- Set up PerspectiveCamera with follow behavior

### Task 3.2: InstancedMesh Cells
- Create `src/cells.js`
- Create InstancedMesh for territory (flat boxes)
- Create InstancedMesh for trail (raised boxes)
- Implement dirty-flag tracking for updates
- Add fill animation brightness effect

### Task 3.3: Player Meshes
- Create `src/players3d.js`
- Create BoxGeometry player bodies
- Add direction cone indicator
- Add CSS2DRenderer for name labels
- Add PointLight for glow effect

---

## Phase 4: Integration & Polish

### Task 4.1: Wire 3D Renderer
- Replace 2D canvas with Three.js renderer
- Connect updateCells() to game loop
- Connect syncPlayerMeshes() to game loop

### Task 4.2: Move Minimap to HUD
- Render minimap on HUD canvas overlay
- Remove old 2D canvas dependency

### Task 4.3: Camera Polish
- Smooth lerp follow
- Add subtle grid helper
- Add player bob animation

### Task 4.4: Testing
- Full gameplay test
- Mobile touch test
- Performance check

---

## Dependencies
- Three.js r165 (CDN via importmap)
- No build tools required
- Works directly in browser

---

## Estimated Tasks: 16
- Phase 1: 2 tasks
- Phase 2: 5 tasks  
- Phase 3: 3 tasks
- Phase 4: 4 tasks
- Testing: 2 tasks
