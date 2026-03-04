import * as THREE from 'three';
import { CFG } from './config.js';
import { territory, state, idx, playerColor, hexToRgb } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;
const N      = CFG.GRID_W * CFG.GRID_H;

let territoryMesh;

const prevTerritory = new Uint8Array(N);
const dummy     = new THREE.Object3D();
const color     = new THREE.Color();
const brightMap = new Map();

export function initCells(scene) {
  const tGeo = new THREE.BoxGeometry(0.92, 0.08, 0.92);
  const tMat = new THREE.MeshLambertMaterial();
  territoryMesh = new THREE.InstancedMesh(tGeo, tMat, N);
  territoryMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
  territoryMesh.castShadow    = false;
  territoryMesh.receiveShadow = true;

  for (let y = 0; y < CFG.GRID_H; y++) {
    for (let x = 0; x < CFG.GRID_W; x++) {
      const i = y * CFG.GRID_W + x;
      dummy.position.set(x - HALF_W + 0.5, 0.04, y - HALF_H + 0.5);
      dummy.scale.set(0, 0, 0);
      dummy.updateMatrix();
      territoryMesh.setMatrixAt(i, dummy.matrix);
      color.set('#000');
      territoryMesh.setColorAt(i, color);
    }
  }
  territoryMesh.instanceMatrix.needsUpdate = true;
  territoryMesh.instanceColor.needsUpdate  = true;

  scene.add(territoryMesh);
}

export function updateCells(now) {
  let tChanged = false;

  brightMap.clear();
  for (const anim of state.fillAnims) {
    const progress   = Math.min((now - anim.startTime) / CFG.FILL_ANIM_MS, 1);
    const brightness = 1 + (1 - progress) * 1.2;
    anim.cells.forEach(ci => brightMap.set(ci, { color: anim.color, brightness }));
  }

  for (let y = 0; y < CFG.GRID_H; y++) {
    for (let x = 0; x < CFG.GRID_W; x++) {
      const i = y * CFG.GRID_W + x;
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
    }
  }

  if (tChanged) {
    territoryMesh.instanceMatrix.needsUpdate = true;
    territoryMesh.instanceColor.needsUpdate  = true;
  }
}
