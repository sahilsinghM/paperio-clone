import { CFG } from './config.js';
import { state, territory, getTerritoryCount, hexToRgb, playerColor } from './state.js';

export function addKillFeed(killerName, victimName, killerColor, victimColor) {
  const el = document.createElement('div');
  el.className = 'kill-entry';
  el.style.borderLeftColor = killerColor;
  const ks = document.createElement('span');
  ks.style.color = killerColor; ks.textContent = killerName;
  const vs = document.createElement('span');
  vs.style.color = victimColor; vs.textContent = victimName;
  el.appendChild(ks);
  el.appendChild(document.createTextNode(' eliminated '));
  el.appendChild(vs);
  document.getElementById('kill-feed').appendChild(el);
  state.killFeed.push({el, time: performance.now()});
}

export function tickKillFeed(now) {
  for (let i = state.killFeed.length-1; i >= 0; i--) {
    const age = now - state.killFeed[i].time;
    if (age > CFG.KILL_FEED_MS) { state.killFeed[i].el.remove(); state.killFeed.splice(i,1); }
    else if (age > CFG.KILL_FEED_MS-500)
      state.killFeed[i].el.style.opacity = String((CFG.KILL_FEED_MS-age)/500);
  }
}

export function tickFillAnims(now) {
  for (let i = state.fillAnims.length-1; i >= 0; i--)
    if (now - state.fillAnims[i].startTime > CFG.FILL_ANIM_MS) state.fillAnims.splice(i,1);
}

export function initColorPicker(onColorSelect) {
  let selected = CFG.COLORS[0];
  const el = document.getElementById('color-picker');
  CFG.COLORS.forEach((c, i) => {
    const swatch = document.createElement('div');
    swatch.className = 'color-swatch' + (i===0 ? ' selected' : '');
    swatch.style.background = c;
    swatch.addEventListener('click', () => {
      document.querySelectorAll('.color-swatch').forEach(s => s.classList.remove('selected'));
      swatch.classList.add('selected');
      selected = c;
      onColorSelect(c);
    });
    el.appendChild(swatch);
  });
  return () => selected;
}

let hudCanvas, hudCtx;

export function initHUD() {
  hudCanvas = document.createElement('canvas');
  hudCanvas.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:10;';
  document.body.appendChild(hudCanvas);
  hudCtx = hudCanvas.getContext('2d');
  window.addEventListener('resize', resizeHUD);
  resizeHUD();
}

function resizeHUD() {
  hudCanvas.width  = window.innerWidth;
  hudCanvas.height = window.innerHeight;
}

export function drawHUD(now) {
  if (!hudCtx || !state.humanPlayer) return;
  hudCtx.clearRect(0, 0, hudCanvas.width, hudCanvas.height);
  drawLeaderboard();
  drawScore();
  drawMinimap();
}

function drawLeaderboard() {
  const total  = territory.length;
  const ranked = [...state.players]
    .filter(p => p.alive)
    .map(p => ({ name:p.name, color:p.color, pct:(getTerritoryCount(p.id)/total*100).toFixed(1) }))
    .sort((a,b) => b.pct - a.pct).slice(0,5);
  const lx=16,ly=80,lw=190,lh=28+ranked.length*26;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.beginPath(); hudCtx.roundRect(lx,ly,lw,lh,8); hudCtx.fill();
  hudCtx.fillStyle='#999'; hudCtx.font='bold 10px sans-serif';
  hudCtx.fillText('LEADERBOARD',lx+10,ly+18);
  ranked.forEach((e,i)=>{
    const ey=ly+28+i*26;
    hudCtx.fillStyle=e.color; hudCtx.fillRect(lx+10,ey,10,10);
    hudCtx.fillStyle='#fff'; hudCtx.font='13px sans-serif';
    hudCtx.fillText(e.name,lx+26,ey+10);
    hudCtx.fillStyle='#aaa'; hudCtx.font='12px sans-serif';
    hudCtx.fillText(e.pct+'%',lx+lw-42,ey+10);
  });
}

function drawScore() {
  const h   = state.humanPlayer;
  if (!h || !h.alive) return;
  const pct = (getTerritoryCount(h.id)/territory.length*100).toFixed(1);
  const text= `${pct}%  |  Kills: ${h.kills}`;
  hudCtx.font='bold 16px sans-serif';
  const tw=hudCtx.measureText(text).width;
  const bx=(hudCanvas.width-tw)/2-16, by=hudCanvas.height-48;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.beginPath(); hudCtx.roundRect(bx,by,tw+32,32,16); hudCtx.fill();
  hudCtx.fillStyle='#fff';
  hudCtx.fillText(text,bx+16,by+21);
}

const minimapOffscreen = document.createElement('canvas');
minimapOffscreen.width  = CFG.GRID_W;
minimapOffscreen.height = CFG.GRID_H;
const mmCtx         = minimapOffscreen.getContext('2d');
const mmImgData     = mmCtx.createImageData(CFG.GRID_W, CFG.GRID_H);

function drawMinimap() {
  if (!hudCtx) return;
  const d = mmImgData.data;
  for (let i = 0; i < territory.length; i++) {
    const t = territory[i], pi = i * 4;
    if (t !== 0) {
      const [r,g,b] = hexToRgb(playerColor(t));
      d[pi]=r*0.55|0; d[pi+1]=g*0.55|0; d[pi+2]=b*0.55|0; d[pi+3]=255;
    } else {
      d[pi]=22; d[pi+1]=33; d[pi+2]=62; d[pi+3]=255;
    }
  }
  // Draw trail dots per player
  for (const p of state.players) {
    if (!p.alive) continue;
    const [r,g,b] = hexToRgb(p.color);
    for (const pt of p.trailPoints) {
      const gx = Math.floor(pt.x), gy = Math.floor(pt.y);
      if (gx < 0 || gx >= CFG.GRID_W || gy < 0 || gy >= CFG.GRID_H) continue;
      const pi = (gy * CFG.GRID_W + gx) * 4;
      d[pi]=r; d[pi+1]=g; d[pi+2]=b; d[pi+3]=255;
    }
  }
  mmCtx.putImageData(mmImgData,0,0);
  const mx=hudCanvas.width-CFG.MINIMAP_SIZE-CFG.MINIMAP_PAD;
  const my=CFG.MINIMAP_PAD;
  hudCtx.fillStyle='rgba(0,0,0,0.55)';
  hudCtx.fillRect(mx-2,my-2,CFG.MINIMAP_SIZE+4,CFG.MINIMAP_SIZE+4);
  hudCtx.drawImage(minimapOffscreen,mx,my,CFG.MINIMAP_SIZE,CFG.MINIMAP_SIZE);
  for (const p of state.players) {
    if (!p.alive) continue;
    const px=mx+(p.x/CFG.GRID_W)*CFG.MINIMAP_SIZE;
    const py=my+(p.y/CFG.GRID_H)*CFG.MINIMAP_SIZE;
    hudCtx.beginPath(); hudCtx.arc(px,py,p.isBot?2:3.5,0,Math.PI*2);
    hudCtx.fillStyle=p.isBot?p.color:'#fff'; hudCtx.fill();
  }
}
