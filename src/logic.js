import { CFG } from './config.js';
import { territory, state, idx, inBounds, randomEmptyCell, getTerritoryCount } from './state.js';

// ─── Player lifecycle ────────────────────────────────────────────────────────

export function createPlayer(name, color, isBot = false) {
  const id = state.nextId++;
  const { x, y } = randomEmptyCell();
  const p = {
    id, name, color, isBot,
    x, y,
    direction:    Math.random() * Math.PI * 2,  // radians; 0=+X, PI/2=+Z
    speed:        CFG.PLAYER_SPEED,
    turnInput:    0,                             // -1 left, 0 straight, 1 right
    trailPoints:  [],                            // [{x, y}] grid float coords
    lastTrailX:   x,
    lastTrailY:   y,
    alive:        true,
    kills:        0,
    botState:     'expand',
    botTimer:     0,
    respawnTimer: 0,
  };
  stampHome(p);
  state.players.push(p);
  return p;
}

export function stampHome(p) {
  const gx = Math.floor(p.x), gy = Math.floor(p.y);
  for (let dy = -2; dy <= 2; dy++)
    for (let dx = -2; dx <= 2; dx++)
      if (inBounds(gx+dx, gy+dy))
        territory[idx(gx+dx, gy+dy)] = p.id;
}

export function respawnBot(p) {
  const { x, y } = randomEmptyCell();
  p.x = x; p.y = y;
  p.direction = Math.random() * Math.PI * 2;
  p.turnInput = 0;
  p.trailPoints = [];
  p.lastTrailX = x; p.lastTrailY = y;
  p.alive = true;
  stampHome(p);
}

export function respawnHuman() {
  const h = state.humanPlayer;
  const { x, y } = randomEmptyCell();
  h.x = x; h.y = y;
  h.direction = Math.random() * Math.PI * 2;
  h.turnInput = 0;
  h.trailPoints = [];
  h.lastTrailX = x; h.lastTrailY = y;
  h.alive = true; h.kills = 0;
  stampHome(h);
}

// ─── Collision helpers ───────────────────────────────────────────────────────

// Squared distance from point (px,py) to segment (a→b)
function distToSegment(px, py, a, b) {
  const dx = b.x - a.x, dy = b.y - a.y;
  const lenSq = dx*dx + dy*dy;
  if (lenSq === 0) return Math.hypot(px - a.x, py - a.y);
  let t = ((px - a.x)*dx + (py - a.y)*dy) / lenSq;
  t = Math.max(0, Math.min(1, t));
  return Math.hypot(px - (a.x + t*dx), py - (a.y + t*dy));
}

// True if player p has run into their own trail (skip last SKIP recent segments)
function checkSelfTrail(p) {
  const SKIP = 10;
  const pts = p.trailPoints;
  for (let i = 0; i < pts.length - SKIP - 1; i++) {
    if (distToSegment(p.x, p.y, pts[i], pts[i+1]) < CFG.COLLISION_RADIUS) return true;
  }
  return false;
}

// Returns the enemy whose trail p is touching, or null
function checkEnemyTrail(p) {
  for (const enemy of state.players) {
    if (enemy.id === p.id || !enemy.alive || enemy.trailPoints.length < 2) continue;
    for (let i = 0; i < enemy.trailPoints.length - 1; i++) {
      if (distToSegment(p.x, p.y, enemy.trailPoints[i], enemy.trailPoints[i+1]) < CFG.COLLISION_RADIUS) {
        return enemy;
      }
    }
  }
  return null;
}

// ─── Movement ────────────────────────────────────────────────────────────────

export function movePlayer(p, dt) {
  // Apply turn
  p.direction += p.turnInput * CFG.TURN_SPEED * (dt / 1000);

  // Compute new position
  const nx = p.x + Math.cos(p.direction) * p.speed * (dt / 1000);
  const ny = p.y + Math.sin(p.direction) * p.speed * (dt / 1000);

  // Boundary: reflect at edges
  const M = 0.8;
  if (nx < M || nx >= CFG.GRID_W - M) { p.direction = Math.PI - p.direction; return; }
  if (ny < M || ny >= CFG.GRID_H - M) { p.direction = -p.direction; return; }
  p.x = nx; p.y = ny;

  // Collision checks
  if (checkSelfTrail(p))            { killPlayer(p, null); return; }
  const enemy = checkEnemyTrail(p);
  if (enemy)                         { killPlayer(enemy, p); return; }

  // Trail recording + territory reconnect
  const gx = Math.floor(p.x), gy = Math.floor(p.y);
  const cellOwner = inBounds(gx, gy) ? territory[idx(gx, gy)] : 0;

  if (cellOwner === p.id && p.trailPoints.length > 0) {
    claimTerritory(p);
  } else if (cellOwner !== p.id) {
    const dist = Math.hypot(p.x - p.lastTrailX, p.y - p.lastTrailY);
    if (dist >= CFG.TRAIL_SAMPLE_DIST) {
      p.trailPoints.push({ x: p.x, y: p.y });
      p.lastTrailX = p.x;
      p.lastTrailY = p.y;
    }
  }
}

