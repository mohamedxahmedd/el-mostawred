// /play controller. Flow: click → POST /api/spins (server commits the result) → animate to that result → reveal.
// The request id is kept in sessionStorage until the result is shown, so a reload or dropped connection
// recovers the committed result (GET lookup / same-key replay) instead of ever spinning twice.
import { $, api, uuid, store, readJson, toast, vibrate, prefersReducedMotion } from './util.js';
import { Wheel, WEDGE_COLORS } from './wheel.js';
import { GameAudio } from './audio.js';
import { Celebration } from './fx.js';
import { ShareDialog } from './share.js';

const PENDING = 'stw.pendingSpin';
const data = readJson('play-data');
const S = data.strings ?? {};
const t = (k, ...args) => (S[k] ?? k).replace(/\{(\d)\}/g, (_, i) => args[+i] ?? '');

const els = {
  root: $('#play'),
  wheel: $('#wheel'),
  disc: $('#wheel-disc'),
  frame: $('#wheel-frame'),
  pointer: $('#wheel-pointer'),
  spin: $('#spin-btn'),
  skeleton: $('#wheel-skeleton'),
  credits: $('#credit-count'),
  creditPill: $('#credit-pill'),
  hint: $('#cta-hint'),
  state: $('#state-card'),
  stateIcon: $('#state-icon'),
  stateTitle: $('#state-title'),
  stateBody: $('#state-body'),
  stateActions: $('#state-actions'),
  netError: $('#net-error'),
  netErrorText: $('#net-error-text'),
  netRetry: $('#net-retry'),
  legend: $('#legend-list'),
  latest: $('#latest-award'),
  announcer: $('#announcer'),
  sound: $('#sound-toggle'),
  reveal: $('#reveal'),
};

const audio = new GameAudio(data.soundDefault !== false);
const wheel = new Wheel({ disc: els.disc, frame: els.frame, pointer: els.pointer, root: els.wheel }, { idPrefix: 'pw' });
const fx = new Celebration($('#reveal-fx'));
fx.intensity = { Subtle: 0.55, Standard: 1, Cinematic: 1.35 }[data.spin?.intensity] ?? 1;
const share = new ShareDialog($('#share-dialog'), { strings: S, lang: data.lang });

let credits = data.credits ?? 0;
let status = data.status === 'Ready' ? 'ready' : ({ NoCredits: 'noCredits', Paused: 'paused', Unavailable: 'unavailable' }[data.status] ?? 'ready');
let busy = false;
let lastResult = null;

// ---------------------------------------------------------------- sound toggle
function syncSound() {
  if (!els.sound) return;
  els.sound.setAttribute('aria-pressed', String(audio.enabled));
  const label = audio.enabled ? els.sound.dataset.labelOn : els.sound.dataset.labelOff;
  els.sound.querySelector('.chip-label').textContent = label;
  els.sound.setAttribute('aria-label', label);
}
els.sound?.addEventListener('click', () => {
  audio.unlock();
  audio.setEnabled(!audio.enabled);
  syncSound();
  audio.click();
});
syncSound();

// ---------------------------------------------------------------- UI state
function setCredits(n) {
  credits = Math.max(0, n | 0);
  els.credits.textContent = String(credits);
  els.creditPill.dataset.zero = credits <= 0 ? 'true' : 'false';
  if (window.gsap && !prefersReducedMotion()) gsap.fromTo(els.credits, { scale: 1.35 }, { scale: 1, duration: 0.5, ease: 'back.out(3)' });
}

function setStatus(next, message) {
  status = next;
  const ready = next === 'ready';
  els.root.dataset.status = next;
  els.wheel.classList.toggle('is-dimmed', !ready);
  els.spin.disabled = !ready || busy;
  els.hint.hidden = !ready;
  els.state.hidden = ready;
  if (ready) return;
  const map = {
    noCredits: ['badge-check', t('play.nocredits.title'), t('play.nocredits.body')],
    paused: ['circle-pause', t('play.paused.title'), message],
    unavailable: ['circle-pause', t('play.unavailable.title'), message ?? t('play.unavailable.body')],
    inactive: ['lock', t('play.unavailable.title'), t('play.inactive.body')],
  }[next];
  els.stateIcon.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#i-${map[0]}"></use></svg>`;
  els.stateTitle.textContent = map[1];
  els.stateBody.textContent = map[2] ?? '';
  els.stateActions.hidden = next !== 'noCredits';
}

