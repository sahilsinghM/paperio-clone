import { state } from './state.js';

const held = new Set();

const TURN_LEFT  = new Set(['ArrowLeft','a','A']);
const TURN_RIGHT = new Set(['ArrowRight','d','D']);

export function applyTurnInput() {
  const h = state.humanPlayer;
  if (!h || !h.alive) return;
  let t = 0;
  for (const k of held) {
    if (TURN_LEFT.has(k))  { t = -1; break; }
    if (TURN_RIGHT.has(k)) { t =  1; break; }
  }
  h.turnInput = t;
}

function vibrate(ms) {
  if (navigator.vibrate) navigator.vibrate(ms);
}

export function initInput(canvas) {
  document.addEventListener('keydown', e => {
    if ([...TURN_LEFT, ...TURN_RIGHT].includes(e.key)) {
      e.preventDefault();
      held.add(e.key);
    }
  });
  document.addEventListener('keyup', e => held.delete(e.key));

  // Touch swipe on canvas → snap to swiped direction
  let touchStart = null;
  canvas.addEventListener('touchstart', e => {
    touchStart = { x: e.touches[0].clientX, y: e.touches[0].clientY };
  }, { passive: true });
  canvas.addEventListener('touchend', e => {
    if (!touchStart) return;
    const dx = e.changedTouches[0].clientX - touchStart.x;
    const dy = e.changedTouches[0].clientY - touchStart.y;
    touchStart = null;
    if (Math.hypot(dx, dy) < 15) return;
    const h = state.humanPlayer;
    if (h && h.alive) h.direction = Math.atan2(dy, dx);
  }, { passive: true });

  // Turn buttons: hold = continuous turn, haptic on press
  const leftBtn  = document.getElementById('turn-left-btn');
  const rightBtn = document.getElementById('turn-right-btn');

  function bindTurnBtn(btn, key) {
    if (!btn) return;
    btn.addEventListener('pointerdown', e => {
      e.preventDefault();
      held.add(key);
      vibrate(18);
    });
    btn.addEventListener('pointerup',    () => held.delete(key));
    btn.addEventListener('pointerleave', () => held.delete(key));
    btn.addEventListener('pointercancel',() => held.delete(key));
  }

  bindTurnBtn(leftBtn,  'ArrowLeft');
  bindTurnBtn(rightBtn, 'ArrowRight');
}
