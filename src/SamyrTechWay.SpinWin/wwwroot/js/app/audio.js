// Synthesised game audio (Web Audio API) — no audio files, no rights issues, tiny payload.
// The AudioContext is created only inside a user gesture (autoplay policies); nothing plays on page load.
import { store } from './util.js';

const KEY = 'stw.sound';

export class GameAudio {
  constructor(defaultOn = true) {
    const saved = store.get(KEY);
    this.enabled = saved === null ? defaultOn : saved === 'on';
    this.ctx = null;
    this.master = null;
    this.noise = null;
    this.lastTick = 0;
  }

  setEnabled(on) {
    this.enabled = on;
    store.set(KEY, on ? 'on' : 'off');
    if (this.master && this.ctx) this.master.gain.setTargetAtTime(on ? 0.32 : 0, this.ctx.currentTime, 0.03);
  }

  /** Call from a click/tap handler before the first sound. */
  unlock() {
    if (!this.ctx) {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      this.ctx = new AC();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.enabled ? 0.32 : 0;
      const comp = this.ctx.createDynamicsCompressor();
      comp.threshold.value = -16; comp.ratio.value = 4;
      this.master.connect(comp).connect(this.ctx.destination);
      const len = this.ctx.sampleRate;
      this.noise = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
      const data = this.noise.getChannelData(0);
      for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    }
    if (this.ctx.state === 'suspended') this.ctx.resume().catch(() => {});
  }

  get ready() { return this.enabled && this.ctx && this.ctx.state === 'running'; }

  env(gainNode, t, attack, peak, decay) {
    const g = gainNode.gain;
    g.cancelScheduledValues(t);
    g.setValueAtTime(0.0001, t);
    g.exponentialRampToValueAtTime(peak, t + attack);
    g.exponentialRampToValueAtTime(0.0001, t + attack + decay);
  }

  tone(freq, { type = 'sine', at = 0, attack = 0.005, decay = 0.4, gain = 0.3, detune = 0, dest } = {}) {
    const t = this.ctx.currentTime + at;
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = type; o.frequency.setValueAtTime(freq, t); o.detune.value = detune;
    this.env(g, t, attack, gain, decay);
    o.connect(g).connect(dest ?? this.master);
    o.start(t); o.stop(t + attack + decay + 0.05);
    return o;
  }

  /** Bell-like chime made from inharmonic partials. */
  bell(freq, at = 0, gain = 0.22, decay = 1.4) {
    [[1, 1], [2.76, 0.45], [5.4, 0.22], [8.93, 0.1]].forEach(([mult, amp]) =>
      this.tone(freq * mult, { at, gain: gain * amp, decay: decay / Math.sqrt(mult), attack: 0.003 }));
  }

  noiseBurst({ at = 0, duration = 0.5, from = 400, to = 2400, q = 1.2, gain = 0.25, type = 'bandpass' } = {}) {
    const t = this.ctx.currentTime + at;
    const src = this.ctx.createBufferSource();
    src.buffer = this.noise; src.loop = true;
    const f = this.ctx.createBiquadFilter();
    f.type = type; f.Q.value = q;
    f.frequency.setValueAtTime(from, t);
    f.frequency.exponentialRampToValueAtTime(to, t + duration * 0.6);
    f.frequency.exponentialRampToValueAtTime(Math.max(80, from * 0.8), t + duration);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(gain, t + duration * 0.25);
    g.gain.exponentialRampToValueAtTime(0.0001, t + duration);
    src.connect(f).connect(g).connect(this.master);
    src.start(t); src.stop(t + duration + 0.05);
  }

  // ---- Game cues ------------------------------------------------------------------------------

  click() { if (this.ready) this.tone(880, { type: 'triangle', gain: 0.12, decay: 0.06 }); }

  whoosh() { if (this.ready) this.noiseBurst({ duration: 0.9, from: 300, to: 2600, gain: 0.28 }); }

