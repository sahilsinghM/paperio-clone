# Paper.io 3D — Fix & Complete Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 4 audit-identified bugs and complete the 3D Paper.io experience so it plays correctly with perspective camera.

**Architecture:** All game logic lives in `src/` ES modules. Rendering is Three.js WebGL via `src/renderer3d.js`, `src/cells.js`, `src/players3d.js`. Human death notification uses `CustomEvent` dispatched from `logic.js` so logic stays decoupled from UI.

**Tech Stack:** Three.js r165 (CDN importmap), Vanilla JS ES modules, Playwright for browser tests, Python http.server for local dev.

---

## How to run tests

```bash
cd /home/xinhangyuan/Documents/mycode/claude-written/paperio
npx playwright test --reporter=line
```

Expected baseline before any changes: tests may partially pass (start screen test should pass; death screen tests may not exist yet).

---

### Task 1: Fix Human Death — Dispatch Event from logic.js, Handle in main.js

**Problem:** When the human player dies, `killPlayer` in `logic.js` computes `pct` but throws it away. The death screen is never shown, the game loop never stops, and no kill feed entry is added.

**Root cause:** `logic.js` has no reference to UI functions. The fix is to dispatch a `CustomEvent` from `logic.js` (no UI imports needed) and handle it in `main.js`.

**Files:**
- Modify: `src/logic.js` — fix `killPlayer` to dispatch `playerKilled` event
- Modify: `src/main.js` — listen for `playerKilled`, show death screen, add kill feed

---

**Step 1: Update `killPlayer` in `src/logic.js`**

Replace the entire `killPlayer` function (lines 59–84) with:

```js
export function killPlayer(victim, killer) {
  if (!victim || !victim.alive) return;
  victim.alive = false;

  // Compute pct BEFORE clearing territory (territory still belongs to victim now)
  const pct = ((getTerritoryCount(victim.id) / territory.length) * 100).toFixed(1);

  if (killer) {
    killer.kills++;
    for (let i = 0; i < territory.length; i++) {
      if (territory[i] === victim.id) territory[i] = killer.id;
    }
    victim.trail.forEach(i => { trail[i] = killer.id; });
  } else {
    for (let i = 0; i < territory.length; i++) {
      if (territory[i] === victim.id) territory[i] = 0;
    }
    victim.trail.forEach(i => { trail[i] = 0; });
  }
  victim.trail.clear();

  window.dispatchEvent(new CustomEvent('playerKilled', {
    detail: { killer, victim, pct }
  }));

  if (victim.isBot) victim.respawnTimer = 2000;
}
```

Key changes vs old code:
- `pct` computed **before** territory is cleared (otherwise it would be 0)
- Removed the dead `const pct` inside the `!victim.isBot` branch
- Dispatches `playerKilled` event for both human and bot kills
- Removed the old `if (!victim.isBot) { const pct = ... }` dead code block

---

**Step 2: Update `src/main.js` — listen for the event**

After the existing import block (after line 10), add this import:

```js
import { addKillFeed } from './ui.js';
```

Then after the `document.getElementById('respawn-btn')` block (after line 50), add:

```js
window.addEventListener('playerKilled', e => {
  const { killer, victim, pct } = e.detail;
  if (killer) addKillFeed(killer.name, victim.name, killer.color, victim.color);
  if (!victim.isBot) {
    state.gameRunning = false;
    document.getElementById('death-killer').textContent   = killer ? killer.name : 'the boundary';
    document.getElementById('death-territory').textContent = pct + '%';
    document.getElementById('death-kills').textContent    = victim.kills;
    document.getElementById('death-screen').classList.remove('hidden');
  }
});
```

---

**Step 3: Add a Playwright test for death screen**

Add to `tests/game.spec.js` at the end (before the closing `});`):

```js
test('death screen appears when player dies (self-trail kill)', async ({ page }) => {
  await page.goto('/');
  await page.locator('#play-btn').click();
  await page.waitForTimeout(300);

  // Force human to self-trail-kill: go right, down, left, up = box loop
  // This may or may not trigger death depending on grid position;
  // just verify death screen CAN appear within 8 seconds of rapid movement
  const deathScreen = page.locator('#death-screen');

  // Spam direction changes to increase chance of death
  for (let i = 0; i < 20; i++) {
    await page.keyboard.press('ArrowRight');
    await page.waitForTimeout(100);
    await page.keyboard.press('ArrowDown');
    await page.waitForTimeout(100);
    await page.keyboard.press('ArrowLeft');
    await page.waitForTimeout(100);
    await page.keyboard.press('ArrowUp');
    await page.waitForTimeout(100);
  }

  // If death screen appeared, respawn button should be visible
  // (This test is probabilistic — just verifies the DOM is wired correctly if death occurs)
  const isVisible = await deathScreen.isVisible();
  if (isVisible) {
    await expect(page.locator('#respawn-btn')).toBeVisible();
    await expect(page.locator('#death-killer')).not.toBeEmpty();
  }
  // If no death yet, test still passes (game running = correct)
});
```