function renderLegend(items) {
  els.legend.replaceChildren(...items.map(item => {
    const li = document.createElement('li');
    li.className = 'legend-item';
    const swatch = document.createElement('span');
    swatch.className = `legend-swatch swatch-${WEDGE_COLORS[item.color] ? item.color : 'ruby'}`;
    const icon = document.createElement('span');
    icon.className = 'legend-icon';
    if (item.imageUrl) {
      const img = document.createElement('img');
      img.src = item.imageUrl; img.alt = ''; img.loading = 'lazy';
      icon.append(img);
    } else {
      icon.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#p-${item.icon}"></use></svg>`;
    }
    const name = document.createElement('span');
    name.className = 'legend-name';
    name.textContent = item.name;
    if (item.description) {
      const small = document.createElement('small');
      small.textContent = item.description;
      name.append(small);
    }
    li.append(swatch, icon, name);
    return li;
  }));
}

function applyLayout(items) {
  if (!items?.length) return;
  if (!wheel.sameLayout(items)) wheel.setItems(items);
  renderLegend(items);
}

async function loadConfig() {
  try {
    const { status: code, body } = await api('/api/wheel/config');
    if (code === 401) { location.href = '/login'; return; }
    if (code !== 200 || !body) throw new Error('config ' + code);
    applyLayout(body.items);
    if (!body.items?.length && !wheel.items.length) wheel.setItems([]);
    setCredits(body.credits);
    setStatus(body.status, body.message);
  } catch {
    if (!wheel.items.length) setStatus('unavailable', t('play.error'));
  } finally {
    els.skeleton.hidden = true;
  }
}

// ---------------------------------------------------------------- spinning
function spinDuration() {
  const min = data.spin?.minMs ?? 5200, max = Math.max(min, data.spin?.maxMs ?? 6800);
  return min + Math.random() * (max - min);
}

function showNetError(text) {
  els.netErrorText.textContent = text;
  els.netError.hidden = false;
}

async function spin() {
  if (busy || status !== 'ready' || wheel.spinning) return;
  busy = true;
  els.spin.disabled = true;
  els.netError.hidden = true;
  audio.unlock();
  audio.click();

  // One click = one id. Reuse a pending id (unanswered earlier attempt) so it can never double-spend.
  const requestId = store.get(PENDING, sessionStorage) ?? uuid();
  store.set(PENDING, requestId, sessionStorage);
  els.spin.querySelector('.spin-label').textContent = '•••';
  els.spin.setAttribute('aria-label', t('play.spinning'));

  let res;
  try {
    res = await api('/api/spins', { method: 'POST', body: { requestId } });
  } catch {
    busy = false;
    resetSpinButton();
    els.spin.disabled = true; // retry goes through the explicit Retry button with the same id
    showNetError(t('play.offline'));
    return;
  }
  await handleSpinResponse(res, requestId);
}

function resetSpinButton() {
  els.spin.querySelector('.spin-label').textContent = t('play.spin');
  els.spin.setAttribute('aria-label', t('play.spin.aria'));
}

async function handleSpinResponse({ status: code, body }, requestId) {
  if (code === 200 || code === 201) {
    if (body.layout) applyLayout(body.layout);
    await playResult(body, { animate: wheel.hasItem(body.itemId) });
    store.remove(PENDING, sessionStorage);
    busy = false;
    resetSpinButton();
    setStatus(credits > 0 ? 'ready' : 'noCredits');
    loadConfig(); // fresh odds/stock for the next spin
    return;
  }

  busy = false;
  resetSpinButton();
  if (code === 401) { location.href = '/login'; return; }
  if (code === 429) {
    toast(t('play.rate'), 'warning');
    setTimeout(() => setStatus(status), 4000);
    return;
  }
  if (![400, 403, 409].includes(code)) {
    // Unknown outcome (5xx, proxy error): the spin may have committed. Keep the id; Retry replays it safely.
    els.spin.disabled = true;
    showNetError(t('play.error'));
    return;
  }
  // 400/403/409: the server refused this id without using it — forget it.
  store.remove(PENDING, sessionStorage);
  if (typeof body?.credits === 'number') setCredits(body.credits);
  if (code === 409 || code === 403) {
    const next = body?.error === 'noCredits' ? 'noCredits' : body?.error === 'paused' ? 'paused' : body?.error === 'inactive' ? 'inactive' : 'unavailable';
    setStatus(next, body?.message);
    if (next === 'unavailable' || next === 'paused') loadConfig();
    return;
  }
  setStatus(status);
  toast(body?.message ?? t('play.error'), 'error');
}

els.spin.addEventListener('click', spin);
els.netRetry.addEventListener('click', async () => {
  const requestId = store.get(PENDING, sessionStorage);
  els.netError.hidden = true;
  if (!requestId) { setStatus(status); return; }
  busy = true;
  try {
    const res = await api('/api/spins', { method: 'POST', body: { requestId } });
    await handleSpinResponse(res, requestId);
  } catch {
    busy = false;
    showNetError(t('play.offline'));
  }
});

