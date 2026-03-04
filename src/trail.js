import * as THREE from 'three';
import { Line2 } from 'three/addons/lines/Line2.js';
import { LineMaterial } from 'three/addons/lines/LineMaterial.js';
import { LineGeometry } from 'three/addons/lines/LineGeometry.js';
import { CFG } from './config.js';
import { state } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

// map: player.id → Line2
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

function getOrCreateLine(p) {
  if (trailLines.has(p.id)) return trailLines.get(p.id);
  const mat = new LineMaterial({
    color: new THREE.Color(p.color),
    linewidth: 3,          // pixels
    resolution: _resolution,
  });
  const geo  = new LineGeometry();
  const line = new Line2(geo, mat);
  line.renderOrder = 1;
  _scene.add(line);
  trailLines.set(p.id, line);
  return line;
}

export function updateTrails() {
  const activeIds = new Set(state.players.map(p => p.id));

  // Remove lines for dead/gone players
  for (const [id, line] of trailLines) {
    if (!activeIds.has(id)) {
      _scene.remove(line);
      line.geometry.dispose();
      line.material.dispose();
      trailLines.delete(id);
    }
  }

  for (const p of state.players) {
    if (!p.alive || p.trailPoints.length < 2) {
      // Hide the line if trail is empty
      const line = trailLines.get(p.id);
      if (line) line.visible = false;
      continue;
    }

    const line = getOrCreateLine(p);
    line.visible = true;

    // Build flat [x,y,z, x,y,z, ...] positions array
    const positions = [];
    for (const pt of p.trailPoints) {
      positions.push(worldX(pt.x), 0.25, worldZ(pt.y));
    }
    // Add current player position as last point
    positions.push(worldX(p.x), 0.25, worldZ(p.y));

    line.geometry.setPositions(positions);
    line.computeLineDistances();
    line.material.color.set(p.color);
    line.material.resolution.copy(_resolution);
  }
}
