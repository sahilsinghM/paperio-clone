# Paper.io 2 — 3D Rendering Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the Canvas 2D rendering layer in the existing Paper.io clone with Three.js WebGL, giving it the same look as Paper.io 2: perspective 3D camera, 3D player meshes, flat instanced territory cells, lighting and fog.

**Architecture:** Extract the existing monolithic `index.html` script into ES modules in `src/`, then replace `drawGrid/drawPlayers/drawMinimap` with a `renderer3d.js` module using `InstancedMesh` for cells (1 draw call per type), `RoundedBoxGeometry` player meshes, and a `PerspectiveCamera` that follows the human player. All game logic (flood-fill, AI, input, collision) is untouched.

**Tech Stack:** Three.js r165 via CDN importmap, ES modules (no bundler), Vanilla JS.

**Coordinate system:** Grid cell `(x, y)` → Three.js world `(x - GRID_W/2 + 0.5, 0, y - GRID_H/2 + 0.5)`. Origin is grid center. Y-axis is up. Ground at Y=0.

---

## Task 1: Extract Logic Into ES Modules

**Files:**
- Create: `src/config.js`
- Create: `src/state.js`
- Create: `src/logic.js`
- Create: `src/ai.js`
- Create: `src/input.js`
- Create: `src/ui.js`
- Create: `src/main.js`
- Modify: `index.html`

This task moves all JS out of `index.html` into modules. The game still runs identically (still uses 2D canvas via a temporary shim in main.js). No logic changes — only file structure changes.

---

### Step 1: Create `src/config.js`

```js
// src/config.js
export const CFG = {
  GRID_W:       250,
  GRID_H:       250,
  CELL:         16,
  SPEED:        8,
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
```

---

### Step 2: Create `src/state.js`

```js
// src/state.js
import { CFG } from './config.js';

export const territory = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
export const trail     = new Uint8Array(CFG.GRID_W * CFG.GRID_H);

// Mutable game state — mutate properties directly
export const state = {
  players:     [],
  nextId:      1,
  gameRunning: false,
  lastTime:    0,
  tickAccum:   0,
  humanPlayer: null,
  killFeed:    [],   // { el, time }
  fillAnims:   [],   // { cells: Set, color, startTime }
};

export const TICK_MS = 1000 / CFG.SPEED;

// ---- Helpers ----
export function idx(x, y)      { return y * CFG.GRID_W + x; }
export function inBounds(x, y) { return x >= 0 && x < CFG.GRID_W && y >= 0 && y < CFG.GRID_H; }

export function randomEmptyCell() {
  let x, y, attempts = 0;
  do {
    x = 10 + Math.floor(Math.random() * (CFG.GRID_W - 20));
    y = 10 + Math.floor(Math.random() * (CFG.GRID_H - 20));
    attempts++;
  } while (territory[idx(x, y)] !== 0 && attempts < 500);
  return { x, y };
}

export function getPlayerById(id)   { return state.players.find(p => p.id === id); }
export function playerColor(id)     { const p = getPlayerById(id); return p ? p.color : '#333'; }

export function getTerritoryCount(id) {
  let n = 0;
  for (let i = 0; i < territory.length; i++) if (territory[i] === id) n++;
  return n;
}

const colorCache = {};
export function hexToRgb(hex) {
  if (colorCache[hex]) return colorCache[hex];
  const r = parseInt(hex.slice(1,3), 16);
  const g = parseInt(hex.slice(3,5), 16);
  const b = parseInt(hex.slice(5,7), 16);
  return (colorCache[hex] = [r, g, b]);
}

export function sanitizeName(raw) {
  return raw.replace(/[^a-zA-Z0-9 '_\-\.]/g, '').trim().slice(0, 16) || 'Player';
}

// roundRect polyfill
export function applyRoundRectPolyfill() {
  if (!CanvasRenderingContext2D.prototype.roundRect) {
    CanvasRenderingContext2D.prototype.roundRect = function(x, y, w, h, r) {
      this.beginPath();
      this.moveTo(x+r,y); this.lineTo(x+w-r,y);
      this.quadraticCurveTo(x+w,y,x+w,y+r);
      this.lineTo(x+w,y+h-r); this.quadraticCurveTo(x+w,y+h,x+w-r,y+h);
      this.lineTo(x+r,y+h); this.quadraticCurveTo(x,y+h,x,y+h-r);
      this.lineTo(x,y+r); this.quadraticCurveTo(x,y,x+r,y);
      this.closePath();
    };
  }
}
```

---

### Step 3: Create `src/logic.js`