async function playResult(result, { animate }) {
  lastResult = result;
  const reduced = prefersReducedMotion();
  if (animate) {
    if (!reduced) { audio.whoosh(); vibrate(12); }
    let lastBuzz = 0;
    await wheel.spinTo(result.itemId, {
      durationMs: spinDuration(),
      reducedMotion: reduced,
      onTick: speed => {
        audio.tick(speed);
        if (window.gsap && !reduced) gsap.fromTo(els.pointer, { rotation: -10 - 14 * speed }, { rotation: 0, duration: 0.1 + (1 - speed) * 0.22, ease: 'power2.out', overwrite: true });
        const now = performance.now();
        if (speed < 0.3 && now - lastBuzz > 110) { vibrate(4); lastBuzz = now; }
      },
      onNearEnd: () => audio.riser(1.6),
    });
  }
  setCredits(result.credits);
  showReveal(result);
}

// ---------------------------------------------------------------- reveal
const R = {
  card: $('#reveal-card'), tier: $('#reveal-tier'), medal: $('#reveal-medal'), kicker: $('#reveal-kicker'),
  title: $('#reveal-title'), name: $('#reveal-name'), body: $('#reveal-body'), pickup: $('#reveal-pickup'),
  pickupText: $('#reveal-pickup-text'), credits: $('#reveal-credits'), again: $('#reveal-again'), share: $('#reveal-share'),
  history: $('#reveal-history'), done: $('#reveal-done'),
};

function medalContent(item) {
  R.medal.replaceChildren();
  if (item.imageUrl) {
    const img = document.createElement('img');
    img.src = item.imageUrl; img.alt = '';
    R.medal.append(img);
  } else {
    R.medal.innerHTML = `<svg class="icon"><use href="/assets/icons/ui.svg#p-${item.icon}"></use></svg>`;
  }
}

