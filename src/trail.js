import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { CFG } from './config.js';
import { state } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

// Each player gets { core: Line2, glow: Line2 }
const trailLines = new Map();

let _scene = null;
let _resolution = new THREE.Vector2();

export function initTrails(scene) {
  _scene = scene;
  _resolution.set(window.innerWidth, window.innerHeight);
  window.addEventListener('resize', () => _resolution.set(window.innerWidth, window.innerHeight));
}

function worldX(gx) { return gx - HALF_W + 0.5; }
function worldZ(gy) { return gy - HALF_H + 0.5; }

// Brighten a hex color by factor f
function brightenColor(hex, f) {
  const r = Math.min(255, parseInt(hex.slice(1,3), 16) * f) | 0;
  const g = Math.min(255, parseInt(hex.slice(3,5), 16) * f) | 0;
  const b = Math.min(255, parseInt(hex.slice(5,7), 16) * f) | 0;
  return new THREE.Color(r/255, g/255, b/255);
}

function makeLine(color, linewidth, opacity) {
  const mat = new LineMaterial({
    color,
    linewidth,
    resolution: _resolution,
    depthTest:  false,
    transparent: opacity < 1,
    opacity,
  });
  const geo  = new LineGeometry();
  const line = new Line2(geo, mat);
  line.renderOrder = 1;
  return line;
}

function getOrCreateLines(p) {
  if (trailLines.has(p.id)) return trailLines.get(p.id);

  const core = makeLine(new THREE.Color(p.color), 8, 1.0);
  const glow = makeLine(brightenColor(p.color, 1.8), 18, 0.22);
  glow.renderOrder = 0;   // render glow behind core

  _scene.add(glow);
  _scene.add(core);
  const entry = { core, glow };
  trailLines.set(p.id, entry);
  return entry;
}

function disposeLine(line) {
  _scene.remove(line);
  line.geometry.dispose();
  line.material.dispose();
}

export function updateTrails() {
  const activeIds = new Set(state.players.map(p => p.id));

  // Remove lines for dead/gone players
  for (const [id, entry] of trailLines) {
    if (!activeIds.has(id)) {
      disposeLine(entry.core);
      disposeLine(entry.glow);
      trailLines.delete(id);
    }
  }

  for (const p of state.players) {
    if (!p.alive || p.trailPoints.length < 1) {
      const entry = trailLines.get(p.id);
      if (entry) { entry.core.visible = false; entry.glow.visible = false; }
      continue;
    }

    const { core, glow } = getOrCreateLines(p);
    core.visible = true;
    glow.visible = true;

    // Build flat [x,y,z, ...] positions
    const positions = [];
    for (const pt of p.trailPoints) {
      positions.push(worldX(pt.x), 0.28, worldZ(pt.y));
    }
    positions.push(worldX(p.x), 0.28, worldZ(p.y));

    core.geometry.setPositions(positions);
    glow.geometry.setPositions(positions);
    core.computeLineDistances();
    glow.computeLineDistances();

    // Keep colors synced in case player color ever changes
    core.material.color.set(p.color);
    glow.material.color.copy(brightenColor(p.color, 1.8));
    core.material.resolution.copy(_resolution);
    glow.material.resolution.copy(_resolution);
  }
}