```js
// src/logic.js
import { CFG } from './config.js';
import { territory, trail, state, idx, inBounds, randomEmptyCell, getTerritoryCount } from './state.js';
import { addKillFeed } from './ui.js';

export function createPlayer(name, color, isBot = false) {
  const id      = state.nextId++;
  const { x, y } = randomEmptyCell();
  const p = {
    id, name, color, isBot,
    x, y, dx: 1, dy: 0,
    trail: new Set(), alive: true, kills: 0,
    botState: 'expand', botTimer: 0, respawnTimer: 0, inputQueue: [],
  };
  stampHome(p);
  state.players.push(p);
  return p;
}

export function stampHome(p) {
  for (let dy = -1; dy <= 1; dy++)
    for (let dx = -1; dx <= 1; dx++)
      if (inBounds(p.x+dx, p.y+dy))
        territory[idx(p.x+dx, p.y+dy)] = p.id;
}

export function respawnBot(p) {
  const { x, y } = randomEmptyCell();
  p.x = x; p.y = y; p.dx = 1; p.dy = 0;
  p.trail.clear(); p.alive = true;
  stampHome(p);
}

export function respawnHuman() {
  const h = state.humanPlayer;
  const { x, y } = randomEmptyCell();
  h.x = x; h.y = y; h.dx = 1; h.dy = 0;
  h.trail.clear(); h.alive = true; h.kills = 0;
  stampHome(h);
}

export function applyInput(p) {
  if (!p.isBot && p.inputQueue.length > 0) {
    const next = p.inputQueue.shift();
    p.dx = next.dx; p.dy = next.dy;
  }
}

export function movePlayer(p) {
  const nx = p.x + p.dx;
  const ny = p.y + p.dy;
  if (!inBounds(nx, ny)) { p.dx = -p.dx; p.dy = -p.dy; return; }
  p.x = nx; p.y = ny;
  const i = idx(p.x, p.y);
  if (trail[i] === p.id)  { killPlayer(p, null); return; }
  if (trail[i] !== 0)     { killPlayer(import_getPlayerById(trail[i]), p); return; }
  if (territory[i] === p.id && p.trail.size > 0) { claimTerritory(p); return; }
  if (territory[i] !== p.id) { trail[i] = p.id; p.trail.add(i); }
}

// NOTE: killPlayer needs getPlayerById from state — import here to avoid circular
import { getPlayerById } from './state.js';
// Fix the reference above — rename the placeholder:
function import_getPlayerById(id) { return getPlayerById(id); }

export function killPlayer(victim, killer) {
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
    state.gameRunning = false;
  } else {
    victim.respawnTimer = 2000;
  }
}

export function claimTerritory(p) {
  p.trail.forEach(i => { territory[i] = p.id; trail[i] = 0; });
  p.trail.clear();
  const outside = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
  const queue   = [];
  function enqueue(x, y) {
    const i = idx(x, y);
    if (outside[i] || territory[i] === p.id) return;
    outside[i] = 1; queue.push(i);
  }
  for (let x = 0; x < CFG.GRID_W; x++) { enqueue(x,0); enqueue(x,CFG.GRID_H-1); }
  for (let y = 0; y < CFG.GRID_H; y++) { enqueue(0,y); enqueue(CFG.GRID_W-1,y); }
  let qi = 0;
  while (qi < queue.length) {
    const i = queue[qi++];
    const cx = i % CFG.GRID_W, cy = (i/CFG.GRID_W)|0;
    if (cx > 0)            enqueue(cx-1, cy);
    if (cx < CFG.GRID_W-1) enqueue(cx+1, cy);
    if (cy > 0)            enqueue(cx, cy-1);
    if (cy < CFG.GRID_H-1) enqueue(cx, cy+1);
  }
  const newCells = new Set();
  for (let i = 0; i < territory.length; i++) {
    if (!outside[i] && territory[i] !== p.id) { territory[i] = p.id; newCells.add(i); }
  }
  if (newCells.size > 0)
    state.fillAnims.push({ cells: newCells, color: p.color, startTime: performance.now() });
}

export function tickPlayers(dt) {
  for (const p of state.players) {
    if (!p.alive) {
      if (p.isBot) { p.respawnTimer -= dt; if (p.respawnTimer <= 0) respawnBot(p); }
      continue;
    }
    applyInput(p);
    movePlayer(p);
  }
}
```

**Important note about the circular import in logic.js:** The `import { getPlayerById } from './state.js'` at the top of the file creates a potential issue. In practice ES module circular imports resolve correctly in modern browsers, but to be safe, restructure `movePlayer` to pass `getPlayerById` inline:

Replace the `import_getPlayerById` workaround with a direct call at the top of the file:
```js
import { getPlayerById, ... } from './state.js';
// then in movePlayer:
if (trail[i] !== 0) { killPlayer(getPlayerById(trail[i]), p); return; }
```
Remove the `import_getPlayerById` function entirely.

---

### Step 4: Create `src/ai.js`

```js
// src/ai.js
import { CFG } from './config.js';
import { territory, trail, state, idx, inBounds } from './state.js';

const BFS_DIRS = [{dx:1,dy:0},{dx:-1,dy:0},{dx:0,dy:1},{dx:0,dy:-1}];

export function bfsDirection(p, predicate, maxDist = 60) {
  const visited = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
  const queue   = [{ x: p.x, y: p.y, dx: 0, dy: 0, dist: 0 }];
  visited[idx(p.x, p.y)] = 1;
  let qi = 0;
  while (qi < queue.length) {
    const cur = queue[qi++];
    if (cur.dist > 0 && predicate(cur.x, cur.y)) return { dx: cur.dx, dy: cur.dy };
    if (cur.dist >= maxDist) continue;
    for (const d of BFS_DIRS) {
      const nx = cur.x + d.dx, ny = cur.y + d.dy;
      if (!inBounds(nx, ny)) continue;
      const ni = idx(nx, ny);
      if (visited[ni] || trail[ni] === p.id) continue;
      visited[ni] = 1;
      queue.push({ x:nx, y:ny,
        dx: cur.dist===0 ? d.dx : cur.dx,
        dy: cur.dist===0 ? d.dy : cur.dy,
        dist: cur.dist+1 });
    }
  }
  return null;
}

function isOpposite(p, d) { return p.dx===-d.dx && p.dy===-d.dy; }
function safeSetDir(p, dir) { if (!dir || isOpposite(p,dir)) return; p.dx=dir.dx; p.dy=dir.dy; }
function randomTurn(p) {
  const opts = BFS_DIRS.filter(d => !isOpposite(p,d));
  return opts[Math.floor(Math.random()*opts.length)];
}

function tickBot(p, dt) {
  p.botTimer -= dt;
  if (p.botTimer > 0) return;
  p.botTimer = CFG.BOT_THINK_MS + Math.random()*80;
  const onHome   = territory[idx(p.x,p.y)] === p.id;
  const trailLen = p.trail.size;
  if (trailLen > CFG.TRAIL_LIMIT) p.botState = 'retreat';
  if (p.botState==='retreat' && onHome && trailLen===0) p.botState = 'expand';
  if (p.botState==='expand' && trailLen < 20) {
    for (const d of BFS_DIRS) {
      const nx=p.x+d.dx, ny=p.y+d.dy;
      if (!inBounds(nx,ny)) continue;
      const ni=idx(nx,ny);
      if (trail[ni]!==0 && trail[ni]!==p.id) { p.botState='attack'; safeSetDir(p,d); return; }
    }
  }
  if (p.botState==='retreat') {
    safeSetDir(p, bfsDirection(p,(x,y)=>territory[idx(x,y)]===p.id,100) || randomTurn(p));
  } else if (p.botState==='attack') {
    const ahead={x:p.x+p.dx,y:p.y+p.dy};
    if (!inBounds(ahead.x,ahead.y)||territory[idx(ahead.x,ahead.y)]===p.id) p.botState='expand';
  } else {
    const dir = bfsDirection(p,(x,y)=>{ const t=territory[idx(x,y)]; return t===0||t!==p.id; },60);
    safeSetDir(p, dir||randomTurn(p));
  }
}

export function tickAllBots(dt) {
  for (const p of state.players) if (p.isBot && p.alive) tickBot(p,dt);
}
```

