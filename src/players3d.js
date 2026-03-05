import * as THREE from 'three';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';
import { CFG } from './config.js';
import { state } from './state.js';

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

const playerGroups = new Map();
const _pos = new THREE.Vector3();
let labelRenderer;

export function initPlayerRenderer(container, camera) {
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

  const bodyMat = new THREE.MeshLambertMaterial({ color: p.color });
  const body    = new THREE.Mesh(bodyGeo, bodyMat);
  body.castShadow    = true;
  body.receiveShadow = false;
  body.position.y    = 0.35;
  group.add(body);
  group.userData.body    = body;
  group.userData.bodyMat = bodyMat;

  const coneMat = new THREE.MeshLambertMaterial({ color: 0xffffff });
  const cone    = new THREE.Mesh(coneGeo, coneMat);
  cone.position.set(Math.cos(p.direction) * 0.45, 0.35, Math.sin(p.direction) * 0.45);
  cone.rotation.set(0, -p.direction, Math.PI / 2);
  group.add(cone);
  group.userData.cone = cone;

  const labelDiv = document.createElement('div');
  labelDiv.style.cssText = 'color:#fff;font:bold 11px sans-serif;text-shadow:0 1px 3px #000;pointer-events:none;';
  labelDiv.textContent   = p.name;
  const label = new CSS2DObject(labelDiv);
  label.position.set(0, 0.85, 0);
  group.add(label);

  const light = new THREE.PointLight(p.color, 0.8, 4);
  light.position.set(0, 0.5, 0);
  group.add(light);
  group.userData.light     = light;
  group.userData.baseColor = new THREE.Color(p.color);

  group.position.set(p.x - HALF_W + 0.5, 0, p.y - HALF_H + 0.5);
  return group;
}

export function syncPlayerMeshes(scene) {
  const alive = new Set(state.players.map(p => p.id));

  for (const [id, group] of playerGroups) {
    if (!alive.has(id)) {
      scene.remove(group);
      playerGroups.delete(id);
    }
  }

  const now = performance.now();

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

    const tx = p.x - HALF_W + 0.5;
    const tz = p.y - HALF_H + 0.5;
    _pos.set(tx, 0, tz);
    group.position.lerp(_pos, 0.3);

    const t = now / 400;
    group.position.y = Math.sin(t + p.id) * 0.05;

    const cone = group.userData.cone;
    if (cone) {
      cone.position.set(Math.cos(p.direction) * 0.45, 0.35, Math.sin(p.direction) * 0.45);
      cone.rotation.set(0, -p.direction, Math.PI / 2);
    }

    // Danger pulse: flash red when trail is dangerously long
    const inDanger = p.trailPoints.length >= CFG.TRAIL_WARN;
    const bodyMat  = group.userData.bodyMat;
    const light    = group.userData.light;
    const base     = group.userData.baseColor;
    if (inDanger) {
      const pulse = 0.5 + 0.5 * Math.abs(Math.sin(now / 220));
      bodyMat.color.setRGB(
        base.r + (1 - base.r) * pulse * 0.7,
        base.g * (1 - pulse * 0.5),
        base.b * (1 - pulse * 0.5),
      );
      light.color.setRGB(1, 0.15, 0.05);
      light.intensity = 1.2 + pulse * 0.8;
    } else {
      bodyMat.color.copy(base);
      light.color.set(p.color);
      light.intensity = 0.8;
    }
  }
}

export function renderLabels(scene, camera) {
  if (labelRenderer) labelRenderer.render(scene, camera);
}
