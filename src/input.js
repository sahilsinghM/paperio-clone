import { state } from './state.js';

const DIR_MAP = {
  ArrowUp:    {dx:0,dy:-1}, arrowup:    {dx:0,dy:-1},
  ArrowDown:  {dx:0,dy:1},  arrowdown:  {dx:0,dy:1},
  ArrowLeft:  {dx:-1,dy:0}, arrowleft:  {dx:-1,dy:0},
  ArrowRight: {dx:1,dy:0},  arrowright: {dx:1,dy:0},
  w:{dx:0,dy:-1}, s:{dx:0,dy:1}, a:{dx:-1,dy:0}, d:{dx:1,dy:0},
};

function queueDirection(dir) {
  const h = state.humanPlayer;
  if (!h || !h.alive || !state.gameRunning) return;
  if (dir.dx===-h.dx && dir.dy===-h.dy) return;
  const last = h.inputQueue[h.inputQueue.length-1];
  if (last && last.dx===dir.dx && last.dy===dir.dy) return;
  h.inputQueue.push({...dir});
}

export function initInput(canvas) {
  document.addEventListener('keydown', e => {
    const dir = DIR_MAP[e.key] || DIR_MAP[e.key.toLowerCase()];
    if (!dir) return;
    e.preventDefault();
    queueDirection(dir);
  });

  let touchStart = null;
  canvas.addEventListener('touchstart', e => {
    touchStart = {x:e.touches[0].clientX, y:e.touches[0].clientY};
  }, {passive:true});
  canvas.addEventListener('touchend', e => {
    if (!touchStart) return;
    const dx = e.changedTouches[0].clientX - touchStart.x;
    const dy = e.changedTouches[0].clientY - touchStart.y;
    touchStart = null;
    if (Math.abs(dx)<15 && Math.abs(dy)<15) return;
    const dir = Math.abs(dx)>Math.abs(dy)
      ? (dx>0 ? DIR_MAP.ArrowRight : DIR_MAP.ArrowLeft)
      : (dy>0 ? DIR_MAP.ArrowDown  : DIR_MAP.ArrowUp);
    queueDirection(dir);
  }, {passive:true});

  const DPAD_DIRS = {up:DIR_MAP.ArrowUp,down:DIR_MAP.ArrowDown,left:DIR_MAP.ArrowLeft,right:DIR_MAP.ArrowRight};
  document.querySelectorAll('.dpad-btn').forEach(btn => {
    btn.addEventListener('click', () => queueDirection(DPAD_DIRS[btn.dataset.dir]));
  });
}
