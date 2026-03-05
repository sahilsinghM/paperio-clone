import * as THREE from 'three';
import { CFG } from './config.js';
import { state } from './state.js';

let renderer, scene, camera, sun;

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

const _camTarget  = new THREE.Vector3();
const _lookTarget = new THREE.Vector3();

// ─── Screen shake ────────────────────────────────────────────────────────────
let _shakeEnd       = 0;
let _shakeIntensity = 0;
let _shakePhase     = 0;

export function triggerScreenShake(durationMs = 350, intensity = 0.5) {
  _shakeEnd       = performance.now() + durationMs;
  _shakeIntensity = intensity;
  _shakePhase     = 0;
}

export function initRenderer(container) {
  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type    = THREE.PCFSoftShadowMap;
  renderer.setClearColor(0x0d1b2a);
  container.appendChild(renderer.domElement);

  scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x0d1b2a, 0.004);

  scene.add(new THREE.AmbientLight(0xffffff, 0.6));

  sun = new THREE.DirectionalLight(0xffffff, 1.0);
  sun.position.set(100, 150, 80);
  sun.castShadow = true;
  sun.shadow.mapSize.width  = 2048;
  sun.shadow.mapSize.height = 2048;
  sun.shadow.camera.near    = 1;
  sun.shadow.camera.far     = 500;
  sun.shadow.camera.left    = -80;
  sun.shadow.camera.right   = 80;
  sun.shadow.camera.top     = 80;
  sun.shadow.camera.bottom  = -80;
  scene.add(sun);
  scene.add(sun.target); // required: target must be in scene for position updates to take effect

  const groundGeo = new THREE.PlaneGeometry(CFG.GRID_W + 40, CFG.GRID_H + 40);
  const groundMat = new THREE.MeshLambertMaterial({ color: 0x16213e });
  const ground    = new THREE.Mesh(groundGeo, groundMat);
  ground.rotation.x    = -Math.PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);

  const grid = new THREE.GridHelper(CFG.GRID_W, CFG.GRID_W, 0x1a2540, 0x1a2540);
  grid.position.y = 0.001;
  scene.add(grid);

  camera = new THREE.PerspectiveCamera(72, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.up.set(0, 0, -1);       // world -Z = screen up (needed for straight-down lookAt)
  camera.position.set(0, 28, 0);
  camera.lookAt(0, 0, 0);

  window.addEventListener('resize', onResize);
  return { renderer, scene, camera };
}

export function onResize() {
  if (!renderer || !camera) return;
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
  camera.up.set(0, 0, -1);   // preserve top-down orientation after resize
}

export function cellToWorld(x, y) {
  return new THREE.Vector3(x - HALF_W + 0.5, 0, y - HALF_H + 0.5);
}

export function updateCamera3d() {
  const h = state.humanPlayer;
  if (!h || !h.alive) return;

  const wx = h.x - HALF_W + 0.5;
  const wz = h.y - HALF_H + 0.5;

  // True top-down: camera directly above player, no forward tilt.
  // Trail is visible in all directions around the player.
  // camera.up=(0,0,-1) set at init avoids gimbal lock when looking straight down.
  _camTarget.set(wx, 28, wz);
  camera.position.lerp(_camTarget, 0.18);   // fast at 60 Hz physics — no perceptible lag

  _lookTarget.set(wx, 0, wz);
  camera.lookAt(_lookTarget);

  // Screen shake — smooth sine wave (no per-frame random jitter)
  const now = performance.now();
  if (now < _shakeEnd) {
    const t = (_shakeEnd - now) / 400;          // 1→0 as shake fades out
    _shakePhase += 0.35;
    const s = t * _shakeIntensity;
    camera.position.x += Math.sin(_shakePhase * 2.1) * s;
    camera.position.z += Math.sin(_shakePhase * 1.7 + 1) * s;
  }

  sun.position.set(wx + 100, 150, wz + 80);
  sun.target.position.set(wx, 0, wz);
  sun.target.updateMatrixWorld();
}

export function render3d() {
  renderer.render(scene, camera);
}

export function getScene()    { return scene; }
export function getCamera()   { return camera; }
export function getRenderer() { return renderer; }
