import * as THREE from 'three';
import { CFG } from './config.js';
import { state } from './state.js';

let renderer, scene, camera, sun;

const HALF_W = CFG.GRID_W / 2;
const HALF_H = CFG.GRID_H / 2;

const _camTarget  = new THREE.Vector3();
const _lookTarget = new THREE.Vector3();

export function initRenderer(container) {
  renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type    = THREE.PCFSoftShadowMap;
  renderer.setClearColor(0x0d1b2a);
  container.appendChild(renderer.domElement);

  scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x0d1b2a, 0.008);

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

  camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 1000);
  camera.position.set(0, 18, 12);
  camera.lookAt(0, 0, 0);

  window.addEventListener('resize', onResize);
  return { renderer, scene, camera };
}

export function onResize() {
  if (!renderer || !camera) return;
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
}

export function cellToWorld(x, y) {
  return new THREE.Vector3(x - HALF_W + 0.5, 0, y - HALF_H + 0.5);
}

export function updateCamera3d() {
  const h = state.humanPlayer;
  if (!h || !h.alive) return;

  const wx = h.x - HALF_W + 0.5;
  const wz = h.y - HALF_H + 0.5;

  const behindX = -Math.cos(h.direction) * 12;
  const behindZ = -Math.sin(h.direction) * 12;
  _camTarget.set(wx + behindX, 15, wz + behindZ);
  camera.position.lerp(_camTarget, 0.12);

  _lookTarget.set(wx, 0, wz);
  camera.lookAt(_lookTarget);

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
