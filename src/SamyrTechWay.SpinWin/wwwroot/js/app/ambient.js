// Ambient ember motes behind the page. Capped, paused when hidden, disabled for reduced motion.
import { prefersReducedMotion } from './util.js';

const canvas = document.getElementById('ambient');
if (canvas && !prefersReducedMotion()) {
  const ctx = canvas.getContext('2d', { alpha: true });
  const lowPower = (navigator.hardwareConcurrency ?? 4) <= 4 || matchMedia('(max-width: 600px)').matches;
  const count = lowPower ? 18 : 34;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  let w = 0, h = 0, raf = 0, last = 0;
  const motes = [];

  const resize = () => {
    w = canvas.clientWidth; h = canvas.clientHeight;
    canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  };
  const spawn = (initial) => ({
    x: Math.random() * w,
    y: initial ? Math.random() * h : h + 10,
    r: 0.6 + Math.random() * 1.8,
    vy: 6 + Math.random() * 16,
    sway: Math.random() * Math.PI * 2,
    hue: Math.random() < 0.6 ? 28 + Math.random() * 18 : 8 + Math.random() * 10,
    life: 0.35 + Math.random() * 0.55,
  });

  const frame = (t) => {
    const dt = Math.min(0.05, (t - last) / 1000 || 0.016);
    last = t;
    ctx.clearRect(0, 0, w, h);
    ctx.globalCompositeOperation = 'lighter';
    for (const m of motes) {
      m.y -= m.vy * dt;
      m.sway += dt * 0.8;
      m.x += Math.sin(m.sway) * 0.25;
      if (m.y < -12) Object.assign(m, spawn(false));
      const fade = Math.min(1, m.y / (h * 0.35)) * m.life;
      const g = ctx.createRadialGradient(m.x, m.y, 0, m.x, m.y, m.r * 5);
      g.addColorStop(0, `hsla(${m.hue}, 100%, 70%, ${0.9 * fade})`);
      g.addColorStop(1, `hsla(${m.hue}, 100%, 50%, 0)`);
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(m.x, m.y, m.r * 5, 0, Math.PI * 2); ctx.fill();
    }
    raf = requestAnimationFrame(frame);
  };

  resize();
  for (let i = 0; i < count; i++) motes.push(spawn(true));
  raf = requestAnimationFrame(frame);
  addEventListener('resize', resize, { passive: true });
  document.addEventListener('visibilitychange', () => {
    cancelAnimationFrame(raf);
    if (!document.hidden) { last = performance.now(); raf = requestAnimationFrame(frame); }
  });
}
