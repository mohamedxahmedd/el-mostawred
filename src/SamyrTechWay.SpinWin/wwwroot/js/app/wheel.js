// SVG prize wheel: rotating disc (wedges, icons/photos, labels) + static metallic frame (ring, LEDs, gloss).
// The server decides the item; this only animates to it. Stop math lives in wheel-math.js (unit tested).
import { layoutSegments, targetRotation, segmentIndexAt, motionPlan, turnsFor } from './wheel-math.js';
import { PRIZE_ICONS } from './icons.js';
import { PRIZE_ART } from './prize-art.js';

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
const R_BURST = 330; // centre of the sunburst on gold wedges, where their text sits
const WEDGE_STYLE = { gold: 'sun', amber: 'sun', onyx: 'night' };
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const rad = d => (d * Math.PI) / 180;
const pt = (r, deg) => [r * Math.sin(rad(deg)), -r * Math.cos(rad(deg))];
const xy = ([x, y]) => `${x.toFixed(2)} ${y.toFixed(2)}`;

/** A wedge pointing straight up, `half` degrees either side of 12 o'clock. */
const wedgePath = half => `M0 0L${xy(pt(R_DISC + 2, -half))}A${R_DISC + 2} ${R_DISC + 2} 0 ${half > 90 ? 1 : 0} 1 ${xy(pt(R_DISC + 2, half))}Z`;

/** Light rays bursting from behind the text of a gold wedge. */
const sunRays = () => Array.from({ length: 18 }, (_, i) => {
  const a = i * 20;
  const [x1, y1] = pt(620, a - 4.5);
  const [x2, y2] = pt(620, a + 4.5);
  return `<path d="M0 ${-R_BURST}L${(x1).toFixed(1)} ${(y1 - R_BURST).toFixed(1)}L${(x2).toFixed(1)} ${(y2 - R_BURST).toFixed(1)}Z" fill="rgba(255,255,255,.16)"/>`;
}).join('');

/** Faint circuit-board traces on black wedges, echoing the store's branding. */
const circuitTraces = half => {
  const w = Math.sin(rad(Math.min(half, 60)));
  return [-1, 1].map(side => {
    const x = side * w * 300;
    return `<path d="M${(x * 1.3).toFixed(1)} -430V-390L${(x * 0.8).toFixed(1)} -340V-300" fill="none" stroke="rgba(255,140,30,.2)" stroke-width="3"/>
      <circle cx="${(x * 0.8).toFixed(1)}" cy="-296" r="5" fill="none" stroke="rgba(255,140,30,.25)" stroke-width="2.5"/>`;
  }).join('');
};