---

### Step 5: Create `src/input.js`

```js
// src/input.js
import { state } from './state.js';

const DIR_MAP = {
  ArrowUp:    {dx:0,dy:-1}, arrowup:    {dx:0,dy:-1},
  ArrowDown:  {dx:0,dy:1},  arrowdown:  {dx:0,dy:1},
  ArrowLeft:  {dx:-1,dy:0}, arrowleft:  {dx:-1,dy:0},
  ArrowRight: {dx:1,dy:0},  arrowright: {dx:1,dy:0},
  w:{dx:0,dy:-1}, s:{dx:0,dy:1}, a:{dx:-1,dy:0}, d:{dx:1,dy:0},
};

function queueDirection(dir) {
  const h = state.humanPlayer;
  if (!h || !h.alive || !state.gameRunning) return;
  if (dir.dx===-h.dx && dir.dy===-h.dy) return;
  const last = h.inputQueue[h.inputQueue.length-1];
  if (last && last.dx===dir.dx && last.dy===dir.dy) return;
  h.inputQueue.push({...dir});
}

export function initInput(canvas) {
  document.addEventListener('keydown', e => {
    const dir = DIR_MAP[e.key] || DIR_MAP[e.key.toLowerCase()];
    if (!dir) return;
    e.preventDefault();
    queueDirection(dir);
  });

  let touchStart = null;
  canvas.addEventListener('touchstart', e => {
    touchStart = {x:e.touches[0].clientX, y:e.touches[0].clientY};
  }, {passive:true});
  canvas.addEventListener('touchend', e => {
    if (!touchStart) return;
    const dx = e.changedTouches[0].clientX - touchStart.x;
    const dy = e.changedTouches[0].clientY - touchStart.y;
    touchStart = null;
    if (Math.abs(dx)<15 && Math.abs(dy)<15) return;
    const dir = Math.abs(dx)>Math.abs(dy)
      ? (dx>0 ? DIR_MAP.ArrowRight : DIR_MAP.ArrowLeft)
      : (dy>0 ? DIR_MAP.ArrowDown  : DIR_MAP.ArrowUp);
    queueDirection(dir);
  }, {passive:true});

  const DPAD_DIRS = {up:DIR_MAP.ArrowUp,down:DIR_MAP.ArrowDown,left:DIR_MAP.ArrowLeft,right:DIR_MAP.ArrowRight};
  document.querySelectorAll('.dpad-btn').forEach(btn => {
    btn.addEventListener('click', () => queueDirection(DPAD_DIRS[btn.dataset.dir]));
  });
}
```

---

### Step 6: Create `src/ui.js`

```js
// src/ui.js
import { CFG } from './config.js';
import { state, territory, getTerritoryCount } from './state.js';

export function addKillFeed(killerName, victimName, killerColor, victimColor) {
  const el = document.createElement('div');
  el.className = 'kill-entry';
  el.style.borderLeftColor = killerColor;
  const ks = document.createElement('span');
  ks.style.color = killerColor; ks.textContent = killerName;
  const vs = document.createElement('span');
  vs.style.color = victimColor; vs.textContent = victimName;
  el.appendChild(ks);
  el.appendChild(document.createTextNode(' eliminated '));
  el.appendChild(vs);
  document.getElementById('kill-feed').appendChild(el);
  state.killFeed.push({el, time: performance.now()});
}

export function tickKillFeed(now) {
  for (let i = state.killFeed.length-1; i >= 0; i--) {
    const age = now - state.killFeed[i].time;
    if (age > CFG.KILL_FEED_MS) { state.killFeed[i].el.remove(); state.killFeed.splice(i,1); }
    else if (age > CFG.KILL_FEED_MS-500)
      state.killFeed[i].el.style.opacity = String((CFG.KILL_FEED_MS-age)/500);
  }
}

export function tickFillAnims(now) {
  for (let i = state.fillAnims.length-1; i >= 0; i--)
    if (now - state.fillAnims[i].startTime > CFG.FILL_ANIM_MS) state.fillAnims.splice(i,1);
}

export function initColorPicker(onColorSelect) {
  let selected = CFG.COLORS[0];
  const el = document.getElementById('color-picker');
  CFG.COLORS.forEach((c, i) => {
    const swatch = document.createElement('div');
    swatch.className = 'color-swatch' + (i===0 ? ' selected' : '');
    swatch.style.background = c;
    swatch.addEventListener('click', () => {
      document.querySelectorAll('.color-swatch').forEach(s => s.classList.remove('selected'));
      swatch.classList.add('selected');
      selected = c;
      onColorSelect(c);
    });
    el.appendChild(swatch);
  });
  return () => selected; // getter
}

export function showDeathScreen(killerName, pct, kills) {
  document.getElementById('death-killer').textContent    = killerName;
  document.getElementById('death-territory').textContent = pct + '%';
  document.getElementById('death-kills').textContent     = String(kills);
  document.getElementById('death-screen').classList.remove('hidden');
}

// HUD: leaderboard + score are drawn on a 2D overlay canvas (not Three.js)
// We create a transparent 2D canvas on top of the WebGL canvas for HUD
let hudCanvas, hudCtx;

export function initHUD() {
  hudCanvas = document.createElement('canvas');
  hudCanvas.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:10;';
  document.body.appendChild(hudCanvas);
  hudCtx = hudCanvas.getContext('2d');
  window.addEventListener('resize', resizeHUD);
  resizeHUD();
}

function resizeHUD() {
  hudCanvas.width  = window.innerWidth;
  hudCanvas.height = window.innerHeight;
}

export function drawHUD(now) {
  if (!hudCtx || !state.humanPlayer) return;
  hudCtx.clearRect(0, 0, hudCanvas.width, hudCanvas.height);
  drawLeaderboard();
  drawScore();
}

function drawLeaderboard() {
  const total  = territory.length;
  const ranked = [...state.players]
    .filter(p => p.alive)
    .map(p => ({ name:p.name, color:p.color, pct:(getTerritoryCount(p.id)/total*100).toFixed(1) }))
    .sort((a,b) => b.pct - a.pct).slice(0,5);
  const lx=16,ly=80,lw=190,lh=28+ranked.length*26;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.beginPath(); hudCtx.roundRect(lx,ly,lw,lh,8); hudCtx.fill();
  hudCtx.fillStyle='#999'; hudCtx.font='bold 10px sans-serif';
  hudCtx.fillText('LEADERBOARD',lx+10,ly+18);
  ranked.forEach((e,i)=>{
    const ey=ly+28+i*26;
    hudCtx.fillStyle=e.color; hudCtx.fillRect(lx+10,ey,10,10);
    hudCtx.fillStyle='#fff'; hudCtx.font='13px sans-serif';
    hudCtx.fillText(e.name,lx+26,ey+10);
    hudCtx.fillStyle='#aaa'; hudCtx.font='12px sans-serif';
    hudCtx.fillText(e.pct+'%',lx+lw-42,ey+10);
  });
}

function drawScore() {
  const h   = state.humanPlayer;
  const pct = (getTerritoryCount(h.id)/territory.length*100).toFixed(1);
  const text= `${pct}%  |  Kills: ${h.kills}`;
  hudCtx.font='bold 16px sans-serif';
  const tw=hudCtx.measureText(text).width;
  const bx=(hudCanvas.width-tw)/2-16, by=hudCanvas.height-48;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.beginPath(); hudCtx.roundRect(bx,by,tw+32,32,16); hudCtx.fill();
  hudCtx.fillStyle='#fff';
  hudCtx.fillText(text,bx+16,by+21);
}
```

