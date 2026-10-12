// Admin shell behaviours. Every action is still authorised and validated on the server.
import { toast } from '../app/util.js';

// Server toasts (TempData) → toast UI.
const host = document.getElementById('toasts');
if (host?.dataset.toast) toast(host.dataset.toast, host.dataset.toastKind || 'success', 5200);

// Mobile sidebar.
const sidebar = document.getElementById('sidebar');
const scrim = document.getElementById('scrim');
const menuBtn = document.getElementById('menu-btn');
const setMenu = open => {
  sidebar.classList.toggle('is-open', open);
  scrim.hidden = !open;
  menuBtn?.setAttribute('aria-expanded', String(open));
  if (open) sidebar.querySelector('a')?.focus();
};
menuBtn?.addEventListener('click', () => setMenu(!sidebar.classList.contains('is-open')));
scrim?.addEventListener('click', () => setMenu(false));
document.addEventListener('keydown', e => { if (e.key === 'Escape' && sidebar?.classList.contains('is-open')) setMenu(false); });

// Branded confirmation for destructive / significant actions (no window.confirm).
const dialog = document.getElementById('confirm-dialog');
let pending = null;
document.addEventListener('submit', e => {
  const form = e.target;
  if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === '1') return;
  e.preventDefault();
  pending = form;
  document.getElementById('confirm-text').textContent = form.dataset.confirm;
  dialog.showModal();
  document.getElementById('confirm-ok').focus();
}, true);
document.getElementById('confirm-ok')?.addEventListener('click', () => {
  dialog.close();
  if (!pending) return;
  pending.dataset.confirmed = '1';
  pending.requestSubmit ? pending.requestSubmit() : pending.submit();
});
document.getElementById('confirm-cancel')?.addEventListener('click', () => { pending = null; dialog.close(); });

// Prevent double submits on slow connections.
document.addEventListener('submit', e => {
  const form = e.target;
  if (e.defaultPrevented || !form.matches?.('[data-busy-form], [data-confirm]')) return;
  if (form.dataset.submitting === '1') { e.preventDefault(); return; }
  form.dataset.submitting = '1';
  form.querySelectorAll('button[type="submit"]').forEach(b => b.setAttribute('aria-disabled', 'true'));
  document.querySelectorAll(`button[form="${form.id}"]`).forEach(b => b.setAttribute('aria-disabled', 'true'));
});
addEventListener('pageshow', () => document.querySelectorAll('form[data-submitting]').forEach(f => {
  delete f.dataset.submitting; delete f.dataset.confirmed;
  f.querySelectorAll('[aria-disabled]').forEach(b => b.removeAttribute('aria-disabled'));
}));

// Copy access code.
document.querySelectorAll('[data-copy]').forEach(btn => btn.addEventListener('click', async () => {
  const text = document.querySelector(btn.dataset.copy)?.textContent.trim() ?? '';
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    const range = document.createRange();
    range.selectNodeContents(document.querySelector(btn.dataset.copy));
    const sel = getSelection(); sel.removeAllRanges(); sel.addRange(range);
    document.execCommand?.('copy');
  }
  toast('تم النسخ');
}));
document.querySelectorAll('[data-print]').forEach(btn => btn.addEventListener('click', () => print()));

// Image preview for uploads.
const file = document.querySelector('input[type="file"][name="Image"]');
const preview = document.getElementById('image-preview');
file?.addEventListener('change', () => {
  const f = file.files?.[0];
  if (!f) return;
  if (f.size > 2 * 1024 * 1024) toast('يجب ألا يزيد حجم الصورة عن 2 ميجابايت.', 'error');
  const img = new Image();
  img.alt = 'معاينة الصورة المختارة';
  img.src = URL.createObjectURL(f);
  preview.replaceChildren(img);
});

// Chart tooltip (hover + keyboard focus); values are also in the table view.
const chart = document.getElementById('activity-chart');
if (chart) {
  const tip = document.getElementById('chart-tip');
  const show = el => {
    tip.replaceChildren();
    const b = document.createElement('b'); b.textContent = `${el.dataset.spins} لفّة`;
    const w = document.createElement('span'); w.textContent = `${el.dataset.wins} فوز · ${el.dataset.spins - el.dataset.wins} حاول مرة أخرى`;
    const d = document.createElement('span'); d.textContent = el.dataset.label;
    tip.append(b, w, d);
    const r = el.getBoundingClientRect(), c = chart.getBoundingClientRect();
    tip.style.left = `${r.left - c.left + r.width / 2}px`;
    tip.style.top = `${Math.max(40, r.top - c.top + 30)}px`;
    tip.hidden = false;
  };
  chart.querySelectorAll('.hit').forEach(el => {
    el.addEventListener('pointerenter', () => show(el));
    el.addEventListener('focus', () => show(el));
    el.addEventListener('pointerleave', () => { tip.hidden = true; });
    el.addEventListener('blur', () => { tip.hidden = true; });
  });
}