function showReveal(result) {
  const won = result.result === 'won';
  const item = result.item;
  const dlg = els.reveal;
  dlg.dataset.kind = won ? 'won' : 'tryAgain';
  dlg.dataset.tier = won ? item.tier : '';
  medalContent(item);

  if (won) {
    R.tier.innerHTML = `<span class="badge badge-tier-${item.tier}">${item.tier}</span>`;
    R.kicker.textContent = t('result.won.kicker');
    R.title.textContent = t(`result.tier.${item.tier}`);
    R.title.className = 'reveal-title text-gold';
    R.name.textContent = item.name;
    R.body.textContent = t('result.thanks');
    R.pickup.hidden = !result.award;
    R.pickupText.textContent = result.award?.pickup ?? '';
    R.again.hidden = credits <= 0;
    R.again.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#i-dices"></use></svg> ${t('play.spin')}`;
    R.share.hidden = false;
    R.history.hidden = false;
    els.announcer.textContent = `${t('result.won.kicker')}: ${item.name}`;
  } else {
    R.tier.replaceChildren();
    R.kicker.textContent = t('result.retry.kicker');
    R.title.textContent = t('result.retry.title');
    R.title.className = 'reveal-title';
    R.name.textContent = '';
    R.body.textContent = t('result.retry.body');
    R.pickup.hidden = true;
    R.again.hidden = credits <= 0;
    R.again.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#i-rotate-ccw"></use></svg> ${t('result.retry.cta')}`;
    R.share.hidden = true;
    R.history.hidden = true;
    els.announcer.textContent = `${t('result.retry.kicker')} ${t('result.retry.body')}`;
  }
  R.credits.textContent = t('result.credits.left', credits);

  if (!dlg.open) dlg.showModal();
  const reduced = prefersReducedMotion();
  requestAnimationFrame(() => {
    const rect = R.medal.getBoundingClientRect();
    const origin = { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
    if (won) { fx.celebrate(item.tier, origin); audio.win(item.tier); vibrate(item.tier === 'Epic' ? [40, 60, 40, 60, 120] : [30, 50, 60]); }
    else { fx.retry(origin); audio.retry(); vibrate(20); }
  });

  if (window.gsap && !reduced) {
    const tl = gsap.timeline();
    if (won) {
      const epic = item.tier === 'Epic';
      tl.fromTo(R.card, { y: 40, opacity: 0, scale: 0.92 }, { y: 0, opacity: 1, scale: 1, duration: 0.6, ease: 'expo.out' })
        .fromTo(R.medal, { scale: 0.2, rotation: -40, opacity: 0 }, { scale: 1, rotation: 0, opacity: 1, duration: epic ? 1.1 : 0.8, ease: epic ? 'elastic.out(1, 0.5)' : 'back.out(2)' }, '-=0.35')
        .fromTo([R.kicker, R.title, R.name], { y: 18, opacity: 0 }, { y: 0, opacity: 1, stagger: 0.09, duration: 0.5, ease: 'power3.out' }, '-=0.6')
        .fromTo([R.body, R.pickup, R.credits, R.card.querySelector('.reveal-actions')], { y: 12, opacity: 0 }, { y: 0, opacity: 1, stagger: 0.06, duration: 0.4 }, '-=0.25');
    } else {
      tl.fromTo(R.card, { y: 30, opacity: 0, scale: 0.9 }, { y: 0, opacity: 1, scale: 1, duration: 0.5, ease: 'back.out(2.2)' })
        .fromTo(R.medal, { y: -50, scale: 0.6 }, { y: 0, scale: 1, duration: 0.9, ease: 'bounce.out' }, '-=0.3')
        .to(R.medal, { rotation: -360, duration: 0.8, ease: 'power2.inOut' }, '-=0.4')
        .fromTo([R.kicker, R.title], { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, stagger: 0.08, duration: 0.45, ease: 'back.out(3)' }, '-=0.9');
    }
  }
  (won ? R.share : R.again.hidden ? R.done : R.again).focus({ preventScroll: true });
}

function closeReveal() {
  if (els.reveal.open) els.reveal.close();
}
els.reveal.addEventListener('close', () => {
  fx.stop();
  if (lastResult?.result === 'won') renderLatest(lastResult);
  if (credits <= 0) setStatus('noCredits');
  else setStatus(status === 'noCredits' ? 'ready' : status);
});
els.reveal.addEventListener('cancel', e => { if (busy) e.preventDefault(); });
R.done.addEventListener('click', closeReveal);
R.again.addEventListener('click', () => { closeReveal(); setTimeout(spin, 120); });
R.share.addEventListener('click', () => {
  if (!lastResult?.award) return;
  share.open(awardForShare(lastResult));
});

function awardForShare(result) {
  return { name: result.item.name, tier: result.item.tier, imageUrl: result.item.imageUrl, icon: result.item.icon, date: result.wonOn };
}

function renderLatest(result) {
  const card = document.createElement('article');
  card.className = 'award-card panel';
  const medal = document.createElement('div');
  medal.className = 'prize-medal';
  medal.setAttribute('aria-hidden', 'true');
  if (result.item.imageUrl) { const img = document.createElement('img'); img.src = result.item.imageUrl; img.alt = ''; medal.append(img); }
  else medal.innerHTML = `<svg class="icon"><use href="/assets/icons/ui.svg#p-${result.item.icon}"></use></svg>`;
  const body = document.createElement('div');
  const badge = document.createElement('span'); badge.className = `badge badge-tier-${result.item.tier}`; badge.textContent = result.item.tier;
  const h = document.createElement('h3'); h.textContent = result.item.name;
  const meta = document.createElement('div'); meta.className = 'award-meta';
  const st = document.createElement('span'); st.className = 'badge badge-warn'; st.textContent = t('prizes.status.Pending');
  const when = document.createElement('span'); when.textContent = result.wonOn;
  meta.append(st, when);
  const pickup = document.createElement('p'); pickup.className = 'award-pickup'; pickup.textContent = result.award?.pickup ?? '';
  const actions = document.createElement('div'); actions.className = 'award-actions';
  const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn-gold btn-sm';
  btn.innerHTML = `<svg class="icon" aria-hidden="true"><use href="/assets/icons/ui.svg#i-share-2"></use></svg> `;
  btn.append(document.createTextNode(t('result.share')));
  btn.addEventListener('click', () => share.open(awardForShare(result)));
  actions.append(btn);
  body.append(badge, h, meta, pickup, actions);
  card.append(medal, body);
  const kicker = document.createElement('p');
  kicker.className = 'kicker'; kicker.style.marginBottom = '10px'; kicker.textContent = t('play.last');
  els.latest.replaceChildren(kicker, card);
}

// Award cards rendered by the server.
document.querySelectorAll('[data-share]').forEach(btn => btn.addEventListener('click', () => share.open({
  name: btn.dataset.name, tier: btn.dataset.tier, imageUrl: btn.dataset.image || null, icon: btn.dataset.icon, date: btn.dataset.date,
})));

// ---------------------------------------------------------------- boot
async function recoverPending() {
  const pending = store.get(PENDING, sessionStorage);
  if (!pending) return;
  try {
    const { status: code, body } = await api(`/api/spins/${encodeURIComponent(pending)}`);
    if (code === 200 && body) {
      store.remove(PENDING, sessionStorage);
      toast(t('play.restored'), 'success');
      await playResult(body, { animate: false });
    }
    // 404: not stored (yet). Keep the id — the next Spin reuses it, so a request still in flight can't spend twice.
  } catch { /* offline: keep the id for a later retry */ }
}

(async () => {
  wheel.setItems([]);
  await loadConfig();
  await recoverPending();
})();
