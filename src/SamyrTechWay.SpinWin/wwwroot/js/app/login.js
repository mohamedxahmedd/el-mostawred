// Customer sign-in: live phone formatting, code formatting, visibility toggle, inline validation, busy state.
import { $ } from './util.js';

const form = $('#login-form');
if (form) {
  const phone = $('#phone');
  const code = $('#code');
  const toggle = $('#code-toggle');
  const submit = $('#login-submit');
  const error = $('#login-error');
  if (error) { error.focus(); }

  const toAscii = s => s.replace(/[٠-٩]/g, d => String(d.charCodeAt(0) - 0x0660)).replace(/[۰-۹]/g, d => String(d.charCodeAt(0) - 0x06F0));

  const normalisePhone = raw => {
    let d = toAscii(raw).replace(/[^\d+]/g, '');
    if (d.startsWith('+20')) d = '0' + d.slice(3);
    else if (d.startsWith('0020')) d = '0' + d.slice(4);
    else if (d.startsWith('20') && d.length === 12) d = '0' + d.slice(2);
    return d.replace(/\D/g, '');
  };
  const validPhone = raw => /^01[0125]\d{8}$/.test(normalisePhone(raw));
  const normaliseCode = raw => toAscii(raw).toUpperCase().replace(/[\s\-_]/g, '');
  const validCode = raw => /^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{10}$/.test(normaliseCode(raw));

  phone.addEventListener('input', () => {
    const d = normalisePhone(phone.value);
    if (/^01\d*$/.test(d) && d.length <= 11 && !phone.value.trim().startsWith('+')) {
      const parts = [d.slice(0, 3), d.slice(3, 7), d.slice(7, 11)].filter(Boolean);
      const formatted = parts.join(' ');
      if (formatted !== phone.value) phone.value = formatted;
    }
    clear(phone, '#phone-error');
  });

  code.addEventListener('input', () => {
    const c = normaliseCode(code.value).slice(0, 10);
    const formatted = c.length > 5 ? `${c.slice(0, 5)}-${c.slice(5)}` : c;
    if (formatted !== code.value) code.value = formatted;
    clear(code, '#code-error');
  });

  toggle.addEventListener('click', () => {
    const show = code.type === 'password';
    code.type = show ? 'text' : 'password';
    toggle.setAttribute('aria-pressed', String(show));
    toggle.setAttribute('aria-label', show ? toggle.dataset.hide : toggle.dataset.show);
    toggle.querySelector('use').setAttribute('href', `/assets/icons/ui.svg#i-${show ? 'eye-off' : 'eye'}`);
  });

  function clear(input, errSel) {
    input.setAttribute('aria-invalid', 'false');
    const e = $(errSel); e.hidden = true; e.textContent = '';
  }
  function fail(input, errSel) {
    const e = $(errSel);
    e.textContent = e.dataset.message; e.hidden = false;
    input.setAttribute('aria-invalid', 'true');
  }

  form.addEventListener('submit', (ev) => {
    let firstBad = null;
    if (!validPhone(phone.value)) { fail(phone, '#phone-error'); firstBad ??= phone; }
    if (!validCode(code.value)) { fail(code, '#code-error'); firstBad ??= code; }
    if (firstBad) { ev.preventDefault(); firstBad.focus(); return; }
    if (submit.classList.contains('is-busy')) { ev.preventDefault(); return; }
    submit.classList.add('is-busy');
    submit.setAttribute('aria-disabled', 'true');
  });

  // Back/forward cache: never leave the button stuck in its busy state.
  addEventListener('pageshow', () => { submit.classList.remove('is-busy'); submit.removeAttribute('aria-disabled'); });
}