  /** Peg tick; pitch rises slightly as the wheel slows. Rate-limited so fast spins stay pleasant. */
  tick(speed01 = 0.5) {
    if (!this.ready) return;
    const now = this.ctx.currentTime;
    if (now - this.lastTick < 0.035) return;
    this.lastTick = now;
    const f = 1500 + (1 - speed01) * 900 + Math.random() * 120;
    this.tone(f, { type: 'square', gain: 0.05 + (1 - speed01) * 0.05, decay: 0.03, attack: 0.001 });
    this.noiseBurst({ duration: 0.03, from: 3000, to: 5000, q: 6, gain: 0.05 });
  }

  /** Tension riser before the stop. */
  riser(seconds = 1.6) {
    if (!this.ready) return;
    const t = this.ctx.currentTime;
    const o = this.ctx.createOscillator();
    const lfo = this.ctx.createOscillator();
    const lfoGain = this.ctx.createGain();
    const f = this.ctx.createBiquadFilter();
    const g = this.ctx.createGain();
    o.type = 'sawtooth';
    o.frequency.setValueAtTime(180, t);
    o.frequency.exponentialRampToValueAtTime(520, t + seconds);
    lfo.frequency.value = 7; lfoGain.gain.value = 8;
    lfo.connect(lfoGain).connect(o.frequency);
    f.type = 'lowpass'; f.frequency.setValueAtTime(500, t); f.frequency.exponentialRampToValueAtTime(2600, t + seconds);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.06, t + seconds * 0.85);
    g.gain.exponentialRampToValueAtTime(0.0001, t + seconds + 0.15);
    o.connect(f).connect(g).connect(this.master);
    o.start(t); lfo.start(t); o.stop(t + seconds + 0.2); lfo.stop(t + seconds + 0.2);
  }

  /** Win fanfare scaled by celebration tier. */
  win(tier = 'Standard') {
    if (!this.ready) return;
    const C6 = 1046.5, E6 = 1318.5, G6 = 1568, C7 = 2093;
    [C6, E6, G6, C7].forEach((f, i) => this.bell(f, i * 0.09, 0.2, 1.6));
    if (tier === 'Rare' || tier === 'Epic') {
      for (let i = 0; i < 10; i++) this.tone(2400 + Math.random() * 2400, { at: 0.35 + i * 0.07, gain: 0.05, decay: 0.25, type: 'sine' });
      [E6, G6, C7].forEach((f, i) => this.bell(f * 1.5, 0.45 + i * 0.08, 0.12, 1.2));
    }
    if (tier === 'Epic') {
      // Low boom + warm brass chord + rising shimmer.
      const t = this.ctx.currentTime;
      const boom = this.ctx.createOscillator();
      const bg = this.ctx.createGain();
      boom.type = 'sine'; boom.frequency.setValueAtTime(120, t); boom.frequency.exponentialRampToValueAtTime(38, t + 0.9);
      this.env(bg, t, 0.01, 0.5, 1.1);
      boom.connect(bg).connect(this.master); boom.start(t); boom.stop(t + 1.2);
      const lp = this.ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 1800; lp.connect(this.master);
      [261.6, 329.6, 392, 523.3].forEach(f => [-7, 7].forEach(d =>
        this.tone(f, { type: 'sawtooth', at: 0.12, attack: 0.08, gain: 0.035, decay: 1.8, detune: d, dest: lp })));
      this.noiseBurst({ at: 0.1, duration: 1.4, from: 800, to: 6000, gain: 0.12, type: 'highpass', q: 0.7 });
    }
  }

  /** Playful "so close" boing for Try Again. */
  retry() {
    if (!this.ready) return;
    const t = this.ctx.currentTime;
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = 'triangle';
    o.frequency.setValueAtTime(660, t);
    o.frequency.exponentialRampToValueAtTime(330, t + 0.18);
    o.frequency.exponentialRampToValueAtTime(880, t + 0.42);
    this.env(g, t, 0.01, 0.22, 0.5);
    o.connect(g).connect(this.master); o.start(t); o.stop(t + 0.6);
    this.bell(784, 0.45, 0.14, 0.7);
    this.bell(1046.5, 0.58, 0.14, 0.9);
  }
}
