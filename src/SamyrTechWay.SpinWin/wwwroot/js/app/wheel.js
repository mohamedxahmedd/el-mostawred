// SVG prize wheel: rotating disc (wedges, icons/photos, labels) + static metallic frame (ring, LEDs, gloss).
// The server decides the item; this only animates to it. Stop math lives in wheel-math.js (unit tested).
import { layoutSegments, targetRotation, segmentIndexAt, motionPlan, turnsFor } from './wheel-math.js';
import { PRIZE_ICONS } from './icons.js';

export const WEDGE_COLORS = {
  ruby: { stops: ['#2e0003', '#6e0208', '#a80710', '#d01518'], text: '#fff7ed', stroke: 'rgba(60,0,0,.55)' },
  flame: { stops: ['#3d0302', '#9a0c09', '#e51b16', '#ff5221'], text: '#fff7ed', stroke: 'rgba(70,0,0,.55)' },
  ember: { stops: ['#4a1402', '#b03806', '#ff7518', '#ff9d45'], text: '#fff7ed', stroke: 'rgba(90,25,0,.6)' },
  amber: { stops: ['#4a2700', '#a96000', '#ffb62d', '#ffd685'], text: '#2a1204', stroke: 'rgba(255,240,200,.35)' },
  gold: { stops: ['#3f2902', '#9c6a0d', '#f5c55a', '#fff0bf'], text: '#2a1204', stroke: 'rgba(255,250,230,.4)' },
  onyx: { stops: ['#020203', '#0f0c11', '#221b23', '#372c35'], text: '#ffe08a', stroke: 'rgba(0,0,0,.6)' },
};

const R_DISC = 446;
const R_HUB = 122;
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const rad = d => (d * Math.PI) / 180;
const pt = (r, deg) => [r * Math.sin(rad(deg)), -r * Math.cos(rad(deg))];

export class Wheel {
  /**
   * @param {{disc:HTMLElement, frame?:SVGSVGElement, pointer?:HTMLElement, root?:HTMLElement}} els
   */
  constructor(els, { idPrefix = 'w' } = {}) {
    this.els = els;
    this.prefix = idPrefix;
    this.items = [];
    this.segments = [];
    this.rotation = 0;
    this.spinning = false;
    if (els.frame) this.renderFrame();
  }

  setItems(items) {
    this.items = items;
    this.segments = layoutSegments(items);
    this.renderDisc();
  }

  hasItem(id) { return this.segments.some(s => s.id === id); }

  sameLayout(items) {
    return items.length === this.items.length && items.every((it, i) => it.id === this.items[i].id);
  }

  setRotation(r) {
    this.rotation = r;
    this.els.disc.style.transform = `rotate(${r}deg)`;
  }

  renderDisc() {
    const p = this.prefix;
    const defs = [];
    const wedges = [];
    const seps = [];
    const labels = [];

    Object.entries(WEDGE_COLORS).forEach(([key, c]) => {
      defs.push(`<radialGradient id="${p}-g-${key}" cx="0" cy="0" r="${R_DISC}" gradientUnits="userSpaceOnUse">
        <stop offset="0.18" stop-color="${c.stops[0]}"/><stop offset="0.5" stop-color="${c.stops[1]}"/>
        <stop offset="0.86" stop-color="${c.stops[2]}"/><stop offset="1" stop-color="${c.stops[3]}"/></radialGradient>`);
    });
    defs.push(`<linearGradient id="${p}-sep" x1="0" y1="0" x2="0" y2="-1" gradientUnits="objectBoundingBox">
      <stop offset="0" stop-color="#8a540a"/><stop offset=".5" stop-color="#ffe08a"/><stop offset="1" stop-color="#fff4cf"/></linearGradient>`);
    defs.push(`<radialGradient id="${p}-hub" cx="0" cy="-30" r="150" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#3b2a1a"/><stop offset="1" stop-color="#0d0a0c"/></radialGradient>`);
    defs.push(`<linearGradient id="${p}-hubring" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#fff1c2"/><stop offset=".45" stop-color="#d9930f"/><stop offset="1" stop-color="#6b3f04"/></linearGradient>`);

    this.segments.forEach((s, k) => {
      const item = this.items[k];
      const color = WEDGE_COLORS[item.color] ? item.color : 'ruby';
      const c = WEDGE_COLORS[color];
      if (s.span >= 359.999) {
        wedges.push(`<circle r="${R_DISC}" fill="url(#${p}-g-${color})"/>`);
      } else {
        const [x1, y1] = pt(R_DISC, s.start);
        const [x2, y2] = pt(R_DISC, s.end);
        wedges.push(`<path d="M0 0L${x1.toFixed(2)} ${y1.toFixed(2)}A${R_DISC} ${R_DISC} 0 ${s.span > 180 ? 1 : 0} 1 ${x2.toFixed(2)} ${y2.toFixed(2)}Z" fill="url(#${p}-g-${color})"/>`);
        const [sx, sy] = pt(R_DISC, s.start);
        const [ix, iy] = pt(R_HUB - 8, s.start);
        seps.push(`<line x1="${ix.toFixed(2)}" y1="${iy.toFixed(2)}" x2="${sx.toFixed(2)}" y2="${sy.toFixed(2)}"/>`);
      }
      labels.push(this.label(s, item, c, k));
    });

    // Decorative hub studs (rotate with the disc).
    const studs = Array.from({ length: 12 }, (_, i) => {
      const [x, y] = pt(R_HUB + 2, i * 30);
      return `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="5" fill="#ffe08a" stroke="#6b3f04" stroke-width="1.5"/>`;
    }).join('');

    this.els.disc.innerHTML = `<svg viewBox="-500 -500 1000 1000" role="presentation">
      <defs>${defs.join('')}</defs>
      <circle r="${R_DISC + 4}" fill="#120a0a"/>
      <g>${wedges.join('')}</g>
      <circle r="${R_DISC}" fill="none" stroke="rgba(255,255,255,.06)" stroke-width="60" />
      <g stroke="url(#${p}-sep)" stroke-width="4" stroke-linecap="round">${seps.join('')}</g>
      <g>${labels.join('')}</g>
      <circle r="${R_HUB + 18}" fill="url(#${p}-hub)" stroke="url(#${p}-hubring)" stroke-width="10"/>
      ${studs}
    </svg>`;
  }