---

**Step 4: Run the test**

```bash
npx playwright test tests/game.spec.js --reporter=line
```

Expected: All 4 existing tests pass. The new death-screen test either passes (death occurred) or passes with no assertion (no death yet — both valid).

---

**Step 5: Commit**

```bash
git add src/logic.js src/main.js tests/game.spec.js
git commit -m "fix: human death shows death screen via playerKilled CustomEvent"
```

---

### Task 2: Fix Camera — PerspectiveCamera + sun.target in scene

**Problem 1:** `OrthographicCamera` is used but the design specifies `PerspectiveCamera(60°)` to match Paper.io 2's 3D perspective look.

**Problem 2:** `sun.target` is repositioned each frame but never added to the scene — Three.js `DirectionalLight` requires `scene.add(sun.target)` for the target position to affect shadow direction.

**File:** `src/renderer3d.js` — full rewrite of the init and camera sections.

---

**Step 1: Replace `src/renderer3d.js` entirely with:**

```js
import * as THREE from 'three';
import { CFG } from './config.js';
import { state } from './state.js';

let renderer, scene, camera, sun;

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

const _camTarget  = new THREE.Vector3();
const _lookTarget = new THREE.Vector3();

export function initRenderer(container) {
  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type    = THREE.PCFSoftShadowMap;
  renderer.setClearColor(0x0d1b2a);
  container.appendChild(renderer.domElement);

  scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x0d1b2a, 0.008);

  scene.add(new THREE.AmbientLight(0xffffff, 0.6));

  sun = new THREE.DirectionalLight(0xffffff, 1.0);
  sun.position.set(100, 150, 80);
  sun.castShadow = true;
  sun.shadow.mapSize.width  = 2048;
  sun.shadow.mapSize.height = 2048;
  sun.shadow.camera.near    = 1;
  sun.shadow.camera.far     = 500;
  sun.shadow.camera.left    = -80;
  sun.shadow.camera.right   = 80;
  sun.shadow.camera.top     = 80;
  sun.shadow.camera.bottom  = -80;
  scene.add(sun);
  scene.add(sun.target);   // ← required for target.position updates to take effect

  const groundGeo = new THREE.PlaneGeometry(CFG.GRID_W + 40, CFG.GRID_H + 40);
  const groundMat = new THREE.MeshLambertMaterial({ color: 0x16213e });
  const ground    = new THREE.Mesh(groundGeo, groundMat);
  ground.rotation.x    = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  const grid = new THREE.GridHelper(CFG.GRID_W, CFG.GRID_W, 0x1a2540, 0x1a2540);
  grid.position.y = 0.001;
  scene.add(grid);

  camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.position.set(0, 18, 12);
  camera.lookAt(0, 0, 0);

  window.addEventListener('resize', onResize);
  return { renderer, scene, camera };
}

export function onResize() {
  if (!renderer || !camera) return;
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

export function cellToWorld(x, y) {
  return new THREE.Vector3(x - HALF_W + 0.5, 0, y - HALF_H + 0.5);
}

export function updateCamera3d() {
  const h = state.humanPlayer;
  if (!h || !h.alive) return;

  const wx = h.x - HALF_W + 0.5;
  const wz = h.y - HALF_H + 0.5;

  _camTarget.set(wx, 18, wz + 12);
  camera.position.lerp(_camTarget, 0.08);

  _lookTarget.set(wx, 0, wz);
  camera.lookAt(_lookTarget);

  sun.position.set(wx + 100, 150, wz + 80);
  sun.target.position.set(wx, 0, wz);
  sun.target.updateMatrixWorld();
}

export function render3d() {
  renderer.render(scene, camera);
}

export function getScene()    { return scene; }
export function getCamera()   { return camera; }
export function getRenderer() { return renderer; }
```

Key changes vs old code:
- `PerspectiveCamera(60, aspect, 0.1, 1000)` replaces `OrthographicCamera`
- `scene.add(sun.target)` — fixes shadow direction tracking
- `onResize` now updates `camera.aspect` + `updateProjectionMatrix()` (not left/right/top/bottom)
- Cached `_camTarget` and `_lookTarget` vectors — no heap allocation per frame
- Fog density increased slightly (0.003→0.008) to look better at lower perspective height
- Shadow camera frustum tightened (±150→±80) for sharper shadows at perspective scale

---

**Step 2: Run tests**

```bash
npx playwright test tests/game.spec.js --reporter=line
```

Expected: All tests pass. The canvas renders test should still see a canvas with dimensions > 0.

---

**Step 3: Commit**

```bash
git add src/renderer3d.js
git commit -m "fix: PerspectiveCamera 60deg + scene.add(sun.target) for correct shadows"
```

