// Prizes page: live allocation total (must equal 100.00%) and a preview of the live wheel.
import { Wheel } from '../app/wheel.js';

const form = document.getElementById('allocation-form');
if (form) {
  const sumEl = document.getElementById('alloc-sum');
  const bar = document.getElementById('alloc-bar');
  const box = document.getElementById('alloc-total');
  const msg = document.getElementById('alloc-msg');
  const save = document.getElementById('alloc-save');
  const rows = [...form.querySelectorAll('tbody tr')];

  const update = () => {
    let bp = 0, bad = false;
    for (const row of rows) {
      const active = row.querySelector('[data-alloc-active]')?.checked;
      const input = row.querySelector('[data-alloc-pct]');
      const v = Number(input.value);
      const twoDecimals = Math.abs(v * 100 - Math.round(v * 100)) < 1e-6;
      const invalid = input.value.trim() === '' || !Number.isFinite(v) || v < 0 || v > 100 || !twoDecimals;
      input.setAttribute('aria-invalid', String(invalid || (active && v <= 0)));
      if (invalid || (active && v <= 0)) bad = true;
      if (active && Number.isFinite(v)) bp += Math.round(v * 100);
    }
    const ok = bp === 10000 && !bad;
    sumEl.textContent = (bp / 100).toFixed(2) + '%';
    bar.style.width = Math.min(100, bp / 100) + '%';
    box.dataset.valid = String(ok);
    msg.textContent = ok ? 'جاهز للحفظ' : bad ? 'صحّح النسب المميّزة (من 0 إلى 100، برقمين عشريين بحد أقصى، والعناصر المفعّلة أكبر من 0)' : bp < 10000 ? `متبقٍ ${((10000 - bp) / 100).toFixed(2)}% للتوزيع` : `زيادة ${((bp - 10000) / 100).toFixed(2)}% عن المطلوب`;
    if (save) save.disabled = !ok;
  };
  form.addEventListener('input', update);
  form.addEventListener('change', update);
  update();
}

const dataEl = document.getElementById('preview-data');
if (dataEl) {
  const items = JSON.parse(dataEl.textContent);
  const wheel = new Wheel({ disc: document.getElementById('preview-disc'), frame: document.getElementById('preview-frame') }, { idPrefix: 'adm' });
  wheel.setItems(items);
}
