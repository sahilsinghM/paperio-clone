import { CFG } from './config.js';

export const territory = new Uint8Array(CFG.GRID_W * CFG.GRID_H);
// NOTE: trail[] removed — trails are now player.trailPoints arrays

export const state = {
  players:     [],
  nextId:      1,
  gameRunning: false,
  lastTime:    0,
  tickAccum:   0,
  humanPlayer: null,
  killFeed:    [],
  fillAnims:   [],
};

export const TICK_MS = 1000 / CFG.TICK_RATE;

export function idx(x, y)      { return ((y | 0) * CFG.GRID_W + (x | 0)); }
export function inBounds(x, y) { return x >= 0 && x < CFG.GRID_W && y >= 0 && y < CFG.GRID_H; }

export function randomEmptyCell() {
  let x, y, attempts = 0;
  do {
    x = 15 + Math.floor(Math.random() * (CFG.GRID_W - 30));
    y = 15 + Math.floor(Math.random() * (CFG.GRID_H - 30));
    attempts++;
  } while (territory[idx(x, y)] !== 0 && attempts < 500);
  return { x: x + 0.5, y: y + 0.5 }; // float center of cell
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