  label(s, item, c, k) {
    const arcAt = r => (r * rad(s.span));
    const iconR = 352;
    const iconSize = Math.max(0, Math.min(84, arcAt(iconR) * 0.6));
    if (s.span < 3) return '';

    let icon = '';
    if (iconSize >= 18) {
      if (item.imageUrl) {
        const rr = iconSize / 2 + 4;
        icon = `<clipPath id="${this.prefix}-clip-${k}"><circle cy="${-iconR}" r="${rr}"/></clipPath>
          <circle cy="${-iconR}" r="${rr + 5}" fill="#1a0b0e" stroke="#ffe08a" stroke-width="4"/>
          <image href="${esc(item.imageUrl)}" x="${-rr}" y="${-iconR - rr}" width="${rr * 2}" height="${rr * 2}" preserveAspectRatio="xMidYMid slice" clip-path="url(#${this.prefix}-clip-${k})"/>`;
      } else {
        const path = PRIZE_ICONS[item.icon] ?? PRIZE_ICONS.gift;
        const sc = iconSize / 24;
        icon = `<g transform="translate(0 ${-iconR}) scale(${sc.toFixed(3)}) translate(-12 -12)" fill="none" stroke="${c.text}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="${path}" stroke="${c.stroke}" stroke-width="4.5"/><path d="${path}"/></g>`;
      }
    }

    let text = '';
    if (s.span >= 15) {
      const name = item.name.length > 18 ? item.name.slice(0, 17) + '…' : item.name;
      const room = 380 - (iconSize ? iconSize / 2 + 26 : 0) - (R_HUB + 34); // radial space for text
      const fs = Math.min(36, room / Math.max(4, name.length * 0.56), arcAt(250) * 0.5);
      if (fs >= 17) {
        const r = R_HUB + 34 + room / 2;
        // Keep labels upright: wedges on the left half read the other way round.
        const turn = s.mid > 180 ? 90 : -90;
        text = `<text class="wheel-seg-label" x="0" y="0" transform="translate(0 ${-r}) rotate(${turn})" text-anchor="middle" dominant-baseline="central"
          font-size="${fs.toFixed(1)}" fill="${c.text}" stroke="${c.stroke}" stroke-width="${(fs * 0.14).toFixed(1)}">${esc(name)}</text>`;
      }
    }
    return `<g transform="rotate(${s.mid.toFixed(3)})">${icon}${text}</g>`;
  }

