// Celebration particles on a single canvas. Particle count is capped and halved on low-power devices;
// reduced-motion users get a single soft glow ring instead of particles.
import { prefersReducedMotion } from './util.js';

const PALETTES = {
  flame: ['#a80710', '#e51b16', '#ff7518', '#ffb62d', '#ffe08a', '#fff7ed'],
  gold: ['#ffe08a', '#ffd27a', '#ffb62d', '#fff1c2', '#e0960f'],
  fire: ['#ff3d14', '#ff7518', '#ffb62d', '#ffe08a'],
};
const TAU = Math.PI * 2;
const rand = (a, b) => a + Math.random() * (b - a);
const pick = arr => arr[(Math.random() * arr.length) | 0];

export class Celebration {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.parts = [];
    this.rings = [];
    this.flash = 0;
    this.raf = 0;
    this.last = 0;
    this.lowPower = (navigator.hardwareConcurrency ?? 4) <= 4 || matchMedia('(max-width: 600px)').matches;
    this.max = this.lowPower ? 180 : 340;
    this.dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.onResize = () => this.resize();
    addEventListener('resize', this.onResize, { passive: true });
  }

  resize() {
    const w = this.canvas.clientWidth || innerWidth, h = this.canvas.clientHeight || innerHeight;
    this.w = w; this.h = h;
    this.canvas.width = Math.round(w * this.dpr); this.canvas.height = Math.round(h * this.dpr);
    this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
  }

  scale(n) { return Math.round(n * (this.intensity ?? 1) * (this.lowPower ? 0.55 : 1)); }

  add(p) { if (this.parts.length < this.max) this.parts.push(p); }

  confetti(x, y, n, palette = PALETTES.flame, spread = TAU, angle = -Math.PI / 2, speed = [380, 820]) {
    for (let i = 0; i < this.scale(n); i++) {
      const a = angle + rand(-spread / 2, spread / 2), v = rand(...speed);
      this.add({ kind: 'confetti', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, g: 900, drag: 1.6,
        w: rand(6, 11), h: rand(10, 18), rot: rand(0, TAU), vr: rand(-12, 12), flip: rand(0, TAU), vf: rand(6, 14),
        color: pick(palette), life: rand(2.2, 3.4), age: 0 });
    }
  }

  sparks(x, y, n, palette = PALETTES.gold, spread = TAU, angle = 0, speed = [300, 900], g = 260) {
    for (let i = 0; i < this.scale(n); i++) {
      const a = angle + rand(-spread / 2, spread / 2), v = rand(...speed);
      this.add({ kind: 'spark', x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, g, drag: 2.2,
        len: rand(10, 26), color: pick(palette), life: rand(0.7, 1.4), age: 0, width: rand(1.4, 2.8) });
    }
  }

  embers(x, y, n, spreadX = 220) {
    for (let i = 0; i < this.scale(n); i++) {
      this.add({ kind: 'ember', x: x + rand(-spreadX, spreadX), y: y + rand(-30, 60), vx: rand(-30, 30), vy: rand(-220, -90), g: -40, drag: 0.6,
        r: rand(1.5, 3.6), color: pick(PALETTES.fire), life: rand(1.4, 2.6), age: 0 });
    }
  }

  ring(x, y, { delay = 0, color = 'rgba(255,210,120,', max = 320, life = 1.1, width = 6 } = {}) {
    this.rings.push({ x, y, delay, color, max, life, width, age: 0 });
  }

  /** Tiered celebration. Origin defaults to the medal position. */
  celebrate(tier = 'Standard', origin) {
    this.resize();
    const x = origin?.x ?? this.w / 2, y = origin?.y ?? this.h * 0.36;
    if (prefersReducedMotion()) { this.ring(x, y, { max: 220, life: 1.2 }); return this.start(); }

    if (tier === 'Epic') {
      this.flash = 1;
      // Phoenix wings: two sweeping fire arcs, then a gold fountain and rising embers.
      this.sparks(x, y, 70, PALETTES.fire, 0.9, -Math.PI * 0.8, [600, 1250], 380);
      this.sparks(x, y, 70, PALETTES.fire, 0.9, -Math.PI * 0.2, [600, 1250], 380);
      this.sparks(x, y, 50, PALETTES.gold, 0.6, -Math.PI / 2, [700, 1300], 420);
      this.embers(x, y + 40, 70, Math.min(320, this.w * 0.4));
      this.confetti(x, y, 120, PALETTES.flame, Math.PI * 1.2);
      [0, 0.18, 0.4].forEach((d, i) => this.ring(x, y, { delay: d, max: 360 + i * 140, width: 8 - i * 2, color: i ? 'rgba(255,117,24,' : 'rgba(255,240,200,' }));
    } else if (tier === 'Rare') {
      this.sparks(x, y, 70, PALETTES.gold, TAU, 0, [320, 900]);
      this.confetti(x, y, 90, PALETTES.gold.concat(PALETTES.flame.slice(1, 4)), Math.PI);
      this.ring(x, y, { max: 340 });
      this.ring(x, y, { delay: 0.2, max: 420, width: 3, color: 'rgba(255,117,24,' });
    } else {
      this.confetti(x, y, 60, PALETTES.flame, Math.PI * 0.9, -Math.PI / 2, [320, 700]);
      this.ring(x, y, { max: 260, width: 4 });
    }
    return this.start();
  }

  retry(origin) {
    this.resize();
    const x = origin?.x ?? this.w / 2, y = origin?.y ?? this.h * 0.36;
    if (prefersReducedMotion()) { this.ring(x, y, { max: 160, life: 0.9, color: 'rgba(255,182,45,' }); return this.start(); }
    this.sparks(x, y, 26, ['#ffb62d', '#ff7518', '#ffe08a'], TAU, 0, [180, 420], 500);
    this.ring(x, y, { max: 200, width: 4, color: 'rgba(255,182,45,' });
    return this.start();
  }

  start() {
    if (!this.raf) { this.last = performance.now(); this.raf = requestAnimationFrame(t => this.frame(t)); }
  }

  stop() {
    cancelAnimationFrame(this.raf); this.raf = 0;
    this.parts = []; this.rings = []; this.flash = 0;
    this.ctx.clearRect(0, 0, this.w || 0, this.h || 0);
  }

  frame(t) {
    const dt = Math.min(0.033, (t - this.last) / 1000);
    this.last = t;
    const { ctx } = this;
    ctx.clearRect(0, 0, this.w, this.h);

    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255, 190, 110, ${this.flash * 0.35})`;
      ctx.fillRect(0, 0, this.w, this.h);
      this.flash = Math.max(0, this.flash - dt * 2.2);
    }

    ctx.globalCompositeOperation = 'lighter';
    for (const r of this.rings) {
      r.age += dt;
      const p = (r.age - r.delay) / r.life;
      if (p <= 0 || p >= 1) continue;
      const e = 1 - Math.pow(1 - p, 3);
      ctx.strokeStyle = `${r.color}${(1 - p) * 0.8})`;
      ctx.lineWidth = r.width * (1 - p) + 0.5;
      ctx.beginPath(); ctx.arc(r.x, r.y, 30 + e * r.max, 0, TAU); ctx.stroke();
    }
    this.rings = this.rings.filter(r => r.age - r.delay < r.life);

    for (const p of this.parts) {
      p.age += dt;
      const k = Math.exp(-p.drag * dt);
      p.vx *= k; p.vy = p.vy * k + p.g * dt;
      p.x += p.vx * dt; p.y += p.vy * dt;
      const fade = Math.max(0, 1 - p.age / p.life);
      if (p.kind === 'spark') {
        const sp = Math.hypot(p.vx, p.vy) || 1;
        ctx.strokeStyle = p.color; ctx.globalAlpha = fade; ctx.lineWidth = p.width; ctx.lineCap = 'round';
        ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x - (p.vx / sp) * p.len, p.y - (p.vy / sp) * p.len); ctx.stroke();
      } else if (p.kind === 'ember') {
        const g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, p.r * 4);
        g.addColorStop(0, p.color); g.addColorStop(1, 'rgba(255,80,20,0)');
        ctx.globalAlpha = fade; ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r * 4, 0, TAU); ctx.fill();
      }
    }
    ctx.globalCompositeOperation = 'source-over';
    for (const p of this.parts) {
      if (p.kind !== 'confetti') continue;
      p.rot += p.vr * dt; p.flip += p.vf * dt;
      const fade = Math.min(1, (p.life - p.age) * 2);
      ctx.save();
      ctx.globalAlpha = Math.max(0, fade);
      ctx.translate(p.x, p.y); ctx.rotate(p.rot); ctx.scale(1, Math.cos(p.flip));
      ctx.fillStyle = p.color; ctx.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
    this.parts = this.parts.filter(p => p.age < p.life && p.y < this.h + 60);

    if (this.parts.length || this.rings.length || this.flash > 0) this.raf = requestAnimationFrame(tt => this.frame(tt));
    else { this.raf = 0; ctx.clearRect(0, 0, this.w, this.h); }
  }
}