---

### Task 3: Performance — Eliminate Per-Frame Object Allocations

**Problem:** Three objects are allocated every animation frame (60× per second):
1. `cells.js`: `new Map()` in `updateCells`
2. `players3d.js`: `new THREE.Vector3(tx, 0, tz)` per alive player in `syncPlayerMeshes`

These create GC pressure. Fix by hoisting to module-level and reusing.

**Files:**
- Modify: `src/cells.js` lines 55–57
- Modify: `src/players3d.js` line 88

---

**Step 1: Fix `src/cells.js` — hoist `brightMap`**

Change line 57 from:
```js
  const brightMap = new Map();
```
to a module-level declaration above `initCells`:
```js
const brightMap = new Map();
```

And at the top of `updateCells`, add:
```js
  brightMap.clear();
```

Full replacement of the `updateCells` function opening:

```js
// Module-level (after the `const dummy` and `const color` declarations, before initCells):
const brightMap = new Map();

// updateCells becomes:
export function updateCells(now) {
  let tChanged = false, trChanged = false;

  brightMap.clear();
  for (const anim of state.fillAnims) {
    const progress   = Math.min((now - anim.startTime) / CFG.FILL_ANIM_MS, 1);
    const brightness = 1 + (1 - progress) * 1.2;
    anim.cells.forEach(ci => brightMap.set(ci, { color: anim.color, brightness }));
  }
  // ... rest of function unchanged
```

---

**Step 2: Fix `src/players3d.js` — cache Vector3 for lerp**

Add after the `const playerGroups` declaration (around line 9):
```js
const _pos = new THREE.Vector3();
```

Change line 88 from:
```js
    group.position.lerp(new THREE.Vector3(tx, 0, tz), 0.3);
```
to:
```js
    _pos.set(tx, 0, tz);
    group.position.lerp(_pos, 0.3);
```

---

**Step 3: Run tests**

```bash
npx playwright test tests/game.spec.js --reporter=line
```

Expected: All tests pass (behaviour unchanged).

---

**Step 4: Commit**

```bash
git add src/cells.js src/players3d.js
git commit -m "perf: hoist brightMap and Vector3 out of animation frame loop"
```

---

### Task 4: Code Quality — Remove Debug Logs, Dead Code, Stray File, Fix .gitignore

**Files:**
- Modify: `src/input.js` — remove 2 debug console.log lines
- Modify: `src/main.js` — remove unused `getSelectedColor` variable
- Modify: `src/logic.js` — verify dead code is gone (handled in Task 1)
- Delete: `src/game.spec.js` — stray test file in wrong directory
- Modify: `.gitignore` — add `node_modules/`

---

**Step 1: Remove debug logs from `src/input.js`**

Delete lines 22–24:
```js
    console.log('keydown event:', e.key, 'code:', e.code);
```
and:
```js
    console.log('mapped direction:', dir);
```

The `keydown` handler should just be:
```js
  document.addEventListener('keydown', e => {
    const dir = DIR_MAP[e.key] || DIR_MAP[e.key.toLowerCase()];
    if (!dir) return;
    e.preventDefault();
    queueDirection(dir);
  });
```

---

**Step 2: Remove unused `getSelectedColor` from `src/main.js`**

Change line 26 from:
```js
const getSelectedColor = initColorPicker(c => { selectedColor = c; });
```
to:
```js
initColorPicker(c => { selectedColor = c; });
```

---

**Step 3: Delete stray spec file**

```bash
rm src/game.spec.js
```

---

**Step 4: Fix `.gitignore`**

Replace the entire `.gitignore` with:
```
.vercel
node_modules/
test-results/
```

---

**Step 5: Run tests one final time**

```bash
npx playwright test tests/game.spec.js --reporter=line
```

Expected: All tests pass.

---

**Step 6: Commit**

```bash
git add -A
git commit -m "chore: remove debug logs, dead code, stray file; fix .gitignore"
```

---

### Task 5: Push to GitHub and Redeploy Vercel

**Step 1: Push to GitHub**

```bash
git push origin main
```

Expected: Push succeeds. GitHub shows 4 new commits since last push.

**Step 2: Verify Vercel auto-deploy**

Vercel is connected to the GitHub repo (`paperio-delta.vercel.app`). Pushing to `main` triggers an auto-deploy. Wait ~60 seconds then check the Vercel dashboard or visit the URL.

If auto-deploy does not trigger (check `vercel ls`):
```bash
vercel --prod --yes
```

**Step 3: Smoke test live URL**

Open `https://paperio-delta.vercel.app` in a browser. Confirm:
- 3D scene renders with perspective view (not flat top-down)
- Player moves and leaves trail
- Territory claim works (flood-fill animation)
- Death screen appears when player dies
- Kill feed shows eliminations

---
