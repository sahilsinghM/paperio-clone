# Paper.io 2 Clone Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a fully playable single-file Paper.io 2 browser clone with AI bots, flood-fill territory, minimap, leaderboard, and kill feed.

**Architecture:** Single `index.html` with embedded CSS and JS, organized in clearly commented sections: CONFIG → STATE → INPUT → GAME LOGIC → AI → RENDERING → UI → LOOP. A flat `Uint8Array` grid backs all territory/trail state for maximum performance.

**Tech Stack:** Vanilla JS, Canvas 2D API, no dependencies.

**Security note:** All user-supplied strings (player names) MUST be set via `textContent` or safe DOM construction — never `innerHTML`. Player names are sanitized to alphanumeric + spaces on input.

---

## Task 1: HTML Shell + Canvas Setup

**Files:**
- Create: `index.html`

**Step 1: Create the HTML shell**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Paper.io 2</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { background: #1a1a2e; overflow: hidden; font-family: 'Segoe UI', sans-serif; }
    #game { display: block; }

    .screen {
      position: fixed; inset: 0;
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      background: rgba(10, 10, 30, 0.92);
      z-index: 100;
    }
    .screen.hidden { display: none; }

    .screen h1 { color: #fff; font-size: 3rem; font-weight: 900;
      text-shadow: 0 0 20px rgba(100,200,255,0.8); margin-bottom: 0.5rem; }
    .screen p  { color: #aaa; font-size: 1rem; margin-bottom: 2rem; }

    .name-input {
      padding: 0.75rem 1.5rem; font-size: 1.1rem; border-radius: 8px;
      border: 2px solid #444; background: #222; color: #fff;
      margin-bottom: 1.5rem; text-align: center; width: 240px;
      outline: none; transition: border-color 0.2s;
    }
    .name-input:focus { border-color: #4af; }

    .color-picker { display: flex; gap: 12px; margin-bottom: 2rem; }
    .color-swatch {
      width: 36px; height: 36px; border-radius: 50%;
      cursor: pointer; border: 3px solid transparent;
      transition: transform 0.15s, border-color 0.15s;
    }
    .color-swatch:hover    { transform: scale(1.15); }
    .color-swatch.selected { border-color: #fff; transform: scale(1.2); }

    .btn {
      padding: 0.9rem 3rem; font-size: 1.2rem; font-weight: 700;
      border: none; border-radius: 30px; cursor: pointer;
      background: linear-gradient(135deg, #4af, #26f);
      color: #fff; letter-spacing: 1px;
      transition: transform 0.15s, box-shadow 0.15s;
      box-shadow: 0 4px 20px rgba(68,170,255,0.4);
    }
    .btn:hover { transform: translateY(-2px); box-shadow: 0 8px 30px rgba(68,170,255,0.5); }
    .btn:active { transform: translateY(0); }

    #death-screen .stat { color: #ccc; font-size: 1.1rem; margin: 0.3rem 0; }
    #death-screen .stat span { color: #fff; font-weight: 700; }
    #death-screen h2 { color: #ff6b6b; font-size: 2rem; margin-bottom: 1rem; }

    #kill-feed {
      position: fixed; top: 16px; left: 16px;
      display: flex; flex-direction: column; gap: 6px;
      pointer-events: none; z-index: 50;
    }
    .kill-entry {
      background: rgba(0,0,0,0.65); color: #fff;
      padding: 6px 12px; border-radius: 20px;
      font-size: 0.85rem; border-left: 3px solid #4af;
      animation: slideIn 0.2s ease-out;
      transition: opacity 0.5s;
    }
    @keyframes slideIn { from { opacity: 0; transform: translateX(-20px); } }

    #dpad {
      position: fixed; bottom: 24px; left: 24px;
      display: none; z-index: 50;
    }
    @media (max-width: 768px) { #dpad { display: grid; } }
    #dpad { grid-template-columns: 48px 48px 48px; grid-template-rows: 48px 48px 48px; gap: 4px; }
    .dpad-btn {
      background: rgba(255,255,255,0.15); border: none; border-radius: 8px;
      color: #fff; font-size: 1.4rem; cursor: pointer;
      display: flex; align-items: center; justify-content: center;
      transition: background 0.1s;
    }
    .dpad-btn:active { background: rgba(255,255,255,0.35); }
  </style>
</head>
<body>

<div id="start-screen" class="screen">
  <h1>Paper.io 2</h1>
  <p>Claim territory. Eliminate rivals.</p>
  <input id="player-name" class="name-input" type="text" placeholder="Your name" maxlength="16" value="Player">
  <div class="color-picker" id="color-picker"></div>
  <button class="btn" id="play-btn">PLAY</button>
</div>

<div id="death-screen" class="screen hidden">
  <h2>You were eliminated!</h2>
  <div class="stat">Eliminated by: <span id="death-killer"></span></div>
  <div class="stat">Territory at death: <span id="death-territory"></span></div>
  <div class="stat">Kills: <span id="death-kills"></span></div>
  <br>
  <button class="btn" id="respawn-btn">RESPAWN</button>
</div>

<div id="kill-feed"></div>

<div id="dpad">
  <div></div>
  <button class="dpad-btn" data-dir="up">↑</button>
  <div></div>
  <button class="dpad-btn" data-dir="left">←</button>
  <div></div>
  <button class="dpad-btn" data-dir="right">→</button>
  <div></div>
  <button class="dpad-btn" data-dir="down">↓</button>
  <div></div>
</div>

<canvas id="game"></canvas>

<script>
// Placeholder — will be replaced in Task 2
document.getElementById('play-btn').addEventListener('click', () => {
  document.getElementById('start-screen').classList.add('hidden');
  alert('Game shell ready!');
});
</script>
</body>
</html>
```

**Step 2: Open in browser and verify**
- Open `index.html` directly
- Should see dark background, "Paper.io 2" title, name input, color picker row, Play button
- Clicking Play hides the screen and shows alert

---

## Task 2: CONFIG + STATE + Canvas Init

**Files:**
- Modify: `index.html` (replace the `<script>` block entirely)

**Step 1: Replace script block with CONFIG + STATE**

```js
// ============================================================
// SECURITY: sanitize player names — strip anything not
// alphanumeric, space, or common punctuation.
// This ensures names are safe to use in textContent calls.
// ============================================================
function sanitizeName(raw) {
  return raw.replace(/[^a-zA-Z0-9 '_\-\.]/g, '').trim().slice(0, 16) || 'Player';
}

// ============================================================
// CONFIG
// ============================================================
const CFG = {
  GRID_W:       250,
  GRID_H:       250,
  CELL:         16,
  SPEED:        8,           // cells/second
  BOT_COUNT:    9,
  TRAIL_LIMIT:  300,
  BOT_THINK_MS: 180,
  FILL_ANIM_MS: 350,
  KILL_FEED_MS: 4000,
  MINIMAP_SIZE: 150,
  MINIMAP_PAD:  12,
  COLORS: ['#e74c3c','#3498db','#2ecc71','#f39c12','#9b59b6',
           '#1abc9c','#e91e63','#ff5722','#00bcd4','#8bc34a'],
};

// ============================================================
// STATE
// ============================================================
const territory = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
const trail     = new Uint8Array(CFG.GRID_W * CFG.GRID_H);

let players     = [];
let nextId      = 1;
let gameRunning = false;
let lastTime    = 0;
let tickAccum   = 0;
const TICK_MS   = 1000 / CFG.SPEED;

const cam       = { x: 0, y: 0 };
const killFeed  = [];   // { el, time }
const fillAnims = [];   // { cells: Set, color, startTime }

// ============================================================
// CANVAS
// ============================================================
const canvas = document.getElementById('game');
const ctx    = canvas.getContext('2d');

function resizeCanvas() {
  canvas.width  = window.innerWidth;
  canvas.height = window.innerHeight;
}
resizeCanvas();
window.addEventListener('resize', resizeCanvas);

// roundRect polyfill for Safari < 15.4
if (!CanvasRenderingContext2D.prototype.roundRect) {
  CanvasRenderingContext2D.prototype.roundRect = function(x, y, w, h, r) {
    this.beginPath();
    this.moveTo(x + r, y);
    this.lineTo(x + w - r, y);
    this.quadraticCurveTo(x + w, y, x + w, y + r);
    this.lineTo(x + w, y + h - r);
    this.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    this.lineTo(x + r, y + h);
    this.quadraticCurveTo(x, y + h, x, y + h - r);
    this.lineTo(x, y + r);
    this.quadraticCurveTo(x, y, x + r, y);
    this.closePath();
  };
}

// ============================================================
// HELPERS
// ============================================================
function idx(x, y)      { return y * CFG.GRID_W + x; }
function inBounds(x, y) { return x >= 0 && x < CFG.GRID_W && y >= 0 && y < CFG.GRID_H; }

function randomEmptyCell() {
  let x, y, attempts = 0;
  do {
    x = 10 + Math.floor(Math.random() * (CFG.GRID_W - 20));
    y = 10 + Math.floor(Math.random() * (CFG.GRID_H - 20));
    attempts++;
  } while (territory[idx(x, y)] !== 0 && attempts < 500);
  return { x, y };
}

function getPlayerById(id)   { return players.find(p => p.id === id); }
function playerColor(id)     { const p = getPlayerById(id); return p ? p.color : '#333'; }

function getTerritoryCount(id) {
  let n = 0;
  for (let i = 0; i < territory.length; i++) if (territory[i] === id) n++;
  return n;
}

// Color hex → [r, g, b] with cache
const colorCache = {};
function hexToRgb(hex) {
  if (colorCache[hex]) return colorCache[hex];
  const r = parseInt(hex.slice(1,3), 16);
  const g = parseInt(hex.slice(3,5), 16);
  const b = parseInt(hex.slice(5,7), 16);
  return (colorCache[hex] = [r, g, b]);
}
```

**Step 2: Verify — open browser, click Play, confirm no errors in DevTools console.**

---

## Task 3: Player Creation + Start Screen Wiring

**Files:**
- Modify: `index.html` (append to script block)

**Step 1: Add createPlayer + respawn functions**

```js
// ============================================================
// PLAYER CREATION
// ============================================================
function createPlayer(name, color, isBot = false) {
  const id      = nextId++;
  const { x, y } = randomEmptyCell();
  const p = {
    id, name, color, isBot,
    x, y, dx: 1, dy: 0,
    trail:        new Set(),
    alive:        true,
    kills:        0,
    botState:     'expand',
    botTimer:     0,
    respawnTimer: 0,
    inputQueue:   [],
  };
  stampHome(p);
  players.push(p);
  return p;
}

function stampHome(p) {
  for (let dy = -1; dy <= 1; dy++)
    for (let dx = -1; dx <= 1; dx++)
      if (inBounds(p.x + dx, p.y + dy))
        territory[idx(p.x + dx, p.y + dy)] = p.id;
}

function respawnBot(p) {
  const { x, y } = randomEmptyCell();
  p.x = x; p.y = y; p.dx = 1; p.dy = 0;
  p.trail.clear(); p.alive = true;
  stampHome(p);
}

function respawnHuman() {
  const h       = window.humanPlayer;
  const { x, y } = randomEmptyCell();
  h.x = x; h.y = y; h.dx = 1; h.dy = 0;
  h.trail.clear(); h.alive = true; h.kills = 0;
  stampHome(h);
}
```

**Step 2: Add start-screen UI wiring**

```js
// ============================================================
// START SCREEN
// ============================================================
let playerColor = CFG.COLORS[0];

const colorPickerEl = document.getElementById('color-picker');
CFG.COLORS.forEach((c, i) => {
  const swatch = document.createElement('div');
  swatch.className = 'color-swatch' + (i === 0 ? ' selected' : '');
  swatch.style.background = c;
  swatch.addEventListener('click', () => {
    document.querySelectorAll('.color-swatch').forEach(s => s.classList.remove('selected'));
    swatch.classList.add('selected');
    playerColor = c;
  });
  colorPickerEl.appendChild(swatch);
});

const BOT_NAMES = ['Zara','Nova','Pixel','Echo','Blaze','Frost','Vex','Onyx','Cleo'];

function startGame() {
  territory.fill(0);
  trail.fill(0);
  players = []; nextId = 1;
  killFeed.length = 0;
  fillAnims.length = 0;

  const rawName = document.getElementById('player-name').value;
  const name    = sanitizeName(rawName);
  window.humanPlayer = createPlayer(name, playerColor, false);

  for (let i = 0; i < CFG.BOT_COUNT; i++) {
    const usedColors = new Set(players.map(p => p.color));
    const botColor   = CFG.COLORS.find(c => !usedColors.has(c)) || CFG.COLORS[i % CFG.COLORS.length];
    createPlayer(BOT_NAMES[i] || `Bot-${i+1}`, botColor, true);
  }

  document.getElementById('start-screen').classList.add('hidden');
  gameRunning = true;
  lastTime    = performance.now();
  requestAnimationFrame(gameLoop);
}

document.getElementById('play-btn').addEventListener('click', startGame);

document.getElementById('respawn-btn').addEventListener('click', () => {
  document.getElementById('death-screen').classList.add('hidden');
  respawnHuman();
  gameRunning = true;
  lastTime    = performance.now();
  requestAnimationFrame(gameLoop);
});

// Placeholder game loop (replaced in Task 7)
function gameLoop(ts) {
  if (!gameRunning) return;
  ctx.fillStyle = '#16213e';
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = '#fff';
  ctx.font = '18px sans-serif';
  ctx.fillText(`Players: ${players.length}  Human: ${humanPlayer.x},${humanPlayer.y}`, 20, 40);
  requestAnimationFrame(gameLoop);
}
```

**Step 3: Verify**
- Click Play → dark canvas with "Players: 10  Human: X,Y"

**Step 4: Commit**
```bash
git init
git add index.html docs/
git commit -m "feat: canvas shell, config, state, player creation, start screen"
```

---

## Task 4: Input System

**Files:**
- Modify: `index.html` (add before the placeholder gameLoop)

**Step 1: Add keyboard + touch + D-pad input**

```js
// ============================================================
// INPUT
// ============================================================
const DIR_MAP = {
  ArrowUp:    { dx:  0, dy: -1 }, arrowup:    { dx:  0, dy: -1 },
  ArrowDown:  { dx:  0, dy:  1 }, arrowdown:  { dx:  0, dy:  1 },
  ArrowLeft:  { dx: -1, dy:  0 }, arrowleft:  { dx: -1, dy:  0 },
  ArrowRight: { dx:  1, dy:  0 }, arrowright: { dx:  1, dy:  0 },
  w: { dx: 0, dy: -1 }, s: { dx: 0, dy: 1 },
  a: { dx: -1, dy: 0 }, d: { dx: 1, dy: 0 },
};

function queueDirection(dir) {
  const h = window.humanPlayer;
  if (!h || !h.alive || !gameRunning) return;
  if (dir.dx === -h.dx && dir.dy === -h.dy) return; // no 180°
  const last = h.inputQueue[h.inputQueue.length - 1];
  if (last && last.dx === dir.dx && last.dy === dir.dy) return; // no duplicate
  h.inputQueue.push({ ...dir });
}

document.addEventListener('keydown', e => {
  const dir = DIR_MAP[e.key] || DIR_MAP[e.key.toLowerCase()];
  if (!dir) return;
  e.preventDefault();
  queueDirection(dir);
});

// Touch swipe
let touchStart = null;
canvas.addEventListener('touchstart', e => {
  touchStart = { x: e.touches[0].clientX, y: e.touches[0].clientY };
}, { passive: true });
canvas.addEventListener('touchend', e => {
  if (!touchStart) return;
  const dx = e.changedTouches[0].clientX - touchStart.x;
  const dy = e.changedTouches[0].clientY - touchStart.y;
  touchStart = null;
  if (Math.abs(dx) < 15 && Math.abs(dy) < 15) return;
  const dir = Math.abs(dx) > Math.abs(dy)
    ? (dx > 0 ? DIR_MAP.ArrowRight : DIR_MAP.ArrowLeft)
    : (dy > 0 ? DIR_MAP.ArrowDown  : DIR_MAP.ArrowUp);
  queueDirection(dir);
}, { passive: true });

// D-Pad
const DPAD_DIRS = { up: DIR_MAP.ArrowUp, down: DIR_MAP.ArrowDown, left: DIR_MAP.ArrowLeft, right: DIR_MAP.ArrowRight };
document.querySelectorAll('.dpad-btn').forEach(btn => {
  btn.addEventListener('click', () => queueDirection(DPAD_DIRS[btn.dataset.dir]));
});
```

**Step 2: Verify — no console errors after adding this section.**

---

## Task 5: Movement + Collision + Territory Flood-Fill

**Files:**
- Modify: `index.html` (add GAME LOGIC section before placeholder gameLoop)

**Step 1: Add movement and collision**

```js
// ============================================================
// GAME LOGIC — Movement & Collision
// ============================================================

function applyInput(p) {
  if (!p.isBot && p.inputQueue.length > 0) {
    const next = p.inputQueue.shift();
    p.dx = next.dx; p.dy = next.dy;
  }
}

function movePlayer(p) {
  const nx = p.x + p.dx;
  const ny = p.y + p.dy;

  if (!inBounds(nx, ny)) {
    // Bounce off wall
    p.dx = -p.dx; p.dy = -p.dy;
    return;
  }

  p.x = nx; p.y = ny;
  const i = idx(p.x, p.y);

  if (trail[i] === p.id) { killPlayer(p, null); return; }           // own trail → self-kill
  if (trail[i] !== 0)    { killPlayer(getPlayerById(trail[i]), p); } // enemy trail → kill them

  if (territory[i] === p.id && p.trail.size > 0) { claimTerritory(p); return; }

  if (territory[i] !== p.id) {
    trail[i] = p.id;
    p.trail.add(i);
  }
}

function killPlayer(victim, killer) {
  if (!victim || !victim.alive) return;
  victim.alive = false;

  victim.trail.forEach(i => { trail[i] = 0; });
  victim.trail.clear();

  if (killer) {
    killer.kills++;
    addKillFeed(killer.name, victim.name, killer.color, victim.color);
  }

  if (!victim.isBot) {
    const pct = ((getTerritoryCount(victim.id) / territory.length) * 100).toFixed(1);
    document.getElementById('death-killer').textContent    = killer ? killer.name : 'the void';
    document.getElementById('death-territory').textContent = pct + '%';
    document.getElementById('death-kills').textContent     = String(victim.kills);
    document.getElementById('death-screen').classList.remove('hidden');
    gameRunning = false;
  } else {
    victim.respawnTimer = 2000;
  }
}

function tickPlayers(dt) {
  for (const p of players) {
    if (!p.alive) {
      if (p.isBot) { p.respawnTimer -= dt; if (p.respawnTimer <= 0) respawnBot(p); }
      continue;
    }
    applyInput(p);
    movePlayer(p);
  }
}
```

**Step 2: Add the flood-fill territory claim**

```js
// ============================================================
// TERRITORY FLOOD FILL
// ============================================================
function claimTerritory(p) {
  // Mark trail cells as territory, clear trail markers
  p.trail.forEach(i => { territory[i] = p.id; trail[i] = 0; });
  p.trail.clear();

  // BFS from all 4 edges to find "outside" cells
  // Anything not reachable from the edge (blocked by player's territory) is inside → captured
  const outside = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
  const queue   = [];

  function enqueue(x, y) {
    const i = idx(x, y);
    if (outside[i] || territory[i] === p.id) return;
    outside[i] = 1;
    queue.push(i);
  }

  for (let x = 0; x < CFG.GRID_W; x++) { enqueue(x, 0); enqueue(x, CFG.GRID_H-1); }
  for (let y = 0; y < CFG.GRID_H; y++) { enqueue(0, y); enqueue(CFG.GRID_W-1, y); }

  let qi = 0;
  while (qi < queue.length) {
    const i  = queue[qi++];
    const cx = i % CFG.GRID_W;
    const cy = (i / CFG.GRID_W) | 0;
    if (cx > 0)            enqueue(cx-1, cy);
    if (cx < CFG.GRID_W-1) enqueue(cx+1, cy);
    if (cy > 0)            enqueue(cx, cy-1);
    if (cy < CFG.GRID_H-1) enqueue(cx, cy+1);
  }

  const newCells = new Set();
  for (let i = 0; i < territory.length; i++) {
    if (!outside[i] && territory[i] !== p.id) {
      territory[i] = p.id;
      newCells.add(i);
    }
  }

  if (newCells.size > 0) {
    fillAnims.push({ cells: newCells, color: p.color, startTime: performance.now() });
  }
}
```

**Step 3: Verify — no console errors. The game loop placeholder will show X,Y updating.**

---

## Task 6: AI Bot System

**Files:**
- Modify: `index.html` (add AI section after GAME LOGIC)

**Step 1: Add BFS direction finder + bot tick**

```js
// ============================================================
// AI
// ============================================================
const BFS_DIRS = [{dx:1,dy:0},{dx:-1,dy:0},{dx:0,dy:1},{dx:0,dy:-1}];

function bfsDirection(p, predicate, maxDist = 60) {
  const visited = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
  const queue   = [{ x: p.x, y: p.y, dx: 0, dy: 0, dist: 0 }];
  visited[idx(p.x, p.y)] = 1;

  while (queue.length > 0) {
    const cur = queue.shift();
    if (cur.dist > 0 && predicate(cur.x, cur.y)) return { dx: cur.dx, dy: cur.dy };
    if (cur.dist >= maxDist) continue;
    for (const d of BFS_DIRS) {
      const nx = cur.x + d.dx, ny = cur.y + d.dy;
      if (!inBounds(nx, ny)) continue;
      const ni = idx(nx, ny);
      if (visited[ni] || trail[ni] === p.id) continue;
      visited[ni] = 1;
      queue.push({ x: nx, y: ny,
        dx: cur.dist === 0 ? d.dx : cur.dx,
        dy: cur.dist === 0 ? d.dy : cur.dy,
        dist: cur.dist + 1 });
    }
  }
  return null;
}

function isOpposite(p, d) { return p.dx === -d.dx && p.dy === -d.dy; }

function safeSetDir(p, dir) {
  if (!dir || isOpposite(p, dir)) return;
  p.dx = dir.dx; p.dy = dir.dy;
}

function randomTurn(p) {
  const opts = BFS_DIRS.filter(d => !isOpposite(p, d));
  return opts[Math.floor(Math.random() * opts.length)];
}

function tickBot(p, dt) {
  p.botTimer -= dt;
  if (p.botTimer > 0) return;
  p.botTimer = CFG.BOT_THINK_MS + Math.random() * 80;

  const onHome   = territory[idx(p.x, p.y)] === p.id;
  const trailLen = p.trail.size;

  // State transitions
  if (trailLen > CFG.TRAIL_LIMIT) p.botState = 'retreat';
  if (p.botState === 'retreat' && onHome && trailLen === 0) p.botState = 'expand';

  // Opportunistic attack: if adjacent enemy trail with short own trail
  if (p.botState === 'expand' && trailLen < 20) {
    for (const d of BFS_DIRS) {
      const nx = p.x + d.dx, ny = p.y + d.dy;
      if (!inBounds(nx, ny)) continue;
      const ni = idx(nx, ny);
      if (trail[ni] !== 0 && trail[ni] !== p.id) {
        p.botState = 'attack';
        safeSetDir(p, d);
        return;
      }
    }
  }

  if (p.botState === 'retreat') {
    safeSetDir(p, bfsDirection(p, (x,y) => territory[idx(x,y)] === p.id, 100) || randomTurn(p));

  } else if (p.botState === 'attack') {
    const ahead = { x: p.x + p.dx, y: p.y + p.dy };
    if (!inBounds(ahead.x, ahead.y) || territory[idx(ahead.x, ahead.y)] === p.id)
      p.botState = 'expand';

  } else { // expand
    const dir = bfsDirection(p, (x,y) => {
      const t = territory[idx(x,y)];
      return t === 0 || t !== p.id;
    }, 60);
    safeSetDir(p, dir || randomTurn(p));
  }
}

function tickAllBots(dt) {
  for (const p of players) if (p.isBot && p.alive) tickBot(p, dt);
}
```

**Step 2: Verify no errors.**

---

## Task 7: Full Rendering System + Real Game Loop

**Files:**
- Modify: `index.html` (add RENDERING section, replace placeholder gameLoop)

**Step 1: Add rendering functions**

```js
// ============================================================
// RENDERING
// ============================================================
const minimapCanvas  = document.createElement('canvas');
minimapCanvas.width  = CFG.GRID_W;
minimapCanvas.height = CFG.GRID_H;
const minimapCtx     = minimapCanvas.getContext('2d');
const minimapImgData = minimapCtx.createImageData(CFG.GRID_W, CFG.GRID_H);

function updateCamera() {
  const h = window.humanPlayer;
  if (!h) return;
  const tx = h.x * CFG.CELL - canvas.width  / 2 + CFG.CELL / 2;
  const ty = h.y * CFG.CELL - canvas.height / 2 + CFG.CELL / 2;
  cam.x += (tx - cam.x) * 0.12;
  cam.y += (ty - cam.y) * 0.12;
}

function drawGrid(now) {
  const startX = Math.max(0, (cam.x / CFG.CELL) | 0);
  const startY = Math.max(0, (cam.y / CFG.CELL) | 0);
  const endX   = Math.min(CFG.GRID_W, startX + ((canvas.width  / CFG.CELL) | 0) + 2);
  const endY   = Math.min(CFG.GRID_H, startY + ((canvas.height / CFG.CELL) | 0) + 2);

  ctx.fillStyle = '#16213e';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // Background grid dots
  ctx.fillStyle = '#1a2540';
  for (let y = startY; y < endY; y++) {
    for (let x = startX; x < endX; x++) {
      ctx.fillRect(x * CFG.CELL - cam.x, y * CFG.CELL - cam.y, CFG.CELL - 1, CFG.CELL - 1);
    }
  }

  // Build fill animation overlay
  const animOverlay = new Map();
  for (const anim of fillAnims) {
    const brightness = 1 + (1 - Math.min((now - anim.startTime) / CFG.FILL_ANIM_MS, 1)) * 0.7;
    anim.cells.forEach(ci => animOverlay.set(ci, { color: anim.color, brightness }));
  }

  // Territory + trail cells
  for (let y = startY; y < endY; y++) {
    for (let x = startX; x < endX; x++) {
      const i  = idx(x, y);
      const t  = territory[i];
      const tr = trail[i];
      const sx = x * CFG.CELL - cam.x;
      const sy = y * CFG.CELL - cam.y;

      if (t !== 0) {
        const anim = animOverlay.get(i);
        if (anim) {
          const [r,g,b] = hexToRgb(anim.color);
          const f = anim.brightness;
          ctx.fillStyle = `rgb(${Math.min(255,r*f)|0},${Math.min(255,g*f)|0},${Math.min(255,b*f)|0})`;
        } else {
          const [r,g,b] = hexToRgb(playerColor(t));
          ctx.fillStyle = `rgb(${r*0.5|0},${g*0.5|0},${b*0.5|0})`;
        }
        ctx.fillRect(sx, sy, CFG.CELL - 1, CFG.CELL - 1);
      }

      if (tr !== 0) {
        ctx.fillStyle = playerColor(tr);
        ctx.fillRect(sx + 2, sy + 2, CFG.CELL - 5, CFG.CELL - 5);
      }
    }
  }

  // Territory border glow for human player
  const h = window.humanPlayer;
  if (h && h.alive) {
    ctx.strokeStyle = h.color + 'bb';
    ctx.lineWidth   = 1.5;
    for (let y = startY; y < endY; y++) {
      for (let x = startX; x < endX; x++) {
        if (territory[idx(x,y)] !== h.id) continue;
        const hasEdge =
          (inBounds(x+1,y) && territory[idx(x+1,y)] !== h.id) ||
          (inBounds(x-1,y) && territory[idx(x-1,y)] !== h.id) ||
          (inBounds(x,y+1) && territory[idx(x,y+1)] !== h.id) ||
          (inBounds(x,y-1) && territory[idx(x,y-1)] !== h.id);
        if (hasEdge) {
          ctx.strokeRect(x * CFG.CELL - cam.x + 0.5, y * CFG.CELL - cam.y + 0.5, CFG.CELL - 2, CFG.CELL - 2);
        }
      }
    }
  }
}

function drawPlayers() {
  for (const p of players) {
    if (!p.alive) continue;
    const sx = p.x * CFG.CELL - cam.x + CFG.CELL / 2;
    const sy = p.y * CFG.CELL - cam.y + CFG.CELL / 2;
    const r  = CFG.CELL * 0.55;

    ctx.save();
    ctx.shadowColor = p.color;
    ctx.shadowBlur  = 14;
    ctx.beginPath();
    ctx.arc(sx, sy, r, 0, Math.PI * 2);
    ctx.fillStyle = p.color;
    ctx.fill();
    ctx.restore();

    // Direction indicator dot
    ctx.beginPath();
    ctx.arc(sx + p.dx * r * 0.5, sy + p.dy * r * 0.5, r * 0.3, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255,255,255,0.9)';
    ctx.fill();

    // Name label (always for human, only nearby for bots)
    const dist = Math.hypot(p.x - humanPlayer.x, p.y - humanPlayer.y);
    if (!p.isBot || dist < 25) {
      ctx.fillStyle  = '#fff';
      ctx.font       = 'bold 11px sans-serif';
      ctx.textAlign  = 'center';
      ctx.fillText(p.name, sx, sy - r - 4);
    }
  }
  ctx.textAlign = 'left';
}

function drawMinimap() {
  const d = minimapImgData.data;
  for (let i = 0; i < territory.length; i++) {
    const t  = territory[i];
    const tr = trail[i];
    const pi = i * 4;
    if (tr !== 0) {
      const [r,g,b] = hexToRgb(playerColor(tr));
      d[pi]=r; d[pi+1]=g; d[pi+2]=b; d[pi+3]=255;
    } else if (t !== 0) {
      const [r,g,b] = hexToRgb(playerColor(t));
      d[pi]=r*0.55|0; d[pi+1]=g*0.55|0; d[pi+2]=b*0.55|0; d[pi+3]=255;
    } else {
      d[pi]=22; d[pi+1]=33; d[pi+2]=62; d[pi+3]=255;
    }
  }
  minimapCtx.putImageData(minimapImgData, 0, 0);

  const mx = canvas.width  - CFG.MINIMAP_SIZE - CFG.MINIMAP_PAD;
  const my = CFG.MINIMAP_PAD;
  ctx.fillStyle = 'rgba(0,0,0,0.55)';
  ctx.fillRect(mx - 2, my - 2, CFG.MINIMAP_SIZE + 4, CFG.MINIMAP_SIZE + 4);
  ctx.drawImage(minimapCanvas, mx, my, CFG.MINIMAP_SIZE, CFG.MINIMAP_SIZE);

  // Player dots
  for (const p of players) {
    if (!p.alive) continue;
    const px = mx + (p.x / CFG.GRID_W) * CFG.MINIMAP_SIZE;
    const py = my + (p.y / CFG.GRID_H) * CFG.MINIMAP_SIZE;
    ctx.beginPath();
    ctx.arc(px, py, p.isBot ? 2 : 3.5, 0, Math.PI * 2);
    ctx.fillStyle = p.isBot ? p.color : '#fff';
    ctx.fill();
  }
}

function drawLeaderboard() {
  const total  = territory.length;
  const ranked = [...players]
    .filter(p => p.alive)
    .map(p => ({ name: p.name, color: p.color, pct: (getTerritoryCount(p.id) / total * 100).toFixed(1) }))
    .sort((a,b) => b.pct - a.pct)
    .slice(0, 5);

  const lx = 16, ly = 80, lw = 190, lh = 28 + ranked.length * 26;
  ctx.fillStyle = 'rgba(0,0,0,0.55)';
  ctx.beginPath(); ctx.roundRect(lx, ly, lw, lh, 8); ctx.fill();
  ctx.fillStyle = '#999'; ctx.font = 'bold 10px sans-serif';
  ctx.fillText('LEADERBOARD', lx + 10, ly + 18);

  ranked.forEach((e, i) => {
    const ey = ly + 28 + i * 26;
    ctx.fillStyle = e.color;
    ctx.fillRect(lx + 10, ey, 10, 10);
    ctx.fillStyle = '#fff'; ctx.font = '13px sans-serif';
    ctx.fillText(e.name, lx + 26, ey + 10);
    ctx.fillStyle = '#aaa'; ctx.font = '12px sans-serif';
    ctx.fillText(e.pct + '%', lx + lw - 42, ey + 10);
  });
}

function drawScore() {
  const h    = window.humanPlayer;
  const pct  = (getTerritoryCount(h.id) / territory.length * 100).toFixed(1);
  const text = `${pct}%  |  Kills: ${h.kills}`;

  ctx.font = 'bold 16px sans-serif';
  const tw = ctx.measureText(text).width;
  const bx = (canvas.width - tw) / 2 - 16;
  const by = canvas.height - 48;

  ctx.fillStyle = 'rgba(0,0,0,0.55)';
  ctx.beginPath(); ctx.roundRect(bx, by, tw + 32, 32, 16); ctx.fill();
  ctx.fillStyle = '#fff';
  ctx.fillText(text, bx + 16, by + 21);
}
```

**Step 2: Add kill feed UI (DOM-safe — no innerHTML)**

```js
// ============================================================
// UI — Kill Feed (safe DOM construction)
// ============================================================
function addKillFeed(killerName, victimName, killerColor, victimColor) {
  const el = document.createElement('div');
  el.className = 'kill-entry';
  el.style.borderLeftColor = killerColor;

  const killerSpan = document.createElement('span');
  killerSpan.style.color = killerColor;
  killerSpan.textContent = killerName;  // safe: textContent, not innerHTML

  const sep = document.createTextNode(' eliminated ');

  const victimSpan = document.createElement('span');
  victimSpan.style.color = victimColor;
  victimSpan.textContent = victimName;  // safe

  el.appendChild(killerSpan);
  el.appendChild(sep);
  el.appendChild(victimSpan);

  document.getElementById('kill-feed').appendChild(el);
  killFeed.push({ el, time: performance.now() });
}

function tickKillFeed(now) {
  for (let i = killFeed.length - 1; i >= 0; i--) {
    const age = now - killFeed[i].time;
    if (age > CFG.KILL_FEED_MS) { killFeed[i].el.remove(); killFeed.splice(i, 1); }
    else if (age > CFG.KILL_FEED_MS - 500) killFeed[i].el.style.opacity = String((CFG.KILL_FEED_MS - age) / 500);
  }
}

function tickFillAnims(now) {
  for (let i = fillAnims.length - 1; i >= 0; i--)
    if (now - fillAnims[i].startTime > CFG.FILL_ANIM_MS) fillAnims.splice(i, 1);
}
```

**Step 3: Replace placeholder gameLoop with final version**

Find and replace the placeholder `function gameLoop` with:

```js
// ============================================================
// GAME LOOP
// ============================================================
function gameLoop(ts) {
  if (!gameRunning) return;

  const dt   = Math.min(ts - lastTime, 100); // cap at 100ms to avoid spiral of death
  lastTime   = ts;
  tickAccum += dt;

  while (tickAccum >= TICK_MS) {
    tickAllBots(TICK_MS);
    tickPlayers(TICK_MS);
    tickAccum -= TICK_MS;
  }

  updateCamera();
  drawGrid(ts);
  drawPlayers();
  drawMinimap();
  drawLeaderboard();
  drawScore();
  tickFillAnims(ts);
  tickKillFeed(ts);

  requestAnimationFrame(gameLoop);
}
```

**Step 4: Full playthrough verification**
- Click Play → game starts, grid visible, player moves with arrow keys
- Leave territory → colored trail appears
- Return to own territory → territory fills with animation
- Cross a bot's trail → kill feed entry appears
- Get killed → death screen shows stats
- Respawn → game continues
- Minimap, leaderboard, score all visible

**Step 5: Commit**
```bash
git add index.html
git commit -m "feat: complete rendering, kill feed, game loop, AI bots"
```

---

## Task 8: Final Verification + Commit

**Step 1: Check for known edge cases**
- Spawn inside another player's territory (randomEmptyCell handles this via retry loop)
- Trail longer than TRAIL_LIMIT (bots retreat — verify in play)
- Bot respawn after death (respawnTimer → respawnBot)
- Resize window mid-game (resizeCanvas handles this)

**Step 2: Open DevTools Performance tab**
- Record 10 seconds of gameplay
- Frame time should stay under 16ms (60fps)
- Watch for GC spikes — there should be minimal object allocation in the hot path

**Step 3: Test on mobile (or DevTools device simulation)**
- Enable touch simulation in DevTools
- Swipe to change direction works
- D-pad appears and works

**Step 4: Final commit**
```bash
git add index.html docs/
git commit -m "chore: final paper.io clone — fully playable"
```

---

## Task Summary

| Task | What it builds | Commit? |
|------|----------------|---------|
| 1 | HTML + CSS skeleton, screens, D-pad | No |
| 2 | CONFIG, STATE, canvas init, polyfills | No |
| 3 | Player creation, start screen, placeholder loop | Yes |
| 4 | Keyboard + touch + D-pad input | No |
| 5 | Movement, collision, flood-fill territory | No |
| 6 | AI bots (BFS expand/attack/retreat) | No |
| 7 | Full rendering + real game loop + kill feed | Yes |
| 8 | Polish, edge cases, performance check | Yes |
