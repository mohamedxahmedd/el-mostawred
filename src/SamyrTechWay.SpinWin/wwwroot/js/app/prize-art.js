// Full-colour prize illustrations (original artwork, 200×200 viewBox) for the wheel, the result card and the prize list.
// Keys match PrizeIcons.cs. "ID-" in gradient ids is replaced with a per-wheel prefix so several wheels can share a page.
// An uploaded prize photo always wins over these; items without art fall back to the line icons in icons.js.

const shadow = `<radialGradient id="ID-shadow"><stop offset="0" stop-color="rgba(0,0,0,.6)"/><stop offset="1" stop-color="rgba(0,0,0,0)"/></radialGradient>`;
const gold = `<linearGradient id="ID-gold" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#fff4b0"/><stop offset=".35" stop-color="#ffcd3c"/><stop offset=".7" stop-color="#e89400"/><stop offset="1" stop-color="#8f5200"/></linearGradient>`;

export const PRIZE_ART = {
  mouse: {
    defs: `${shadow}
      <linearGradient id="ID-mouse" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#55505c"/><stop offset=".3" stop-color="#232027"/><stop offset="1" stop-color="#050506"/></linearGradient>
      <linearGradient id="ID-rgb" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffd166"/><stop offset=".5" stop-color="#ff7a00"/><stop offset="1" stop-color="#ff3d00"/></linearGradient>`,
    body: `<ellipse cx="104" cy="182" rx="48" ry="9" fill="url(#ID-shadow)"/>
      <g transform="rotate(-16 100 100)">
        <path d="M100 16C140 16 154 52 154 94C154 142 134 182 100 182S46 142 46 94C46 52 60 16 100 16Z" fill="url(#ID-mouse)" stroke="#000" stroke-width="3"/>
        <path d="M50 96C50 130 60 160 80 174" fill="none" stroke="url(#ID-rgb)" stroke-width="5" stroke-linecap="round"/>
        <path d="M150 96C150 130 140 160 120 174" fill="none" stroke="url(#ID-rgb)" stroke-width="5" stroke-linecap="round"/>
        <path d="M52 86Q100 102 148 86" fill="none" stroke="#000" stroke-width="3"/>
        <path d="M100 18V92" stroke="#000" stroke-width="3"/>
        <rect x="92" y="36" width="16" height="34" rx="8" fill="#2c2932" stroke="#000" stroke-width="2.5"/>
        <path d="M95 44h10M95 50h10M95 56h10M95 62h10" stroke="#ff8a00" stroke-width="2" stroke-linecap="round" opacity=".9"/>
        <path d="M88 138l12-14 12 14-12 10Z" fill="url(#ID-rgb)" opacity=".95"/>
        <path d="M66 40C74 26 88 22 96 22" fill="none" stroke="rgba(255,255,255,.35)" stroke-width="5" stroke-linecap="round"/>
        <ellipse cx="76" cy="62" rx="10" ry="22" fill="rgba(255,255,255,.08)"/>
      </g>`,
  },
  usb: {
    defs: `${shadow}
      <linearGradient id="ID-metal" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#9aa0a6"/><stop offset=".3" stop-color="#f4f5f7"/><stop offset=".6" stop-color="#b9bec4"/><stop offset="1" stop-color="#6d7278"/></linearGradient>
      <linearGradient id="ID-usb" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#3a3740"/><stop offset=".45" stop-color="#16141a"/><stop offset="1" stop-color="#050506"/></linearGradient>`,
    body: `<ellipse cx="104" cy="184" rx="42" ry="8" fill="url(#ID-shadow)"/>
      <g transform="rotate(-32 100 100)">
        <rect x="74" y="14" width="52" height="48" rx="4" fill="url(#ID-metal)" stroke="#3b3f44" stroke-width="2"/>
        <rect x="85" y="26" width="11" height="9" rx="1.5" fill="#33363b"/><rect x="104" y="26" width="11" height="9" rx="1.5" fill="#33363b"/>
        <rect x="64" y="58" width="72" height="118" rx="16" fill="url(#ID-usb)" stroke="#000" stroke-width="3"/>
        <rect x="64" y="58" width="72" height="12" rx="4" fill="#0b0a0d"/>
        <rect x="64" y="128" width="72" height="7" fill="#ff8a00"/>
        <circle cx="100" cy="112" r="5" fill="#ffb02e"/><circle cx="100" cy="112" r="10" fill="rgba(255,150,30,.25)"/>
        <circle cx="100" cy="158" r="8" fill="#000" stroke="#34313a" stroke-width="2"/>
        <rect x="72" y="74" width="10" height="92" rx="5" fill="rgba(255,255,255,.1)"/>
      </g>`,
  },
  backpack: {
    defs: `${shadow}
      <linearGradient id="ID-bag" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3b3a40"/><stop offset=".4" stop-color="#1d1c21"/><stop offset="1" stop-color="#0a0a0c"/></linearGradient>
      <linearGradient id="ID-pocket" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2a292f"/><stop offset="1" stop-color="#0e0e11"/></linearGradient>`,
    body: `<ellipse cx="100" cy="182" rx="74" ry="10" fill="url(#ID-shadow)"/>
      <path d="M72 70C72 34 128 34 128 70" fill="none" stroke="#050506" stroke-width="15" stroke-linecap="round"/>
      <path d="M72 70C72 38 128 38 128 70" fill="none" stroke="#3c3b42" stroke-width="4" stroke-linecap="round"/>
      <rect x="24" y="66" width="152" height="110" rx="18" fill="url(#ID-bag)" stroke="#000" stroke-width="3"/>
      <rect x="32" y="74" width="136" height="94" rx="13" fill="none" stroke="rgba(255,255,255,.14)" stroke-width="1.5" stroke-dasharray="4 4"/>
      <path d="M36 82H164" stroke="#5a5962" stroke-width="2.5" stroke-dasharray="3 2"/>
      <rect x="44" y="102" width="112" height="62" rx="11" fill="url(#ID-pocket)" stroke="#000" stroke-width="2.5"/>
      <path d="M52 112H148" stroke="#5a5962" stroke-width="2" stroke-dasharray="3 2"/>
      <rect x="140" y="106" width="14" height="10" rx="2" fill="#c9ccd1"/>
      <rect x="86" y="128" width="28" height="12" rx="3" fill="#ff8a00"/>
      <rect x="16" y="86" width="12" height="18" rx="3" fill="#a7abb1" stroke="#33363b" stroke-width="2"/>
      <rect x="172" y="86" width="12" height="18" rx="3" fill="#a7abb1" stroke="#33363b" stroke-width="2"/>
      <path d="M36 74Q60 68 96 68" fill="none" stroke="rgba(255,255,255,.22)" stroke-width="4" stroke-linecap="round"/>`,
  },
  coins: {
    defs: `${shadow}${gold}
      <linearGradient id="ID-edge" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#9a5a00"/><stop offset=".4" stop-color="#e6a21a"/><stop offset="1" stop-color="#7a4400"/></linearGradient>`,
    body: `<ellipse cx="96" cy="184" rx="70" ry="9" fill="url(#ID-shadow)"/>
      ${[164, 146, 128].map(y => `<path d="M30 ${y}v-12h96v12a48 15 0 0 1-96 0Z" fill="url(#ID-edge)" stroke="#5c3300" stroke-width="2"/>
        <ellipse cx="78" cy="${y - 12}" rx="48" ry="15" fill="url(#ID-gold)" stroke="#5c3300" stroke-width="2"/>`).join('')}
      <circle cx="130" cy="86" r="54" fill="url(#ID-edge)" stroke="#5c3300" stroke-width="2.5"/>
      <circle cx="126" cy="82" r="50" fill="url(#ID-gold)" stroke="#5c3300" stroke-width="2.5"/>
      <circle cx="126" cy="82" r="38" fill="none" stroke="#b36b00" stroke-width="3"/>
      <path d="M146 70A24 24 0 1 0 150 92" fill="none" stroke="#8a4f00" stroke-width="7" stroke-linecap="round"/>
      <path d="M140 58l14 12-17 6Z" fill="#8a4f00"/>
      <path d="M96 52Q110 40 128 40" fill="none" stroke="rgba(255,255,255,.65)" stroke-width="5" stroke-linecap="round"/>
      <path d="M176 30l3 8 8 3-8 3-3 8-3-8-8-3 8-3Z" fill="#fffbe0"/>`,
  },
  smile: {
    defs: `${shadow}
      <radialGradient id="ID-face" cx=".38" cy=".32" r=".75"><stop offset="0" stop-color="#fff7a8"/><stop offset=".45" stop-color="#ffd21f"/><stop offset=".85" stop-color="#f59e00"/><stop offset="1" stop-color="#c46d00"/></radialGradient>`,
    body: `<ellipse cx="100" cy="186" rx="56" ry="8" fill="url(#ID-shadow)"/>
      <circle cx="100" cy="100" r="80" fill="url(#ID-face)" stroke="#8a4a00" stroke-width="3"/>
      <ellipse cx="72" cy="84" rx="10" ry="15" fill="#4a2600"/><ellipse cx="128" cy="84" rx="10" ry="15" fill="#4a2600"/>
      <circle cx="69" cy="78" r="3.5" fill="#fff"/><circle cx="125" cy="78" r="3.5" fill="#fff"/>
      <path d="M58 118Q100 160 142 118" fill="none" stroke="#4a2600" stroke-width="9" stroke-linecap="round"/>
      <ellipse cx="52" cy="118" rx="11" ry="7" fill="rgba(255,110,50,.35)"/><ellipse cx="148" cy="118" rx="11" ry="7" fill="rgba(255,110,50,.35)"/>
      <ellipse cx="74" cy="46" rx="26" ry="12" fill="rgba(255,255,255,.45)" transform="rotate(-20 74 46)"/>`,
  },
  headset: {
    defs: `${shadow}
      <linearGradient id="ID-cup" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#4b4752"/><stop offset=".4" stop-color="#1e1c22"/><stop offset="1" stop-color="#050506"/></linearGradient>
      <linearGradient id="ID-rgb" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffd166"/><stop offset="1" stop-color="#ff5a00"/></linearGradient>`,
    body: `<ellipse cx="100" cy="186" rx="64" ry="8" fill="url(#ID-shadow)"/>
      <path d="M42 112C42 52 66 24 100 24S158 52 158 112" fill="none" stroke="#050506" stroke-width="20" stroke-linecap="round"/>
      <path d="M46 108C46 56 70 30 100 30S154 56 154 108" fill="none" stroke="#3a3840" stroke-width="5" stroke-linecap="round"/>
      <path d="M58 40Q100 14 142 40" fill="none" stroke="url(#ID-rgb)" stroke-width="4" stroke-linecap="round"/>
      <rect x="22" y="94" width="48" height="76" rx="22" fill="url(#ID-cup)" stroke="#000" stroke-width="3"/>
      <rect x="130" y="94" width="48" height="76" rx="22" fill="url(#ID-cup)" stroke="#000" stroke-width="3"/>
      <rect x="32" y="106" width="28" height="52" rx="14" fill="none" stroke="url(#ID-rgb)" stroke-width="4"/>
      <rect x="140" y="106" width="28" height="52" rx="14" fill="none" stroke="url(#ID-rgb)" stroke-width="4"/>
      <path d="M40 160C46 184 70 190 92 184" fill="none" stroke="#111" stroke-width="7" stroke-linecap="round"/>
      <circle cx="96" cy="183" r="7" fill="#ff8a00" stroke="#000" stroke-width="2"/>
      <path d="M30 104Q34 98 42 97" fill="none" stroke="rgba(255,255,255,.3)" stroke-width="4" stroke-linecap="round"/>`,
  },
  mousepad: {
    defs: `${shadow}
      <linearGradient id="ID-pad" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2f2c35"/><stop offset=".5" stop-color="#141217"/><stop offset="1" stop-color="#050506"/></linearGradient>
      <linearGradient id="ID-rgb" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#ff3d00"/><stop offset=".5" stop-color="#ff9500"/><stop offset="1" stop-color="#ffd166"/></linearGradient>`,
    body: `<ellipse cx="100" cy="170" rx="88" ry="10" fill="url(#ID-shadow)"/>
      <path d="M34 64L176 52L186 146L24 160Z" fill="url(#ID-rgb)"/>
      <path d="M38 69L172 58L180 141L29 154Z" fill="url(#ID-pad)"/>
      <path d="M150 84l10 -10 10 10 -10 9Z" fill="#ff8a00" opacity=".9"/>
      <path d="M48 80L120 74" stroke="rgba(255,255,255,.12)" stroke-width="5" stroke-linecap="round"/>
      <g transform="translate(70 92) rotate(-8)"><path d="M18 0C30 0 34 10 34 22C34 38 28 48 18 48S2 38 2 22C2 10 6 0 18 0Z" fill="#1c1a20" stroke="#000" stroke-width="2"/><path d="M18 2V18" stroke="#000" stroke-width="2"/><path d="M4 30C6 40 10 44 14 46" fill="none" stroke="#ff8a00" stroke-width="2.5" stroke-linecap="round"/></g>`,
  },
  cleaning: {
    defs: `${shadow}
      <linearGradient id="ID-bottle" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#dfe9f3"/><stop offset=".35" stop-color="#ffffff"/><stop offset="1" stop-color="#9fb2c4"/></linearGradient>
      <linearGradient id="ID-liquid" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7fd0ff"/><stop offset="1" stop-color="#1f7ac9"/></linearGradient>
      <linearGradient id="ID-cloth" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ffb347"/><stop offset="1" stop-color="#e86a00"/></linearGradient>`,
    body: `<ellipse cx="100" cy="186" rx="74" ry="8" fill="url(#ID-shadow)"/>
      <path d="M112 150C128 136 172 136 186 152C190 166 182 178 168 182C140 188 114 182 106 172C102 164 104 156 112 150Z" fill="url(#ID-cloth)" stroke="#8a3d00" stroke-width="2.5"/>
      <path d="M118 158C138 150 164 150 180 160M114 168C136 162 162 164 176 172" fill="none" stroke="rgba(255,255,255,.35)" stroke-width="2.5"/>
      <path d="M58 34H96V48H58Z" fill="#ff8a00" stroke="#7a3a00" stroke-width="2.5"/>
      <path d="M96 38H118L114 46H96Z" fill="#ff8a00" stroke="#7a3a00" stroke-width="2.5"/>
      <path d="M64 48V62L92 62V48" fill="#3a3a40" stroke="#000" stroke-width="2.5"/>
      <path d="M90 52Q102 60 100 76" fill="none" stroke="#3a3a40" stroke-width="6" stroke-linecap="round"/>
      <path d="M54 62H98C108 80 110 100 110 120V170C110 178 104 182 96 182H56C48 182 42 178 42 170V120C42 100 44 80 54 62Z" fill="url(#ID-bottle)" stroke="#5c6d7d" stroke-width="3"/>
      <path d="M46 116H106V170C106 176 102 178 96 178H56C50 178 46 176 46 170Z" fill="url(#ID-liquid)" opacity=".9"/>
      <rect x="56" y="124" width="40" height="26" rx="5" fill="#fff" opacity=".9"/><rect x="62" y="132" width="28" height="4" rx="2" fill="#ff8a00"/><rect x="62" y="140" width="20" height="3" rx="1.5" fill="#9fb2c4"/>
      <rect x="50" y="72" width="8" height="96" rx="4" fill="rgba(255,255,255,.55)"/>`,
  },
  stand: {
    defs: `${shadow}
      <linearGradient id="ID-alu" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#f4f5f7"/><stop offset=".45" stop-color="#b9bec4"/><stop offset="1" stop-color="#6d7278"/></linearGradient>
      <linearGradient id="ID-lid" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#4a4752"/><stop offset="1" stop-color="#0c0b0e"/></linearGradient>`,
    body: `<ellipse cx="100" cy="182" rx="82" ry="9" fill="url(#ID-shadow)"/>
      <path d="M44 178L70 120M156 178L130 120" stroke="url(#ID-alu)" stroke-width="12" stroke-linecap="round"/>
      <path d="M38 178H162" stroke="#8b9096" stroke-width="7" stroke-linecap="round"/>
      <path d="M22 122L178 96L182 108L26 136Z" fill="url(#ID-alu)" stroke="#4b5056" stroke-width="2.5"/>
      <path d="M38 118L160 98L136 24L20 44Z" fill="url(#ID-lid)" stroke="#000" stroke-width="3"/>
      <path d="M44 110L152 92L132 32L28 50Z" fill="#141218"/>
      <path d="M78 72l10-10 10 10-10 9Z" fill="#ff8a00"/>
      <path d="M34 52L60 47" stroke="rgba(255,255,255,.25)" stroke-width="4" stroke-linecap="round"/>`,
  },
  keyboard: {
    defs: `${shadow}
      <linearGradient id="ID-kb" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#34313a"/><stop offset="1" stop-color="#0a090c"/></linearGradient>`,
    body: `<ellipse cx="100" cy="164" rx="92" ry="9" fill="url(#ID-shadow)"/>
      <path d="M26 60H174L190 150H10Z" fill="url(#ID-kb)" stroke="#000" stroke-width="3"/>
      ${[0, 1, 2, 3].map(r => Array.from({ length: 10 }, (_, c) => {
        const y = 70 + r * 19, x0 = 30 - r * 4, w = (140 + r * 8) / 10;
        return `<rect x="${(x0 + c * w + 1.5).toFixed(1)}" y="${y}" width="${(w - 3).toFixed(1)}" height="14" rx="2.5" fill="#1d1b21" stroke="${(r + c) % 4 === 0 ? '#ff8a00' : 'rgba(255,138,0,.45)'}" stroke-width="1.6"/>`;
      }).join('')).join('')}`,
  },
  gift: {
    defs: `${shadow}${gold}
      <linearGradient id="ID-box" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff4d3a"/><stop offset=".55" stop-color="#d0150f"/><stop offset="1" stop-color="#7a0705"/></linearGradient>`,
    body: `<ellipse cx="100" cy="184" rx="70" ry="9" fill="url(#ID-shadow)"/>
      <rect x="38" y="90" width="124" height="90" rx="8" fill="url(#ID-box)" stroke="#4d0303" stroke-width="3"/>
      <rect x="28" y="66" width="144" height="32" rx="7" fill="url(#ID-box)" stroke="#4d0303" stroke-width="3"/>
      <rect x="88" y="66" width="24" height="114" fill="url(#ID-gold)" stroke="#7a4400" stroke-width="2"/>
      <path d="M100 66C80 30 46 38 56 58C62 68 84 68 100 66ZM100 66C120 30 154 38 144 58C138 68 116 68 100 66Z" fill="url(#ID-gold)" stroke="#7a4400" stroke-width="2.5"/>
      <path d="M40 72H80" stroke="rgba(255,255,255,.35)" stroke-width="4" stroke-linecap="round"/>`,
  },
  retry: {
    defs: `<linearGradient id="ID-arrow" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ffd166"/><stop offset=".5" stop-color="#ff9500"/><stop offset="1" stop-color="#e05a00"/></linearGradient>`,
    body: `<path d="M143 68A56 56 0 1 1 90 49" fill="none" stroke="#1a0d00" stroke-width="34"/>
      <path d="M86 12L132 49L86 86Z" fill="#1a0d00" stroke="#1a0d00" stroke-width="8" stroke-linejoin="round"/>
      <path d="M143 68A56 56 0 1 1 90 49" fill="none" stroke="url(#ID-arrow)" stroke-width="24"/>
      <path d="M88 20L126 49L88 78Z" fill="url(#ID-arrow)"/>
      <path d="M62 84A44 44 0 0 1 80 60" fill="none" stroke="rgba(255,255,255,.45)" stroke-width="6" stroke-linecap="round"/>`,
  },
};

PRIZE_ART.bag = PRIZE_ART.backpack; // a laptop bag reads better than a shopping bag on the wheel

/** Inline <svg> for HTML (result card, prize list). Returns null when the key has no artwork. */
export function artSvg(key, prefix) {
  const art = PRIZE_ART[key];
  if (!art) return null;
  return `<svg viewBox="0 0 200 200" aria-hidden="true"><defs>${art.defs.replaceAll('ID-', `${prefix}-`)}</defs>${art.body.replaceAll('ID-', `${prefix}-`)}</svg>`;
}