---

### Step 7: Create `src/main.js` (temporary — uses old 2D canvas shim)

This wires everything together and keeps the old 2D drawing temporarily, so you can verify the module refactor works before replacing the renderer.

```js
// src/main.js
import { CFG } from './config.js';
import { territory, trail, state, TICK_MS, sanitizeName, applyRoundRectPolyfill, hexToRgb, playerColor, getTerritoryCount, idx, inBounds } from './state.js';
import { createPlayer, tickPlayers, respawnHuman } from './logic.js';
import { tickAllBots } from './ai.js';
import { initInput } from './input.js';
import { initColorPicker, initHUD, drawHUD, tickKillFeed, tickFillAnims } from './ui.js';

applyRoundRectPolyfill();

// ---- Temporary 2D canvas shim (replaced in Task 2) ----
const canvas = document.getElementById('game');
const ctx    = canvas.getContext('2d');
function resizeCanvas() { canvas.width=window.innerWidth; canvas.height=window.innerHeight; }
resizeCanvas();
window.addEventListener('resize', resizeCanvas);
initInput(canvas);
initHUD();

// ---- Minimap (2D, kept permanently) ----
const minimapCanvas = document.createElement('canvas');
minimapCanvas.width = CFG.GRID_W; minimapCanvas.height = CFG.GRID_H;
const mmCtx         = minimapCanvas.getContext('2d');
const mmImgData     = mmCtx.createImageData(CFG.GRID_W, CFG.GRID_H);

function drawMinimap() {
  const d = mmImgData.data;
  for (let i = 0; i < territory.length; i++) {
    const t=territory[i], tr_=trail[i], pi=i*4;
    if (tr_!==0) {
      const [r,g,b]=hexToRgb(playerColor(tr_));
      d[pi]=r;d[pi+1]=g;d[pi+2]=b;d[pi+3]=255;
    } else if (t!==0) {
      const [r,g,b]=hexToRgb(playerColor(t));
      d[pi]=r*0.55|0;d[pi+1]=g*0.55|0;d[pi+2]=b*0.55|0;d[pi+3]=255;
    } else {
      d[pi]=22;d[pi+1]=33;d[pi+2]=62;d[pi+3]=255;
    }
  }
  mmCtx.putImageData(mmImgData,0,0);
  // Draw onto HUD canvas (handled in ui.js drawHUD if we pass it, or draw here on main canvas)
  const mx=canvas.width-CFG.MINIMAP_SIZE-CFG.MINIMAP_PAD, my=CFG.MINIMAP_PAD;
  ctx.fillStyle='rgba(0,0,0,0.55)';
  ctx.fillRect(mx-2,my-2,CFG.MINIMAP_SIZE+4,CFG.MINIMAP_SIZE+4);
  ctx.drawImage(minimapCanvas,mx,my,CFG.MINIMAP_SIZE,CFG.MINIMAP_SIZE);
  for (const p of state.players) {
    if (!p.alive) continue;
    const px=mx+(p.x/CFG.GRID_W)*CFG.MINIMAP_SIZE;
    const py=my+(p.y/CFG.GRID_H)*CFG.MINIMAP_SIZE;
    ctx.beginPath(); ctx.arc(px,py,p.isBot?2:3.5,0,Math.PI*2);
    ctx.fillStyle=p.isBot?p.color:'#fff'; ctx.fill();
  }
}

// ---- Camera (2D) ----
const cam2d = {x:0, y:0};
function updateCamera2d() {
  const h=state.humanPlayer; if(!h) return;
  const tx=h.x*CFG.CELL-canvas.width/2+CFG.CELL/2;
  const ty=h.y*CFG.CELL-canvas.height/2+CFG.CELL/2;
  cam2d.x+=(tx-cam2d.x)*0.12; cam2d.y+=(ty-cam2d.y)*0.12;
}

// ---- 2D Draw (temp shim) ----
function draw2d(ts) {
  const startX=Math.max(0,(cam2d.x/CFG.CELL)|0);
  const startY=Math.max(0,(cam2d.y/CFG.CELL)|0);
  const endX=Math.min(CFG.GRID_W,startX+((canvas.width/CFG.CELL)|0)+2);
  const endY=Math.min(CFG.GRID_H,startY+((canvas.height/CFG.CELL)|0)+2);
  ctx.fillStyle='#16213e'; ctx.fillRect(0,0,canvas.width,canvas.height);
  ctx.fillStyle='#1a2540';
  for (let y=startY;y<endY;y++) for (let x=startX;x<endX;x++)
    ctx.fillRect(x*CFG.CELL-cam2d.x,y*CFG.CELL-cam2d.y,CFG.CELL-1,CFG.CELL-1);
  for (let y=startY;y<endY;y++) {
    for (let x=startX;x<endX;x++) {
      const i=idx(x,y), t=territory[i], tr_=trail[i];
      const sx=x*CFG.CELL-cam2d.x, sy=y*CFG.CELL-cam2d.y;
      if (t!==0) {
        const [r,g,b]=hexToRgb(playerColor(t));
        ctx.fillStyle=`rgb(${r*0.5|0},${g*0.5|0},${b*0.5|0})`;
        ctx.fillRect(sx,sy,CFG.CELL-1,CFG.CELL-1);
      }
      if (tr_!==0) {
        ctx.fillStyle=playerColor(tr_);
        ctx.fillRect(sx+2,sy+2,CFG.CELL-5,CFG.CELL-5);
      }
    }
  }
  for (const p of state.players) {
    if (!p.alive) continue;
    const sx=p.x*CFG.CELL-cam2d.x+CFG.CELL/2, sy=p.y*CFG.CELL-cam2d.y+CFG.CELL/2;
    const r=CFG.CELL*0.55;
    ctx.beginPath(); ctx.arc(sx,sy,r,0,Math.PI*2);
    ctx.fillStyle=p.color; ctx.fill();
  }
  drawMinimap();
}

// ---- Start/Respawn ----
let selectedColor = CFG.COLORS[0];
const getSelectedColor = initColorPicker(c => { selectedColor = c; });
const BOT_NAMES = ['Zara','Nova','Pixel','Echo','Blaze','Frost','Vex','Onyx','Cleo'];

function startGame() {
  territory.fill(0); trail.fill(0);
  state.players=[]; state.nextId=1;
  state.killFeed.length=0; state.fillAnims.length=0;
  state.humanPlayer = createPlayer(sanitizeName(document.getElementById('player-name').value), selectedColor, false);
  for (let i=0;i<CFG.BOT_COUNT;i++) {
    const usedColors=new Set(state.players.map(p=>p.color));
    const botColor=CFG.COLORS.find(c=>!usedColors.has(c))||CFG.COLORS[i%CFG.COLORS.length];
    createPlayer(BOT_NAMES[i]||`Bot-${i+1}`, botColor, true);
  }
  document.getElementById('start-screen').classList.add('hidden');
  state.gameRunning=true; state.lastTime=performance.now();
  requestAnimationFrame(gameLoop);
}

document.getElementById('play-btn').addEventListener('click', startGame);
document.getElementById('respawn-btn').addEventListener('click', () => {
  document.getElementById('death-screen').classList.add('hidden');
  respawnHuman();
  state.gameRunning=true; state.lastTime=performance.now();
  requestAnimationFrame(gameLoop);
});

// ---- Game Loop ----
function gameLoop(ts) {
  if (!state.gameRunning) return;
  const dt=Math.min(ts-state.lastTime,100);
  state.lastTime=ts; state.tickAccum+=dt;
  while (state.tickAccum>=TICK_MS) {
    tickAllBots(TICK_MS); tickPlayers(TICK_MS);
    state.tickAccum-=TICK_MS;
  }
  updateCamera2d();
  draw2d(ts);
  drawHUD(ts);
  tickFillAnims(ts); tickKillFeed(ts);
  requestAnimationFrame(gameLoop);
}
```

