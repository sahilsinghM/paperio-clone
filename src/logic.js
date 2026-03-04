import { CFG } from './config.js';
import { territory, trail, state, idx, inBounds, randomEmptyCell, getTerritoryCount, getPlayerById } from './state.js';

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
  if (trail[i] !== 0)     { killPlayer(getPlayerById(trail[i]), p); return; }
  if (territory[i] === p.id && p.trail.size > 0) { claimTerritory(p); return; }
  if (territory[i] !== p.id) { trail[i] = p.id; p.trail.add(i); }
}

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
