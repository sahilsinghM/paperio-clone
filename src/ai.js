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
