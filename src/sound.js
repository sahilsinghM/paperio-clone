// ─── Synth sound effects via Web Audio API ───────────────────────────────────
// All sounds are generated procedurally — no audio files needed.

let _ctx = null;

function ctx() {
  if (!_ctx) _ctx = new (window.AudioContext || window.webkitAudioContext)();
  // Resume if browser suspended it (autoplay policy)
  if (_ctx.state === 'suspended') _ctx.resume();
  return _ctx;
}

function osc(type, freq, gainVal, startTime, duration, freqEnd) {
  const ac   = ctx();
  const o    = ac.createOscillator();
  const g    = ac.createGain();
  o.connect(g);
  g.connect(ac.destination);
  o.type = type;
  o.frequency.setValueAtTime(freq, startTime);
  if (freqEnd !== undefined)
    o.frequency.exponentialRampToValueAtTime(freqEnd, startTime + duration);
  g.gain.setValueAtTime(gainVal, startTime);
  g.gain.exponentialRampToValueAtTime(0.001, startTime + duration);
  o.start(startTime);
  o.stop(startTime + duration + 0.01);
}

/** Rising chime — played when territory is captured */
export function playCapture() {
  try {
    const t = ctx().currentTime;
    osc('sine', 440, 0.25, t,        0.12, 660);
    osc('sine', 660, 0.15, t + 0.10, 0.14, 900);
  } catch (_) {}
}

/** Descending crash — played when the human player dies */
export function playDeath() {
  try {
    const t = ctx().currentTime;
    osc('sawtooth', 380, 0.35, t,       0.45, 60);
    osc('square',   200, 0.20, t + 0.05, 0.30, 40);
  } catch (_) {}
}

/** Double-ding — played when human player kills an enemy */
export function playKill() {
  try {
    const t = ctx().currentTime;
    osc('sine', 700, 0.28, t,        0.18);
    osc('sine', 950, 0.22, t + 0.14, 0.22);
  } catch (_) {}
}

/** Soft blip — played when the trail first starts leaving home */
export function playTrailStart() {
  try {
    const t = ctx().currentTime;
    osc('sine', 300, 0.12, t, 0.09, 240);
  } catch (_) {}
}
