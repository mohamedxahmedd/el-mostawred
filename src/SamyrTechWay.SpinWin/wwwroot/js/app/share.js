// "Share Your Win" — renders a branded 1080×1350 PNG in the browser from same-origin assets only.
// Contains: logo, "I WON!", prize name/image/icon, tier, date and (opt-in) first name. Never phone, code or invoice.
import { PRIZE_ICONS } from './icons.js';
import { toast } from './util.js';

const W = 1080, H = 1350;
const LOGO = '/assets/brand/samyr-tech-way-logo.png';
const TIER_LABEL = { Standard: 'GIFT UNLOCKED', Rare: 'RARE WIN', Epic: 'EPIC WIN' };

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.decoding = 'async';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('image failed: ' + src));
    img.src = src;
  });
}

function seeded(seedText) {
  let h = 2166136261;
  for (const ch of seedText) h = Math.imul(h ^ ch.charCodeAt(0), 16777619);
  return () => ((h = Math.imul(h ^ (h >>> 15), 2246822507) ^ Math.imul(h ^ (h >>> 13), 3266489909)) >>> 0) / 4294967296;
}

function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
}

function wrapLines(ctx, text, maxWidth, maxLines) {
  const words = text.split(/\s+/);
  const lines = [];
  let line = '';
  for (const word of words) {
    const test = line ? `${line} ${word}` : word;
    if (ctx.measureText(test).width <= maxWidth || !line) line = test;
    else { lines.push(line); line = word; }
  }
  if (line) lines.push(line);
  if (lines.length > maxLines) {
    lines.length = maxLines;
    let last = lines[maxLines - 1];
    while (ctx.measureText(last + '…').width > maxWidth && last.length > 1) last = last.slice(0, -1);
    lines[maxLines - 1] = last + '…';
  }
  return lines;
}