/** One line, or two balanced lines split at a space when the name is long ("خصم 200 ج" → "خصم" / "200 ج"). */
export function splitLabel(name, maxOneLine) {
  const clean = String(name ?? '').trim().replace(/\s+/g, ' ');
  const cut = t => (t.length > 16 ? t.slice(0, 15) + '…' : t);
  if (clean.length <= maxOneLine || !clean.includes(' ')) return [cut(clean)];
  const mid = clean.length / 2;
  let best = -1;
  for (let i = 0; i < clean.length; i++) if (clean[i] === ' ' && (best < 0 || Math.abs(i - mid) < Math.abs(best - mid))) best = i;
  return [cut(clean.slice(0, best)), cut(clean.slice(best + 1))];
}

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

  /**
   * Booth style (matches the printed store wheel): gold/amber items are yellow sunburst wedges with big dark text,
   * onyx items are black wedges with white text and a picture, ruby/flame/ember keep their own colour.
   * Each wedge is drawn upright in its own rotated group, so gradients and rays are shared.
   */
  renderDisc() {
    const p = this.prefix;
    const defs = [];
    const wedges = [];
    const seps = [];
    const labels = [];
    this.artUsed = new Set();

    defs.push(`<radialGradient id="${p}-sun" cx="0" cy="${-R_BURST}" r="330" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#fff58a"/><stop offset=".3" stop-color="#ffd526"/><stop offset=".72" stop-color="#ffa400"/><stop offset="1" stop-color="#ef7300"/></radialGradient>`);
    defs.push(`<radialGradient id="${p}-night" cx="0" cy="0" r="${R_DISC}" gradientUnits="userSpaceOnUse">
      <stop offset=".25" stop-color="#040305"/><stop offset=".75" stop-color="#141015"/><stop offset="1" stop-color="#231b21"/></radialGradient>`);
    Object.entries(WEDGE_COLORS).forEach(([key, c]) => {
      defs.push(`<radialGradient id="${p}-g-${key}" cx="0" cy="0" r="${R_DISC}" gradientUnits="userSpaceOnUse">
        <stop offset="0.18" stop-color="${c.stops[0]}"/><stop offset="0.5" stop-color="${c.stops[1]}"/>
        <stop offset="0.86" stop-color="${c.stops[2]}"/><stop offset="1" stop-color="${c.stops[3]}"/></radialGradient>`);
    });

    this.segments.forEach((s, k) => {
      const item = this.items[k];
      const color = WEDGE_COLORS[item.color] ? item.color : 'ruby';
      const style = WEDGE_STYLE[color] ?? 'color';
      const half = s.span / 2;
      const shape = s.span >= 359.999 ? `<circle r="${R_DISC}"/>` : `<path d="${wedgePath(half)}"/>`;
      const fill = style === 'sun' ? `url(#${p}-sun)` : style === 'night' ? `url(#${p}-night)` : `url(#${p}-g-${color})`;
      defs.push(`<clipPath id="${p}-w${k}">${shape}</clipPath>`);
      wedges.push(`<g transform="rotate(${s.mid.toFixed(3)})"><g clip-path="url(#${p}-w${k})">
        <rect x="-${R_DISC}" y="-${R_DISC}" width="${R_DISC * 2}" height="${R_DISC * 2}" fill="${fill}"/>
        ${style === 'sun' ? sunRays() : style === 'night' ? circuitTraces(half) : ''}</g></g>`);
      if (s.span < 359.999) {
        const [sx, sy] = pt(R_DISC, s.start);
        const [ix, iy] = pt(R_HUB, s.start);
        seps.push(`<line x1="${ix.toFixed(2)}" y1="${iy.toFixed(2)}" x2="${sx.toFixed(2)}" y2="${sy.toFixed(2)}"/>`);
      }
      labels.push(this.label(s, item, style, WEDGE_COLORS[color], k));
    });
    this.artUsed.forEach(key => defs.push(PRIZE_ART[key].defs.replaceAll('ID-', `${p}-a-`)));

    this.els.disc.innerHTML = `<svg viewBox="-500 -500 1000 1000" role="presentation">
      <defs>${defs.join('')}</defs>
      <circle r="${R_DISC + 4}" fill="#050405"/>
      <g>${wedges.join('')}</g>
      <g stroke="#050405" stroke-width="6" stroke-linecap="round">${seps.join('')}</g>
      <g>${labels.join('')}</g>
      <circle r="${R_HUB + 16}" fill="#070608" stroke="#ff8a00" stroke-width="7"/>
      <circle r="${R_HUB + 4}" fill="none" stroke="rgba(255,170,60,.35)" stroke-width="2"/>
    </svg>`;
  }

  label(s, item, style, c, k) {
    if (s.span < 3) return '';
    const half = Math.min(s.span / 2, 90);
    const chord = r => 2 * r * Math.sin(rad(half)); // usable width across the wedge at radius r
    const sun = style === 'sun';
    const ink = sun ? '#1c1004' : style === 'night' ? '#ffffff' : c.text;
    const halo = sun ? 'rgba(255,245,200,.45)' : 'rgba(0,0,0,.7)';

    // A gold wedge without a picture (e.g. "خصم 200 ج") gets big text only, like the printed wheel. Any item with a
    // photo or illustration shows it, whatever its colour, with the name near the rim.
    const art = !item.imageUrl ? PRIZE_ART[item.icon] : null;
    const pictured = !!item.imageUrl || !!art;
    const big = sun && !pictured;

    const lines = splitLabel(item.name, big ? 6 : 10);
    const longest = Math.max(3, ...lines.map(l => l.length));
    const rim = big ? 402 : 412;
    const fitAt = r => (chord(r) * 0.86) / (longest * 0.5);
    let fs = Math.min(big ? 66 : 48, fitAt(rim - (lines.length > 1 ? 120 : 60)));
    const radii = lines.map((_, i) => rim - fs * 0.6 - i * fs * 1.08);
    fs = Math.min(fs, fitAt(radii[radii.length - 1] - fs * 0.3));
    const rtl = /[\u0600-\u06FF]/.test(item.name) ? ' direction="rtl" unicode-bidi="embed"' : '';

    let text = '';
    if (fs >= 13) {
      text = lines.map((line, i) => `<text class="wheel-seg-label" x="0" y="${(-radii[i]).toFixed(1)}" text-anchor="middle" dominant-baseline="central"${rtl}
        font-size="${fs.toFixed(1)}" font-weight="${sun ? 900 : 800}" fill="${ink}" stroke="${halo}" stroke-width="${(fs * (sun ? 0.05 : 0.12)).toFixed(1)}">${esc(line)}</text>`).join('');
    }

    let icon = '';
    const textInner = radii[radii.length - 1] - fs * 0.7;
    const iconR = Math.max(R_HUB + 70, Math.min(258, textInner - 76));
    const iconSize = Math.min(art ? 170 : 118, chord(iconR) * (art ? 1.15 : 0.72), (textInner - R_HUB - 22) * (art ? 1.15 : 0.9));
    if (!big && iconSize >= 18) {
      if (item.imageUrl) {
        const rr = iconSize / 2;
        icon = `<clipPath id="${this.prefix}-clip-${k}"><circle cy="${-iconR}" r="${rr}"/></clipPath>
          <circle cy="${-iconR}" r="${rr + 4}" fill="#0b090c" stroke="#ff8a00" stroke-width="3"/>
          <image href="${esc(item.imageUrl)}" x="${-rr}" y="${-iconR - rr}" width="${rr * 2}" height="${rr * 2}" preserveAspectRatio="xMidYMid slice" clip-path="url(#${this.prefix}-clip-${k})"/>`;
      } else if (art) {
        this.artUsed.add(item.icon);
        const sc = iconSize / 200;
        icon = `<g transform="translate(${(-iconSize / 2).toFixed(1)} ${(-iconR - iconSize / 2).toFixed(1)}) scale(${sc.toFixed(4)})">${art.body.replaceAll('ID-', `${this.prefix}-a-`)}</g>`;
      } else {
        const path = PRIZE_ICONS[item.icon] ?? PRIZE_ICONS.gift;
        const sc = iconSize / 24;
        icon = `<g transform="translate(0 ${-iconR}) scale(${sc.toFixed(3)}) translate(-12 -12)" fill="none" stroke-linecap="round" stroke-linejoin="round">
          <path d="${path}" stroke="rgba(0,0,0,.65)" stroke-width="4.5"/><path d="${path}" stroke="#ffae1f" stroke-width="2.2"/></g>`;
      }
    }
    return `<g transform="rotate(${s.mid.toFixed(3)})">${text}${icon}</g>`;
  }

  renderFrame() {
    const p = this.prefix;
    const bulbs = 32;
    const leds = Array.from({ length: bulbs }, (_, i) => {
      const [x, y] = pt(469, (360 / bulbs) * i + 360 / bulbs / 2);
      return `<g class="led ${i % 2 ? 'led-b' : 'led-a'}"><circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="15" fill="url(#${p}-ledglow)"/>
        <circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="6.5" fill="url(#${p}-led)"/></g>`;
    }).join('');
    this.els.frame.innerHTML = `
      <defs>
        <radialGradient id="${p}-led" cx=".4" cy=".35" r=".7"><stop offset="0" stop-color="#fffbe0"/><stop offset=".45" stop-color="#ffd24a"/><stop offset="1" stop-color="#ff8a00"/></radialGradient>
        <radialGradient id="${p}-ledglow"><stop offset="0" stop-color="rgba(255,190,60,.8)"/><stop offset="1" stop-color="rgba(255,120,0,0)"/></radialGradient>
        <radialGradient id="${p}-gloss" cx="-.15" cy="-.55" r="1.1"><stop offset="0" stop-color="rgba(255,255,255,.12)"/><stop offset=".5" stop-color="rgba(255,255,255,.02)"/><stop offset="1" stop-color="rgba(255,255,255,0)"/></radialGradient>
      </defs>
      <circle r="${R_DISC - 2}" fill="url(#${p}-gloss)"/>
      <circle r="469" fill="none" stroke="#060506" stroke-width="46"/>
      <circle r="448" fill="none" stroke="#ff8a00" stroke-width="5"/>
      <circle r="451" fill="none" stroke="rgba(255,138,0,.35)" stroke-width="12"/>
      <circle r="491" fill="none" stroke="#1d1410" stroke-width="4"/>
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
