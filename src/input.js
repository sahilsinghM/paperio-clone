import { state } from './state.js';

const held = new Set();

const TURN_LEFT  = new Set(['ArrowLeft','a','A']);
const TURN_RIGHT = new Set(['ArrowRight','d','D']);

// Max knob displacement from joystick center (px)
const JOYSTICK_RADIUS = 35;
// Dead zone: ignore tiny movements
const JOYSTICK_DEAD = 10;

let joystickActive = false;
let joystickCenter = null;

export function applyTurnInput() {
  const h = state.humanPlayer;
  if (!h || !h.alive) return;
  // Joystick sets direction directly; only apply keyboard turning when joystick is idle
  if (joystickActive) return;
  let t = 0;
  for (const k of held) {
    if (TURN_LEFT.has(k))  { t = -1; break; }
    if (TURN_RIGHT.has(k)) { t =  1; break; }
  }
  h.turnInput = t;
}

export function initInput(canvas) {
  document.addEventListener('keydown', e => {
    if ([...TURN_LEFT, ...TURN_RIGHT].includes(e.key)) {
      e.preventDefault();
      held.add(e.key);
    }
  });
  document.addEventListener('keyup', e => held.delete(e.key));

  // Touch swipe on canvas → set player direction angle
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

  // Virtual joystick
  const joystick = document.getElementById('joystick');
  const knob = document.getElementById('joystick-knob');

  function updateJoystick(clientX, clientY) {
    const dx = clientX - joystickCenter.x;
    const dy = clientY - joystickCenter.y;
    const dist = Math.hypot(dx, dy);
    const clamped = Math.min(dist, JOYSTICK_RADIUS);
    const angle = Math.atan2(dy, dx);

    // Move knob visually (clamped inside ring)
    const kx = Math.cos(angle) * clamped;
    const ky = Math.sin(angle) * clamped;
    knob.style.transform = `translate(${kx}px, ${ky}px)`;

    const h = state.humanPlayer;
    if (!h || !h.alive) return;

    if (dist > JOYSTICK_DEAD) {
      // X axis of joystick → turn left/right (camera-independent, intuitive)
      // Normalise to [-1, 1] across the full radius
      const nx = dx / JOYSTICK_RADIUS;
      h.turnInput = nx > 0.25 ? 1 : nx < -0.25 ? -1 : 0;
    } else {
      h.turnInput = 0;
    }
  }

  function resetKnob() {
    joystickActive = false;
    joystickCenter = null;
    knob.style.transform = 'translate(0px, 0px)';
    const h = state.humanPlayer;
    if (h) h.turnInput = 0;
  }

  function startJoystick(clientX, clientY) {
    const rect = joystick.getBoundingClientRect();
    joystickCenter = {
      x: rect.left + rect.width  / 2,
      y: rect.top  + rect.height / 2,
    };
    joystickActive = true;
    updateJoystick(clientX, clientY);
  }

  // Touch events
  joystick.addEventListener('touchstart', e => {
    e.preventDefault();
    startJoystick(e.touches[0].clientX, e.touches[0].clientY);
  }, { passive: false });

  joystick.addEventListener('touchmove', e => {
    e.preventDefault();
    if (!joystickActive) return;
    updateJoystick(e.touches[0].clientX, e.touches[0].clientY);
  }, { passive: false });

  joystick.addEventListener('touchend',    resetKnob, { passive: true });
  joystick.addEventListener('touchcancel', resetKnob, { passive: true });

  // Mouse events (desktop)
  joystick.addEventListener('mousedown', e => {
    startJoystick(e.clientX, e.clientY);
  });

  document.addEventListener('mousemove', e => {
    if (!joystickActive) return;
    updateJoystick(e.clientX, e.clientY);
  });

  document.addEventListener('mouseup', resetKnob);
}
