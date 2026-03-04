import { CFG } from './config.js';
import { territory, state, idx, inBounds } from './state.js';

// Wrap angle to [-PI, PI]
function wrapAngle(a) {
  while (a >  Math.PI) a -= 2 * Math.PI;
  while (a < -Math.PI) a += 2 * Math.PI;
  return a;
}

// Set turnInput toward target world position
function steerToward(p, tx, ty) {
  const desired = Math.atan2(ty - p.y, tx - p.x);
  const diff    = wrapAngle(desired - p.direction);
  p.turnInput   = diff > 0.05 ? 1 : diff < -0.05 ? -1 : 0;
}

// Find nearest grid cell owned by player p (random sample, cheap)
function expansionTarget(p) {
  // Pick a random unowned cell 5-30 cells away
  const gx = Math.floor(p.x), gy = Math.floor(p.y);
  for (let attempt = 0; attempt < 20; attempt++) {
    const dist = 5 + Math.floor(Math.random() * 25);
    const angle = Math.random() * Math.PI * 2;
    const tx = gx + Math.round(Math.cos(angle) * dist);
    const ty = gy + Math.round(Math.sin(angle) * dist);
    if (!inBounds(tx, ty)) continue;
    const t = territory[idx(tx, ty)];
    if (t === 0 || t !== p.id) return { x: tx + 0.5, y: ty + 0.5 };
  }
  // fallback: steer toward map centre
  return { x: CFG.GRID_W / 2, y: CFG.GRID_H / 2 };
}

// Find nearest cell owned by player p (for retreat)
function nearestOwnedCell(p) {
  const gx = Math.floor(p.x), gy = Math.floor(p.y);
  for (let r = 1; r < 60; r++) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.abs(dx) !== r && Math.abs(dy) !== r) continue;
        const nx = gx + dx, ny = gy + dy;
        if (inBounds(nx, ny) && territory[idx(nx, ny)] === p.id) {
          return { x: nx + 0.5, y: ny + 0.5 };
        }
      }
    }
  }
  return null;
}

// Is there an enemy trail near current position?
function enemyTrailNearby(p) {
  for (const other of state.players) {
    if (other.id === p.id || !other.alive) continue;
    for (const pt of other.trailPoints) {
      if (Math.hypot(pt.x - p.x, pt.y - p.y) < 4) return pt;
    }
  }
  return null;
}

function avoidBoundary(p) {
  const M = 8;
  const gx = p.x, gy = p.y;
  if (gx < M || gx > CFG.GRID_W - M || gy < M || gy > CFG.GRID_H - M) {
    steerToward(p, CFG.GRID_W / 2, CFG.GRID_H / 2);
    return true;
  }
  return false;
}

function tickBot(p, dt) {
  p.botTimer -= dt;

  const onOwn    = territory[idx(Math.floor(p.x), Math.floor(p.y))] === p.id;
  const trailLen = p.trailPoints.length;

  // State transitions
  if (trailLen > CFG.TRAIL_LIMIT)             p.botState = 'retreat';
  if (p.botState === 'retreat' && onOwn && trailLen === 0) p.botState = 'expand';

  if (avoidBoundary(p)) return;

  if (p.botState === 'retreat') {
    const home = nearestOwnedCell(p);
    if (home) steerToward(p, home.x, home.y);
    return;
  }

  // Opportunistic: if there's a nearby enemy trail, try to cut it
  const threat = enemyTrailNearby(p);
  if (threat && trailLen < 50) {
    steerToward(p, threat.x, threat.y);
    return;
  }

  // Expand: aim at a random unowned cell
  if (p.botTimer <= 0) {
    p.botTimer = CFG.BOT_THINK_MS + Math.random() * 100;
    const target = expansionTarget(p);
    p._botTarget = target;
  }

  if (p._botTarget) steerToward(p, p._botTarget.x, p._botTarget.y);
}

export function tickAllBots(dt) {
  for (const p of state.players) {
    if (p.isBot && p.alive) tickBot(p, dt);
  }
}