  renderFrame() {
    const p = this.prefix;
    const bulbs = 28;
    const leds = Array.from({ length: bulbs }, (_, i) => {
      const [x, y] = pt(468, (360 / bulbs) * i + 360 / bulbs / 2);
      return `<g class="led ${i % 2 ? 'led-b' : 'led-a'}"><circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="17" fill="url(#${p}-ledglow)"/>
        <circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="7.5" fill="url(#${p}-led)" stroke="#7a4a05" stroke-width="1.5"/></g>`;
    }).join('');
    this.els.frame.innerHTML = `
      <defs>
        <linearGradient id="${p}-ring" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#fff4cf"/><stop offset=".18" stop-color="#e2a12a"/><stop offset=".38" stop-color="#7d4806"/>
          <stop offset=".55" stop-color="#ffd57a"/><stop offset=".72" stop-color="#a8650a"/><stop offset=".9" stop-color="#ffe9a8"/><stop offset="1" stop-color="#8a540a"/>
        </linearGradient>
        <radialGradient id="${p}-led" cx=".4" cy=".35" r=".7"><stop offset="0" stop-color="#ffffff"/><stop offset=".45" stop-color="#fff0b0"/><stop offset="1" stop-color="#ff9d2e"/></radialGradient>
        <radialGradient id="${p}-ledglow"><stop offset="0" stop-color="rgba(255,214,120,.75)"/><stop offset="1" stop-color="rgba(255,140,40,0)"/></radialGradient>
        <radialGradient id="${p}-gloss" cx="-.15" cy="-.55" r="1.1"><stop offset="0" stop-color="rgba(255,255,255,.16)"/><stop offset=".5" stop-color="rgba(255,255,255,.03)"/><stop offset="1" stop-color="rgba(255,255,255,0)"/></radialGradient>
      </defs>
      <circle r="${R_DISC}" fill="none" stroke="rgba(0,0,0,.55)" stroke-width="14"/>
      <circle r="${R_DISC - 2}" fill="url(#${p}-gloss)"/>
      <circle r="468" fill="none" stroke="#1b0e06" stroke-width="48"/>
      <circle r="468" fill="none" stroke="url(#${p}-ring)" stroke-width="40"/>
      <circle r="449" fill="none" stroke="#4a2a04" stroke-width="3"/>
      <circle r="489" fill="none" stroke="rgba(255,240,200,.65)" stroke-width="2.5"/>
      ${leds}`;
  }

  /**
   * Animate to the server-chosen item. Resolves when the pointer rests inside that wedge.
   * Hooks: onTick(speed01) when a separator passes, onNearEnd() ~1.5 s before the stop.
   */
  spinTo(itemId, { durationMs = 6000, reducedMotion = false, onTick, onNearEnd } = {}) {
    const seg = this.segments.find(s => s.id === itemId);
    if (!seg) return Promise.reject(new Error('Item not on wheel'));
    const gsap = window.gsap;
    this.spinning = true;
    this.els.root?.classList.add('is-spinning');

    return new Promise(resolve => {
      const state = { r: this.rotation };
      let lastIdx = segmentIndexAt(state.r, this.segments);
      let lastR = state.r, lastT = performance.now();
      let peak = 1;
      const update = () => {
        this.setRotation(state.r);
        const now = performance.now();
        const v = Math.abs(state.r - lastR) / Math.max(1, now - lastT) * 1000;
        lastR = state.r; lastT = now;
        const idx = segmentIndexAt(state.r, this.segments);
        if (idx !== lastIdx) { lastIdx = idx; onTick?.(Math.min(1, v / peak)); }
      };
      const done = () => {
        this.spinning = false;
        this.els.root?.classList.remove('is-spinning');
        this.setRotation(state.r % 360);
        resolve(seg);
      };

      if (reducedMotion || !gsap) {
        const final = targetRotation(this.rotation, seg, 1, Math.random());
        if (!gsap) { state.r = final; update(); done(); return; }
        gsap.to(state, { r: final, duration: 0.9, ease: 'power2.out', onUpdate: update, onComplete: done });
        return;
      }

      const seconds = durationMs / 1000;
      const windUp = 9;
      const start = this.rotation - windUp;
      const final = targetRotation(start, seg, turnsFor(seconds), Math.random());
      const plan = motionPlan(final - start, Math.max(1.5, seconds - 0.3));
      peak = plan.peakVelocity;

      const tl = gsap.timeline({ onComplete: done });
      tl.to(state, { r: start, duration: 0.3, ease: 'power2.out', onUpdate: update })
        .to(state, { r: start + plan.d1, duration: plan.t1, ease: 'power2.in', onUpdate: update })
        .to(state, { r: final, duration: plan.t2, ease: 'power4.out', onUpdate: update });
      if (onNearEnd) tl.call(onNearEnd, [], Math.max(0.3 + plan.t1, tl.duration() - Math.min(1.8, plan.t2 * 0.4)));
    });
  }
}
