// /my-prizes: share buttons on the customer's own server-rendered awards.
import { readJson } from './util.js';
import { ShareDialog } from './share.js';

const data = readJson('page-data');
const dialog = document.getElementById('share-dialog');
if (dialog) {
  const share = new ShareDialog(dialog, { strings: data.strings ?? {}, lang: data.lang ?? 'ar' });
  document.querySelectorAll('[data-share]').forEach(btn => btn.addEventListener('click', () => share.open({
    name: btn.dataset.name, tier: btn.dataset.tier, imageUrl: btn.dataset.image || null, icon: btn.dataset.icon, date: btn.dataset.date,
  })));
}