// ─── Kill ────────────────────────────────────────────────────────────────────

export function killPlayer(victim, killer) {
  if (!victim || !victim.alive) return;
  victim.alive = false;

  const pct = ((getTerritoryCount(victim.id) / territory.length) * 100).toFixed(1);

  if (killer) {
    killer.kills++;
    for (let i = 0; i < territory.length; i++) {
      if (territory[i] === victim.id) territory[i] = killer.id;
    }
  } else {
    for (let i = 0; i < territory.length; i++) {
      if (territory[i] === victim.id) territory[i] = 0;
    }
  }
  victim.trailPoints = [];

  window.dispatchEvent(new CustomEvent('playerKilled', {
    detail: { killer, victim, pct }
  }));

  if (victim.isBot) victim.respawnTimer = 2000;
}

// ─── Territory capture ───────────────────────────────────────────────────────

// Bresenham line: calls callback(gx, gy) for each cell along a→b
function rasterizeLine(a, b, callback) {
  let x0 = Math.floor(a.x), y0 = Math.floor(a.y);
  let x1 = Math.floor(b.x), y1 = Math.floor(b.y);
  const dx = Math.abs(x1-x0), sx = x0 < x1 ? 1 : -1;
  const dy = Math.abs(y1-y0), sy = y0 < y1 ? 1 : -1;
  let err = (dx > dy ? dx : -dy) >> 1;
  while (true) {
    callback(x0, y0);
    if (x0 === x1 && y0 === y1) break;
    const e2 = err;
    if (e2 > -dx) { err -= dy; x0 += sx; }
    if (e2 <  dy) { err += dx; y0 += sy; }
  }
}

export function claimTerritory(p) {
  if (p.trailPoints.length < 2) {
    p.trailPoints = [];
    p.lastTrailX = p.x; p.lastTrailY = p.y;
    return;
  }

  // Step 1: Rasterise trail path as boundary cells, claim them
  for (let i = 0; i < p.trailPoints.length - 1; i++) {
    rasterizeLine(p.trailPoints[i], p.trailPoints[i+1], (gx, gy) => {
      if (inBounds(gx, gy)) territory[idx(gx, gy)] = p.id;
    });
  }
  // Close the loop back to player's current position
  const last = p.trailPoints[p.trailPoints.length - 1];
  rasterizeLine(last, { x: p.x, y: p.y }, (gx, gy) => {
    if (inBounds(gx, gy)) territory[idx(gx, gy)] = p.id;
  });

  // Step 2: Flood fill from all edges to find "outside" cells
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
    const i = queue[qi++];
    const cx = i % CFG.GRID_W, cy = (i / CFG.GRID_W) | 0;
    if (cx > 0)            enqueue(cx-1, cy);
    if (cx < CFG.GRID_W-1) enqueue(cx+1, cy);
    if (cy > 0)            enqueue(cx, cy-1);
    if (cy < CFG.GRID_H-1) enqueue(cx, cy+1);
  }

  // Step 3: Claim all enclosed cells
  const newCells = new Set();
  for (let i = 0; i < territory.length; i++) {
    if (!outside[i] && territory[i] !== p.id) {
      territory[i] = p.id;
      newCells.add(i);
    }
  }

  p.trailPoints = [];
  p.lastTrailX = p.x; p.lastTrailY = p.y;

  if (newCells.size > 0) {
    state.fillAnims.push({ cells: newCells, color: p.color, startTime: performance.now() });
  }
}

// ─── Game tick ───────────────────────────────────────────────────────────────

export function tickPlayers(dt) {
  for (const p of state.players) {
    if (!p.alive) {
      if (p.isBot) { p.respawnTimer -= dt; if (p.respawnTimer <= 0) respawnBot(p); }
      continue;
    }
    movePlayer(p, dt);
  }
}
