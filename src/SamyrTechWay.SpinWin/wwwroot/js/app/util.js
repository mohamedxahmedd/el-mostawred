// Small shared helpers for the customer site.

export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

export const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

/** RFC 4122 v4 UUID. crypto.randomUUID needs a secure context, so fall back to getRandomValues (works on HTTP). */
export function uuid() {
  if (window.crypto?.randomUUID) {
    try { return crypto.randomUUID(); } catch { /* insecure context */ }
  }
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = [...b].map(x => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

/** Storage that never throws (private mode, blocked storage). */
export const store = {
  get(key, area = localStorage) { try { return area.getItem(key); } catch { return null; } },
  set(key, value, area = localStorage) { try { area.setItem(key, value); } catch { /* ignore */ } },
  remove(key, area = localStorage) { try { area.removeItem(key); } catch { /* ignore */ } },
};

export const csrfToken = () => document.querySelector('meta[name="csrf-token"]')?.content ?? '';

/** fetch JSON with antiforgery header and a timeout. Resolves { status, body } or throws on network failure. */
export async function api(url, { method = 'GET', body, timeoutMs = 20000 } = {}) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(url, {
      method,
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json', ...(body ? { 'Content-Type': 'application/json' } : {}), ...(method !== 'GET' ? { 'X-CSRF-TOKEN': csrfToken() } : {}) },
      body: body ? JSON.stringify(body) : undefined,
      signal: ctrl.signal,
    });
    let data = null;
    try { data = await res.json(); } catch { /* empty body */ }
    return { status: res.status, body: data };
  } finally {
    clearTimeout(timer);
  }
}

export function readJson(id) {
  const el = document.getElementById(id);
  try { return el ? JSON.parse(el.textContent) : {}; } catch { return {}; }
}

/** Accessible toast notifications. */
export function toast(message, kind = 'success', ms = 4200) {
  const host = document.getElementById('toasts');
  if (!host) return;
  const el = document.createElement('div');
  el.className = 'toast';
  el.dataset.kind = kind;
  const icon = kind === 'error' ? 'triangle-alert' : kind === 'warning' ? 'info' : 'circle-check';
  el.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#i-${icon}"></use></svg><span></span>`;
  el.querySelector('span').textContent = message;
  host.append(el);
  setTimeout(() => { el.classList.add('is-leaving'); setTimeout(() => el.remove(), 320); }, ms);
}

export function vibrate(pattern) {
  try { if (navigator.vibrate && !prefersReducedMotion()) navigator.vibrate(pattern); } catch { /* unsupported */ }
}