---

### Step 8: Update `index.html`

Replace the entire `<script>` block (from `<script>` to `</script>`) with:

```html
<script type="importmap">
{
  "imports": {
    "three": "https://cdn.jsdelivr.net/npm/three@0.165.0/build/three.module.js",
    "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.165.0/examples/jsm/"
  }
}
</script>
<script type="module" src="src/main.js"></script>
```

Also add `<canvas id="game"></canvas>` remains in the HTML (it's used as the touch target; Three.js renderer creates its own canvas).

---

### Step 9: Verify game still works

Run a local server: `python3 -m http.server 8765`

Open `http://localhost:8765` — game should work identically to before (still 2D canvas). If it works, the module refactor is complete.

**Commit:**
```bash
cd /home/xinhangyuan/Documents/mycode/claude-written/paperio
git add src/ index.html
git commit -m "refactor: extract game logic into ES modules (2D canvas still active)"
```

---

## Task 2: Three.js Scene Setup

**Files:**
- Create: `src/renderer3d.js`
- Modify: `src/main.js`

Replace the 2D canvas renderer with Three.js. After this task, the game renders an empty 3D scene (dark ground, no territory cells yet — just lights and camera).

### Step 1: Create `src/renderer3d.js` (scene + lights + ground + camera skeleton)

```js
// src/renderer3d.js
import * as THREE from 'three';
import { CFG } from './config.js';
import { state } from './state.js';

let renderer, scene, camera, groundMesh;

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

export function initRenderer(container) {
  // Renderer
  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type    = THREE.PCFSoftShadowMap;
  renderer.setClearColor(0x0d1b2a);
  container.appendChild(renderer.domElement);

  // Scene
  scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x0d1b2a, 0.004);

  // Lights
  const ambient = new THREE.AmbientLight(0xffffff, 0.55);
  scene.add(ambient);

  const sun = new THREE.DirectionalLight(0xffffff, 1.2);
  sun.position.set(50, 80, 30);
  sun.castShadow = true;
  sun.shadow.mapSize.width  = 2048;
  sun.shadow.mapSize.height = 2048;
  sun.shadow.camera.near    = 1;
  sun.shadow.camera.far     = 400;
  sun.shadow.camera.left    = -150;
  sun.shadow.camera.right   = 150;
  sun.shadow.camera.top     = 150;
  sun.shadow.camera.bottom  = -150;
  scene.add(sun);

  // Ground plane
  const groundGeo = new THREE.PlaneGeometry(CFG.GRID_W + 20, CFG.GRID_H + 20);
  const groundMat = new THREE.MeshLambertMaterial({ color: 0x16213e });
  groundMesh = new THREE.Mesh(groundGeo, groundMat);
  groundMesh.rotation.x = -Math.PI / 2;
  groundMesh.receiveShadow = true;
  scene.add(groundMesh);

  // Camera
  camera = new THREE.PerspectiveCamera(55, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.position.set(0, 18, 14);
  camera.lookAt(0, 0, 0);

  window.addEventListener('resize', onResize);
  return { renderer, scene, camera };
}

export function onResize() {
  if (!renderer) return;
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

// World position of grid cell (x, y)
export function cellToWorld(x, y) {
  return new THREE.Vector3(x - HALF_W + 0.5, 0, y - HALF_H + 0.5);
}

// Smooth camera follow (call each frame)
export function updateCamera3d() {
  const h = state.humanPlayer;
  if (!h) return;
  const wx = h.x - HALF_W + 0.5;
  const wz = h.y - HALF_H + 0.5;
  const targetPos = new THREE.Vector3(wx, 18, wz + 14);
  const lookAt    = new THREE.Vector3(wx, 0, wz);
  camera.position.lerp(targetPos, 0.08);
  // Smooth lookAt via a target point lerp
  const currentTarget = new THREE.Vector3();
  camera.getWorldDirection(currentTarget);
  currentTarget.multiplyScalar(100).add(camera.position);
  currentTarget.lerp(lookAt, 0.08);
  camera.lookAt(currentTarget);
}

export function render3d() {
  renderer.render(scene, camera);
}

export function getScene()    { return scene; }
export function getCamera()   { return camera; }
export function getRenderer() { return renderer; }
```

### Step 2: Wire Three.js into `src/main.js`

In `main.js`, replace the 2D canvas setup section with:

```js
import { initRenderer, updateCamera3d, render3d } from './renderer3d.js';

// Remove: const canvas = document.getElementById('game'); const ctx = ...
// Remove: resizeCanvas() and its resize listener
// Remove: draw2d function and cam2d

// Replace the canvas variable used by initInput:
// initInput now receives the renderer's domElement
const { renderer } = initRenderer(document.body);
initInput(renderer.domElement);
```

Update `gameLoop` to call `updateCamera3d()` and `render3d()` instead of `updateCamera2d()` and `draw2d()`:

```js
function gameLoop(ts) {
  if (!state.gameRunning) return;
  const dt=Math.min(ts-state.lastTime,100);
  state.lastTime=ts; state.tickAccum+=dt;
  while (state.tickAccum>=TICK_MS) {
    tickAllBots(TICK_MS); tickPlayers(TICK_MS);
    state.tickAccum-=TICK_MS;
  }
  updateCamera3d();
  render3d();
  drawHUD(ts);        // 2D HUD overlay still works
  // drawMinimap stays as 2D (drawn on the hud canvas instead of main canvas)
  tickFillAnims(ts); tickKillFeed(ts);
  requestAnimationFrame(gameLoop);
}
```

Also update `drawMinimap` to draw onto the HUD canvas (`hudCtx` from ui.js) instead of the old `ctx`. Export `hudCtx` from `ui.js` or pass it to a minimap module. Simplest fix: move `drawMinimap` into `ui.js` and call it from `drawHUD`.

### Step 3: Verify

Open `http://localhost:8765` — you should see a dark 3D ground plane. Clicking Play starts the game but no territory is visible yet (terrain rendering is Task 3). The HUD/leaderboard/kill feed still work.

**Commit:**
```bash
git add src/ index.html
git commit -m "feat: Three.js scene setup — ground plane, lights, 3D camera"
```

---

## Task 3: InstancedMesh Territory + Trail Rendering

**Files:**
- Create: `src/cells.js`
- Modify: `src/renderer3d.js`
- Modify: `src/main.js`

### Step 1: Create `src/cells.js`

This module manages the two `InstancedMesh`es for territory and trail cells.

```js
// src/cells.js
import * as THREE from 'three';
import { CFG } from './config.js';
import { territory, trail, state, idx, playerColor, hexToRgb } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;
const N      = CFG.GRID_W * CFG.GRID_H;

let territoryMesh, trailMesh;

// Snapshots of last-rendered state for dirty detection
const prevTerritory = new Uint8Array(N);
const prevTrail     = new Uint8Array(N);

const dummy = new THREE.Object3D();
const color = new THREE.Color();

export function initCells(scene) {
  // Territory: flat boxes (y=0.04 center, height=0.08)
  const tGeo = new THREE.BoxGeometry(0.92, 0.08, 0.92);
  const tMat = new THREE.MeshLambertMaterial();
  territoryMesh = new THREE.InstancedMesh(tGeo, tMat, N);
  territoryMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
  territoryMesh.castShadow    = false;
  territoryMesh.receiveShadow = true;

  // Trail: taller raised boxes
  const trGeo = new THREE.BoxGeometry(0.78, 0.18, 0.78);
  const trMat = new THREE.MeshLambertMaterial();
  trailMesh = new THREE.InstancedMesh(trGeo, trMat, N);
  trailMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
  trailMesh.castShadow    = true;
  trailMesh.receiveShadow = true;

  // Pre-position all instances; start invisible (scale=0)
  for (let y = 0; y < CFG.GRID_H; y++) {
    for (let x = 0; x < CFG.GRID_W; x++) {
      const i = y * CFG.GRID_W + x;
      dummy.position.set(x - HALF_W + 0.5, 0.04, y - HALF_H + 0.5);
      dummy.scale.set(0, 0, 0);
      dummy.updateMatrix();
      territoryMesh.setMatrixAt(i, dummy.matrix);
      trailMesh.setMatrixAt(i, dummy.matrix);
      color.set('#000');
      territoryMesh.setColorAt(i, color);
      trailMesh.setColorAt(i, color);
    }
  }
  territoryMesh.instanceMatrix.needsUpdate = true;
  territoryMesh.instanceColor.needsUpdate  = true;
  trailMesh.instanceMatrix.needsUpdate     = true;
  trailMesh.instanceColor.needsUpdate      = true;

  scene.add(territoryMesh);
  scene.add(trailMesh);
}

export function updateCells(now) {
  let tChanged = false, trChanged = false;

  // Build fill animation brightness map
  const brightMap = new Map();
  for (const anim of state.fillAnims) {
    const progress  = Math.min((now - anim.startTime) / CFG.FILL_ANIM_MS, 1);
    const brightness = 1 + (1 - progress) * 1.2; // flare then settle
    anim.cells.forEach(ci => brightMap.set(ci, { color: anim.color, brightness }));
  }

  for (let y = 0; y < CFG.GRID_H; y++) {
    for (let x = 0; x < CFG.GRID_W; x++) {
      const i = y * CFG.GRID_W + x;

      // Territory
      if (territory[i] !== prevTerritory[i] || brightMap.has(i)) {
        dummy.position.set(x - HALF_W + 0.5, 0.04, y - HALF_H + 0.5);
        if (territory[i] !== 0) {
          dummy.scale.set(1, 1, 1);
          const anim = brightMap.get(i);
          if (anim) {
            const [r,g,b] = hexToRgb(anim.color);
            const f = anim.brightness;
            color.setRGB(Math.min(1,r/255*f), Math.min(1,g/255*f), Math.min(1,b/255*f));
          } else {
            const [r,g,b] = hexToRgb(playerColor(territory[i]));
            color.setRGB(r/255*0.5, g/255*0.5, b/255*0.5);
          }
        } else {
          dummy.scale.set(0, 0, 0);
          color.set('#000');
        }
        dummy.updateMatrix();
        territoryMesh.setMatrixAt(i, dummy.matrix);
        territoryMesh.setColorAt(i, color);
        prevTerritory[i] = territory[i];
        tChanged = true;
      }

      // Trail
      if (trail[i] !== prevTrail[i]) {
        dummy.position.set(x - HALF_W + 0.5, 0.09, y - HALF_H + 0.5);
        if (trail[i] !== 0) {
          dummy.scale.set(1, 1, 1);
          const [r,g,b] = hexToRgb(playerColor(trail[i]));
          color.setRGB(r/255, g/255, b/255);
        } else {
          dummy.scale.set(0, 0, 0);
          color.set('#000');
        }
        dummy.updateMatrix();
        trailMesh.setMatrixAt(i, dummy.matrix);
        trailMesh.setColorAt(i, color);
        prevTrail[i] = trail[i];
        trChanged = true;
      }
    }
  }

  if (tChanged)  { territoryMesh.instanceMatrix.needsUpdate=true; territoryMesh.instanceColor.needsUpdate=true; }
  if (trChanged) { trailMesh.instanceMatrix.needsUpdate=true;     trailMesh.instanceColor.needsUpdate=true; }
}
```

### Step 2: Wire cells into renderer and game loop

In `src/renderer3d.js`, export `scene` and call `initCells(scene)` from main after scene is ready.

In `src/main.js`:
```js
import { initCells, updateCells } from './cells.js';
// After initRenderer:
const { scene } = initRenderer(document.body);
initCells(scene);

// In gameLoop, before render3d():
updateCells(ts);
```

### Step 3: Verify

Click Play — territory cells should appear as colored 3D boxes on the ground. Trail cells appear slightly higher. Fill animation flares brighter then settles.

**Commit:**
```bash
git add src/cells.js src/
git commit -m "feat: InstancedMesh territory + trail cells with dirty tracking"
```

---

## Task 4: Player 3D Meshes + Camera Polish

**Files:**
- Create: `src/players3d.js`
- Modify: `src/main.js`

### Step 1: Create `src/players3d.js`

```js
// src/players3d.js
import * as THREE from 'three';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { CFG } from './config.js';
import { state } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

const playerGroups = new Map(); // id → THREE.Group
let labelRenderer;

export function initPlayerRenderer(container, camera) {
  // CSS2D renderer for name labels
  labelRenderer = new CSS2DRenderer();
  labelRenderer.setSize(window.innerWidth, window.innerHeight);
  labelRenderer.domElement.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:5;';
  container.appendChild(labelRenderer.domElement);

  window.addEventListener('resize', () => {
    labelRenderer.setSize(window.innerWidth, window.innerHeight);
  });
}

const bodyGeo = new THREE.BoxGeometry(0.7, 0.7, 0.7);
const coneGeo = new THREE.ConeGeometry(0.15, 0.35, 6);

function createPlayerMesh(p) {
  const group = new THREE.Group();

  // Body
  const bodyMat = new THREE.MeshLambertMaterial({ color: p.color });
  const body    = new THREE.Mesh(bodyGeo, bodyMat);
  body.castShadow    = true;
  body.receiveShadow = false;
  body.position.y    = 0.35;
  group.add(body);

  // Direction cone
  const coneMat = new THREE.MeshLambertMaterial({ color: 0xffffff });
  const cone    = new THREE.Mesh(coneGeo, coneMat);
  cone.position.set(p.dx * 0.5, 0.35, p.dy * 0.5);
  cone.rotation.z = Math.PI / 2; // point horizontally
  group.add(cone);
  group.userData.cone = cone;

  // Name label
  const labelDiv = document.createElement('div');
  labelDiv.style.cssText = 'color:#fff;font:bold 11px sans-serif;text-shadow:0 1px 3px #000;pointer-events:none;';
  labelDiv.textContent   = p.name;
  const label = new CSS2DObject(labelDiv);
  label.position.set(0, 1.2, 0);
  group.add(label);

  // Point light (glow)
  const light = new THREE.PointLight(p.color, 0.8, 4);
  light.position.set(0, 0.5, 0);
  group.add(light);
  group.userData.light = light;

  group.position.set(p.x - HALF_W + 0.5, 0, p.y - HALF_H + 0.5);
  return group;
}

export function syncPlayerMeshes(scene) {
  const alive = new Set(state.players.map(p => p.id));

  // Remove dead players' meshes
  for (const [id, group] of playerGroups) {
    if (!alive.has(id)) {
      scene.remove(group);
      playerGroups.delete(id);
    }
  }

  // Add/update meshes
  for (const p of state.players) {
    if (!p.alive) {
      if (playerGroups.has(p.id)) {
        playerGroups.get(p.id).visible = false;
      }
      continue;
    }

    if (!playerGroups.has(p.id)) {
      const group = createPlayerMesh(p);
      scene.add(group);
      playerGroups.set(p.id, group);
    }

    const group = playerGroups.get(p.id);
    group.visible = true;

    // Smooth position update
    const tx = p.x - HALF_W + 0.5;
    const tz = p.y - HALF_H + 0.5;
    group.position.lerp(new THREE.Vector3(tx, 0, tz), 0.3);

    // Update direction cone orientation
    const cone = group.userData.cone;
    if (cone) {
      const angle = Math.atan2(p.dx, p.dy);
      cone.position.set(p.dx * 0.45, 0.35, p.dy * 0.45);
      cone.rotation.set(0, angle, Math.PI/2);
    }
  }
}

export function renderLabels(scene, camera) {
  if (labelRenderer) labelRenderer.render(scene, camera);
}
```

### Step 2: Wire into `main.js`

```js
import { initPlayerRenderer, syncPlayerMeshes, renderLabels } from './players3d.js';

// After initRenderer:
const { scene, camera, renderer } = initRenderer(document.body);
initPlayerRenderer(document.body, camera);

// In gameLoop, after updateCells(ts):
syncPlayerMeshes(scene);

// After render3d():
renderLabels(scene, camera);
```

### Step 3: Verify

Players appear as colored 3D cubes. Names float above them. Direction cone faces movement direction. Camera smoothly follows human player.

**Commit:**
```bash
git add src/players3d.js src/main.js
git commit -m "feat: 3D player meshes with CSS2D name labels and glow lights"
```

---

## Task 5: Minimap on HUD Canvas

**Files:**
- Modify: `src/ui.js`
- Modify: `src/main.js`

Move `drawMinimap` into `ui.js` so it renders onto the HUD canvas (not the old 2D canvas which no longer exists as a 2D context).

### Step 1: Add minimap to `src/ui.js`

Add these at the top of `ui.js` after the existing imports:
```js
import { CFG } from './config.js';
import { territory, trail, playerColor, hexToRgb } from './state.js';
```

Add minimap setup variables and function to `ui.js`:

```js
// ---- Minimap ----
const minimapOffscreen = document.createElement('canvas');
minimapOffscreen.width  = CFG.GRID_W;
minimapOffscreen.height = CFG.GRID_H;
const mmCtx     = minimapOffscreen.getContext('2d');
const mmImgData = mmCtx.createImageData(CFG.GRID_W, CFG.GRID_H);

function drawMinimap() {
  if (!hudCtx) return;
  const d = mmImgData.data;
  for (let i = 0; i < territory.length; i++) {
    const t=territory[i], tr_=trail[i], pi=i*4;
    if (tr_!==0) {
      const [r,g,b]=hexToRgb(playerColor(tr_));
      d[pi]=r;d[pi+1]=g;d[pi+2]=b;d[pi+3]=255;
    } else if (t!==0) {
      const [r,g,b]=hexToRgb(playerColor(t));
      d[pi]=r*0.55|0;d[pi+1]=g*0.55|0;d[pi+2]=b*0.55|0;d[pi+3]=255;
    } else {
      d[pi]=22;d[pi+1]=33;d[pi+2]=62;d[pi+3]=255;
    }
  }
  mmCtx.putImageData(mmImgData,0,0);
  const mx=hudCanvas.width-CFG.MINIMAP_SIZE-CFG.MINIMAP_PAD;
  const my=CFG.MINIMAP_PAD;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.fillRect(mx-2,my-2,CFG.MINIMAP_SIZE+4,CFG.MINIMAP_SIZE+4);
  hudCtx.drawImage(minimapOffscreen,mx,my,CFG.MINIMAP_SIZE,CFG.MINIMAP_SIZE);
  for (const p of state.players) {
    if (!p.alive) continue;
    const px=mx+(p.x/CFG.GRID_W)*CFG.MINIMAP_SIZE;
    const py=my+(p.y/CFG.GRID_H)*CFG.MINIMAP_SIZE;
    hudCtx.beginPath(); hudCtx.arc(px,py,p.isBot?2:3.5,0,Math.PI*2);
    hudCtx.fillStyle=p.isBot?p.color:'#fff'; hudCtx.fill();
  }
}
```

Call `drawMinimap()` from inside `drawHUD()`.

### Step 2: Remove minimap from `main.js`

Delete the old `minimapCanvas`, `mmCtx`, `mmImgData`, and `drawMinimap()` from `main.js`.

### Step 3: Verify

Minimap appears top-right. All HUD elements work correctly.

**Commit:**
```bash
git add src/
git commit -m "feat: minimap moved to HUD canvas overlay"
```

---

## Task 6: Final Cleanup + Polish

**Files:**
- Modify: `src/main.js`
- Modify: `src/renderer3d.js`

### Step 1: Remove all remaining 2D canvas shim code from `main.js`

Delete:
- `const canvas = document.getElementById('game')` (the old 2D canvas shim at top — keep `renderer.domElement` from Three.js)
- `cam2d` object and `updateCamera2d()`
- `draw2d()` function
- Old `drawMinimap()` if not already removed

Ensure `initInput` receives `renderer.domElement` as the touch target.

### Step 2: Add subtle ground grid lines to `renderer3d.js`

Add a `GridHelper` for the subtle grid feel:
```js
const grid = new THREE.GridHelper(CFG.GRID_W, CFG.GRID_W, 0x1a2540, 0x1a2540);
grid.position.y = 0.001; // just above ground
scene.add(grid);
```

### Step 3: Add player bob animation in `players3d.js`

Inside `syncPlayerMeshes`, for alive players, add a subtle up/down bob:
```js
const t = performance.now() / 400;
group.position.y = Math.sin(t + p.id) * 0.05; // gentle bob
```

### Step 4: Verify full game playthrough

- Start screen → Play
- Territory fills with bright 3D animated cells
- Bots move, attack, retreat
- Kill feed, death screen, respawn all work
- Minimap correct
- Camera follows player smoothly at fixed angle
- No console errors

### Step 5: Final commit + push

```bash
git add src/ index.html
git commit -m "feat: complete 3D rendering — Three.js InstancedMesh, player meshes, camera follow"
git push origin main
```

---

## Summary

| Task | Builds | Risk |
|------|--------|------|
| 1 | ES module restructure (game logic unchanged) | High — many files |
| 2 | Three.js scene: renderer, lights, ground, camera | Medium |
| 3 | InstancedMesh territory + trail cells | Medium |
| 4 | Player 3D meshes + CSS2D name labels | Low |
| 5 | Minimap on HUD canvas | Low |
| 6 | Cleanup + polish (grid, bob, final push) | Low |

**Performance targets after completion:**
- 2 draw calls for all territory (InstancedMesh)
- 2 draw calls for all trails
- N draw calls for N player meshes (N ≤ 10)
- CSS2D name labels: 0 texture memory
- Minimap: 1 ImageData pass per frame (typed array, fast)
