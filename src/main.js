import { CFG } from './config.js';
import { territory, trail, state, TICK_MS, sanitizeName, applyRoundRectPolyfill } from './state.js';
import { createPlayer, tickPlayers, respawnHuman } from './logic.js';
import { tickAllBots } from './ai.js';
import { initInput } from './input.js';
import { initColorPicker, initHUD, drawHUD, tickKillFeed, tickFillAnims, addKillFeed } from './ui.js';

import { initRenderer, updateCamera3d, render3d } from './renderer3d.js';
import { initCells, updateCells } from './cells.js';
import { initPlayerRenderer, syncPlayerMeshes, renderLabels } from './players3d.js';

applyRoundRectPolyfill();

window.addEventListener('error', e => console.error('Global error:', e.error));
window.addEventListener('unhandledrejection', e => console.error('Unhandled rejection:', e.reason));

const { scene, camera, renderer } = initRenderer(document.body);
initCells(scene);
initPlayerRenderer(document.body, camera);

const canvas = renderer.domElement;
initInput(canvas);
initHUD();

let selectedColor = CFG.COLORS[0];
const getSelectedColor = initColorPicker(c => { selectedColor = c; });
const BOT_NAMES = ['Zara','Nova','Pixel','Echo','Blaze','Frost','Vex','Onyx','Cleo'];

export function startGame() {
  territory.fill(0); trail.fill(0);
  state.players=[]; state.nextId=1;
  state.killFeed.length=0; state.fillAnims.length=0;
  state.humanPlayer = createPlayer(sanitizeName(document.getElementById('player-name').value), selectedColor, false);
  for (let i=0;i<CFG.BOT_COUNT;i++) {
    const usedColors=new Set(state.players.map(p=>p.color));
    const botColor=CFG.COLORS.find(c=>!usedColors.has(c))||CFG.COLORS[i%CFG.COLORS.length];
    createPlayer(BOT_NAMES[i]||`Bot-${i+1}`, botColor, true);
  }
  document.getElementById('start-screen').classList.add('hidden');
  state.gameRunning=true; state.lastTime=performance.now();
  requestAnimationFrame(gameLoop);
}

document.getElementById('play-btn').addEventListener('click', startGame);
document.getElementById('respawn-btn').addEventListener('click', () => {
  document.getElementById('death-screen').classList.add('hidden');
  respawnHuman();
  state.gameRunning=true; state.lastTime=performance.now();
  requestAnimationFrame(gameLoop);
});

window.addEventListener('playerKilled', e => {
  const { killer, victim, pct } = e.detail;
  if (killer) addKillFeed(killer.name, victim.name, killer.color, victim.color);
  if (!victim.isBot) {
    state.gameRunning = false;
    document.getElementById('death-killer').textContent    = killer ? killer.name : 'the boundary';
    document.getElementById('death-territory').textContent = pct + '%';
    document.getElementById('death-kills').textContent     = victim.kills;
    document.getElementById('death-screen').classList.remove('hidden');
  }
});

function gameLoop(ts) {
  if (!state.gameRunning) return;
  const dt=Math.min(ts-state.lastTime,100);
  state.lastTime=ts; state.tickAccum+=dt;
  while (state.tickAccum>=TICK_MS) {
    tickAllBots(TICK_MS); tickPlayers(TICK_MS);
    state.tickAccum-=TICK_MS;
  }
  updateCamera3d();
  updateCells(ts);
  syncPlayerMeshes(scene);
  render3d();
  renderLabels(scene, camera);
  drawHUD(ts);
  tickFillAnims(ts); tickKillFeed(ts);
  requestAnimationFrame(gameLoop);
}