/** @returns {Promise<Blob>} */
export async function renderShareImage({ name, tier = 'Standard', imageUrl, icon = 'gift', date, firstName, lang = 'ar', strings = {} }) {
  const arabic = lang === 'ar';
  const display = arabic ? 'Cairo' : 'Outfit';
  try {
    await Promise.all([
      document.fonts.load('900 150px Outfit', 'I WON!'),
      document.fonts.load(`800 64px ${display}`, name),
      document.fonts.load('700 40px Cairo', 'كسبت هدية'),
    ]);
  } catch { /* fall back to system fonts */ }

  const canvas = document.createElement('canvas');
  canvas.width = W; canvas.height = H;
  const ctx = canvas.getContext('2d');
  const rnd = seeded(`${name}|${date}|${tier}`);

  // Background: black with ember glow and gold light.
  ctx.fillStyle = '#07060a'; ctx.fillRect(0, 0, W, H);
  let g = ctx.createRadialGradient(540, 820, 40, 540, 820, 900);
  g.addColorStop(0, 'rgba(229,27,22,0.55)'); g.addColorStop(0.35, 'rgba(168,7,16,0.28)'); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
  g = ctx.createRadialGradient(540, 0, 10, 540, 0, 700);
  g.addColorStop(0, 'rgba(255,182,45,0.28)'); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);

  // Light beams from the medal (stronger for Rare/Epic).
  const beams = tier === 'Epic' ? 22 : tier === 'Rare' ? 16 : 10;
  ctx.save(); ctx.translate(540, 840); ctx.globalCompositeOperation = 'lighter';
  for (let i = 0; i < beams; i++) {
    const a = (i / beams) * Math.PI * 2 + rnd() * 0.1;
    const spread = 0.05 + rnd() * 0.05;
    const bg = ctx.createRadialGradient(0, 0, 120, 0, 0, 900);
    bg.addColorStop(0, `rgba(255,${tier === 'Epic' ? 150 : 210},${tier === 'Epic' ? 60 : 120},${tier === 'Standard' ? 0.07 : 0.12})`);
    bg.addColorStop(1, 'rgba(255,180,80,0)');
    ctx.fillStyle = bg;
    ctx.beginPath(); ctx.moveTo(0, 0); ctx.arc(0, 0, 900, a - spread, a + spread); ctx.closePath(); ctx.fill();
  }
  ctx.restore();

  // Embers.
  ctx.save(); ctx.globalCompositeOperation = 'lighter';
  for (let i = 0; i < 70; i++) {
    const x = rnd() * W, y = 420 + rnd() * 900, r = 1 + rnd() * 3.5;
    const eg = ctx.createRadialGradient(x, y, 0, x, y, r * 5);
    eg.addColorStop(0, `rgba(255,${150 + (rnd() * 80) | 0},60,${0.5 + rnd() * 0.5})`); eg.addColorStop(1, 'rgba(255,90,20,0)');
    ctx.fillStyle = eg; ctx.beginPath(); ctx.arc(x, y, r * 5, 0, Math.PI * 2); ctx.fill();
  }
  ctx.restore();

  // Frame.
  const gold = ctx.createLinearGradient(0, 0, W, H);
  gold.addColorStop(0, '#fff1c2'); gold.addColorStop(0.3, '#d9930f'); gold.addColorStop(0.55, '#ffe08a'); gold.addColorStop(0.8, '#a6650a'); gold.addColorStop(1, '#ffd27a');
  ctx.strokeStyle = gold; ctx.lineWidth = 4; roundRect(ctx, 34, 34, W - 68, H - 68, 44); ctx.stroke();
  ctx.strokeStyle = 'rgba(255,224,138,0.25)'; ctx.lineWidth = 1.5; roundRect(ctx, 50, 50, W - 100, H - 100, 34); ctx.stroke();

  // Logo on a porcelain plaque (its fine lettering is dark).
  const plaque = { x: 540 - 160, y: 92, s: 320 };
  ctx.save();
  ctx.shadowColor = 'rgba(229,27,22,0.55)'; ctx.shadowBlur = 50;
  const pg = ctx.createRadialGradient(540, plaque.y, 20, 540, plaque.y + 120, 380);
  pg.addColorStop(0, '#fffaf2'); pg.addColorStop(0.6, '#f8eedf'); pg.addColorStop(1, '#e9d6ba');
  ctx.fillStyle = pg; roundRect(ctx, plaque.x, plaque.y, plaque.s, plaque.s, 40); ctx.fill();
  ctx.restore();
  ctx.strokeStyle = 'rgba(255,214,140,0.9)'; ctx.lineWidth = 2.5; roundRect(ctx, plaque.x, plaque.y, plaque.s, plaque.s, 40); ctx.stroke();
  try {
    const logo = await loadImage(LOGO);
    ctx.drawImage(logo, plaque.x + 12, plaque.y + 12, plaque.s - 24, plaque.s - 24);
  } catch { /* logo missing: plaque stays, text below still brands the image */ }

  // Headline.
  ctx.textAlign = 'center'; ctx.textBaseline = 'alphabetic';
  ctx.font = '900 150px Outfit, Cairo, sans-serif';
  const tg = ctx.createLinearGradient(0, 470, 0, 600);
  tg.addColorStop(0, '#fff4cf'); tg.addColorStop(0.45, '#ffd27a'); tg.addColorStop(0.75, '#ff9f2e'); tg.addColorStop(1, '#e2551b');
  ctx.save(); ctx.shadowColor = 'rgba(255,117,24,0.65)'; ctx.shadowBlur = 40; ctx.fillStyle = tg;
  ctx.fillText(strings['share.iwon'] ?? 'I WON!', 540, 590); ctx.restore();

  ctx.direction = arabic ? 'rtl' : 'ltr';
  ctx.font = `700 40px ${arabic ? 'Cairo' : 'Outfit'}, sans-serif`;
  ctx.fillStyle = '#fff7ed';
  ctx.fillText(strings['share.iwon.local'] ?? '', 540, 650);
  ctx.direction = 'ltr';

  // Medal.
  const mx = 540, my = 850, mr = 160;
  ctx.save(); ctx.shadowColor = tier === 'Epic' ? 'rgba(255,117,24,0.95)' : 'rgba(255,182,45,0.7)'; ctx.shadowBlur = tier === 'Epic' ? 90 : 55;
  ctx.fillStyle = '#1a0b0e'; ctx.beginPath(); ctx.arc(mx, my, mr + 22, 0, Math.PI * 2); ctx.fill(); ctx.restore();
  const ring = ctx.createLinearGradient(mx - mr, my - mr, mx + mr, my + mr);
  ring.addColorStop(0, '#fff4cf'); ring.addColorStop(0.35, '#e2a12a'); ring.addColorStop(0.6, '#7d4806'); ring.addColorStop(0.85, '#ffd57a'); ring.addColorStop(1, '#a8650a');
  ctx.strokeStyle = ring; ctx.lineWidth = 18; ctx.beginPath(); ctx.arc(mx, my, mr + 10, 0, Math.PI * 2); ctx.stroke();
  const disc = ctx.createRadialGradient(mx, my - 60, 10, mx, my, mr);
  disc.addColorStop(0, '#4a1a16'); disc.addColorStop(1, '#150a0c');
  ctx.fillStyle = disc; ctx.beginPath(); ctx.arc(mx, my, mr, 0, Math.PI * 2); ctx.fill();

  let drewImage = false;
  if (imageUrl && imageUrl.startsWith('/')) {
    try {
      const img = await loadImage(imageUrl);
      const scale = Math.max((mr * 2) / img.width, (mr * 2) / img.height);
      const iw = img.width * scale, ih = img.height * scale;
      ctx.save(); ctx.beginPath(); ctx.arc(mx, my, mr - 2, 0, Math.PI * 2); ctx.clip();
      ctx.drawImage(img, mx - iw / 2, my - ih / 2, iw, ih); ctx.restore();
      drewImage = true;
    } catch { /* fall back to the icon */ }
  }
  if (!drewImage) {
    const path = new Path2D(PRIZE_ICONS[icon] ?? PRIZE_ICONS.gift);
    const s = 7.2;
    ctx.save(); ctx.translate(mx - 12 * s, my - 12 * s); ctx.scale(s, s);
    ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    ctx.shadowColor = 'rgba(255,117,24,0.8)'; ctx.shadowBlur = 24;
    ctx.strokeStyle = '#ffe08a'; ctx.lineWidth = 1.6; ctx.stroke(path);
    ctx.restore();
  }

  // Tier + prize name.
  ctx.font = '800 30px Outfit, sans-serif';
  if ('letterSpacing' in ctx) ctx.letterSpacing = '8px';
  ctx.fillStyle = tier === 'Epic' ? '#ffb39a' : '#ffd27a';
  ctx.fillText(TIER_LABEL[tier] ?? TIER_LABEL.Standard, 540, 1088);
  if ('letterSpacing' in ctx) ctx.letterSpacing = '0px';

  ctx.direction = arabic ? 'rtl' : 'ltr';
  ctx.font = `800 64px ${display}, sans-serif`;
  ctx.fillStyle = '#fff7ed';
  const lines = wrapLines(ctx, name, 900, 2);
  lines.forEach((line, i) => ctx.fillText(line, 540, 1160 + i * 72));
  let y = 1160 + (lines.length - 1) * 72;

  if (firstName) {
    ctx.font = `600 34px ${arabic ? 'Cairo' : 'Outfit'}, sans-serif`;
    ctx.fillStyle = 'rgba(255,240,222,0.78)';
    ctx.fillText((strings['share.wonby'] ?? '{0} won').replace('{0}', firstName), 540, y + 58);
  }
  ctx.direction = 'ltr';

  // Footer.
  ctx.font = '700 26px Outfit, sans-serif';
  if ('letterSpacing' in ctx) ctx.letterSpacing = '6px';
  ctx.fillStyle = '#ffd27a';
  ctx.fillText('SAMYR TECH WAY · SPIN & WIN', 540, H - 92);
  if ('letterSpacing' in ctx) ctx.letterSpacing = '0px';
  if (date) {
    ctx.direction = arabic ? 'rtl' : 'ltr';
    ctx.font = `500 24px ${arabic ? 'Cairo' : 'Outfit'}, sans-serif`;
    ctx.fillStyle = 'rgba(255,236,214,0.55)';
    ctx.fillText(date, 540, H - 58);
    ctx.direction = 'ltr';
  }

  return new Promise((resolve, reject) => canvas.toBlob(b => (b ? resolve(b) : reject(new Error('toBlob failed'))), 'image/png'));
}

/** Wires the #share-dialog: preview, first-name toggle, Web Share (files), download and clipboard copy. */
export class ShareDialog {
  constructor(dialog, { strings, lang }) {
    this.dialog = dialog;
    this.strings = strings;
    this.lang = lang;
    this.firstName = dialog.dataset.firstName || '';
    this.preview = dialog.querySelector('#share-preview');
    this.nameToggle = dialog.querySelector('#share-name');
    this.btnNative = dialog.querySelector('#share-native');
    this.btnDownload = dialog.querySelector('#share-download');
    this.btnCopy = dialog.querySelector('#share-copy');
    this.blob = null;
    this.url = null;
    this.award = null;

    dialog.querySelectorAll('[data-close]').forEach(b => b.addEventListener('click', () => dialog.close()));
    dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });
    dialog.addEventListener('close', () => this.release());
    this.nameToggle?.addEventListener('change', () => this.render());
    this.btnDownload.addEventListener('click', () => this.download());
    this.btnNative.addEventListener('click', () => this.share());
    this.btnCopy.addEventListener('click', () => this.copy());
    this.btnCopy.hidden = !(navigator.clipboard?.write && window.ClipboardItem);
  }

  open(award) {
    this.award = award;
    if (this.nameToggle) this.nameToggle.checked = false;
    if (!this.dialog.open) this.dialog.showModal();
    this.render();
  }

  release() {
    if (this.url) URL.revokeObjectURL(this.url);
    this.url = null; this.blob = null;
  }

  setBusy(busy) {
    [this.btnNative, this.btnDownload, this.btnCopy].forEach(b => { b.disabled = busy; });
    if (busy) this.preview.innerHTML = `<div class="cta-hint"><span class="spinner" aria-hidden="true"></span> ${this.strings['share.generating'] ?? ''}</div>`;
  }

  async render() {
    this.setBusy(true);
    try {
      this.release();
      this.blob = await renderShareImage({
        ...this.award,
        firstName: this.nameToggle?.checked ? this.firstName : null,
        lang: this.lang,
        strings: this.strings,
      });
      this.url = URL.createObjectURL(this.blob);
      const img = new Image();
      img.alt = this.strings['share.title'] ?? '';
      img.src = this.url;
      this.preview.replaceChildren(img);
      const file = this.file();
      this.btnNative.hidden = !(navigator.canShare && navigator.canShare({ files: [file] }));
      this.setBusy(false);
    } catch {
      this.preview.textContent = this.strings['share.failed'] ?? 'Error';
      toast(this.strings['share.failed'] ?? 'Error', 'error');
      this.btnDownload.disabled = true;
    }
  }

  file() { return new File([this.blob], 'samyr-tech-way-win.png', { type: 'image/png' }); }

  download() {
    if (!this.url) return;
    const a = document.createElement('a');
    a.href = this.url; a.download = 'samyr-tech-way-win.png';
    document.body.append(a); a.click(); a.remove();
    toast(this.strings['share.saved'] ?? 'Saved');
  }

  async share() {
    try {
      await navigator.share({
        files: [this.file()],
        title: 'SAMYR TECH WAY',
        text: (this.strings['share.text'] ?? '{0}').replace('{0}', this.award.name),
      });
    } catch (e) {
      if (e?.name !== 'AbortError') this.download();
    }
  }

  async copy() {
    try {
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': this.blob })]);
      toast(this.strings['share.copied'] ?? 'Copied');
    } catch {
      this.download();
    }
  }
}
