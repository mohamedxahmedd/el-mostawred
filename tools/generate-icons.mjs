import fs from 'node:fs';
// Dev-only generator. Usage: npm pack lucide-static@1.55.0, extract, then
// node tools/generate-icons.mjs <lucide>/package/icons src/SamyrTechWay.SpinWin/wwwroot/js/app/icons.js src/SamyrTechWay.SpinWin/Services/PrizeIcons.cs src/SamyrTechWay.SpinWin/wwwroot/assets/icons/ui.svg
const [,, srcDir, outJs, outCs, outSprite] = process.argv;
const prize = {gift:'gift',mouse:'mouse',mousepad:'rectangle-horizontal',headphones:'headphones',headset:'headset',backpack:'backpack',keyboard:'keyboard',usb:'usb',cleaning:'spray-can',laptop:'laptop',stand:'laptop-minimal',speaker:'speaker',cable:'cable',charger:'battery-charging',gamepad:'gamepad-2',watch:'watch',phone:'smartphone',storage:'hard-drive',bag:'shopping-bag',voucher:'ticket',discount:'badge-percent',trophy:'trophy',crown:'crown',gem:'gem',star:'star',medal:'medal',sparkles:'sparkles',retry:'rotate-ccw'};
const labels = {gift:'Gift box',mouse:'Mouse',mousepad:'Mouse pad',headphones:'Headphones',headset:'Gaming headset',backpack:'Backpack',keyboard:'Keyboard',usb:'USB drive',cleaning:'Cleaning kit',laptop:'Laptop',stand:'Laptop stand',speaker:'Speaker',cable:'Cable',charger:'Charger / power bank',gamepad:'Game controller',watch:'Smart watch',phone:'Phone',storage:'Storage drive',bag:'Shopping bag',voucher:'Voucher',discount:'Discount',trophy:'Trophy',crown:'Crown',gem:'Gem',star:'Star',medal:'Medal',sparkles:'Sparkles',retry:'Try again'};
const ui = ['volume-2','volume-x','eye','eye-off','log-out','share-2','download','copy','x','check','users','layout-dashboard','gift','rotate-ccw','settings','history','package','printer','search','chevron-left','chevron-right','chevron-down','plus','pencil','archive','archive-restore','shield-check','triangle-alert','clock','phone','key-round','menu','languages','sparkles','trophy','info','filter','refresh-cw','lock','user','ticket','circle-check','wifi-off','circle-pause','circle-play','scroll-text','coins','image','badge-check','minus','arrow-right','arrow-left','store','percent','dices','house','loader-circle','external-link','list','user-plus','power','calendar'];
const attr = (s, n) => { const m = s.match(new RegExp(`\\b${n}="([^"]*)"`)); return m ? parseFloat(m[1]) : undefined; };
const attrS = (s, n) => { const m = s.match(new RegExp(`\\b${n}="([^"]*)"`)); return m ? m[1] : undefined; };
// A path's first moveto is absolute even when written as 'm'. Make it explicit so paths can be concatenated;
// any implicit coordinate pairs after a relative 'm' are relative linetos, so they keep an explicit 'l'.
function absoluteFirstMove(d) {
  d = d.trim();
  if (d[0] !== 'm') return d;
  const m = d.match(/^m\s*(-?\d*\.?\d+)\s*,?\s*(-?\d*\.?\d+)/);
  const rest = d.slice(m[0].length).trimStart();
  return `M${m[1]} ${m[2]}` + (/^[-\d.]/.test(rest) ? `l${rest}` : rest);
}
function toPaths(svg) {
  const out = [];
  for (const m of svg.matchAll(/<(path|rect|circle|ellipse|line|polyline|polygon)\b([^>]*)\/>/g)) {
    const [, tag, a] = m;
    if (tag === 'path') out.push(absoluteFirstMove(attrS(a, 'd')));
    else if (tag === 'line') out.push(`M${attr(a,'x1')} ${attr(a,'y1')}L${attr(a,'x2')} ${attr(a,'y2')}`);
    else if (tag === 'polyline' || tag === 'polygon') { const p = attrS(a,'points').trim().split(/[\s,]+/); let d = `M${p[0]} ${p[1]}`; for (let i=2;i<p.length;i+=2) d += `L${p[i]} ${p[i+1]}`; out.push(tag==='polygon'? d+'Z' : d); }
    else if (tag === 'circle' || tag === 'ellipse') { const cx=attr(a,'cx'), cy=attr(a,'cy'); const rx = attr(a,'r') ?? attr(a,'rx'), ry = attr(a,'r') ?? attr(a,'ry'); out.push(`M${cx-rx} ${cy}a${rx} ${ry} 0 1 0 ${2*rx} 0a${rx} ${ry} 0 1 0 ${-2*rx} 0`); }
    else if (tag === 'rect') { const x=attr(a,'x')??0, y=attr(a,'y')??0, w=attr(a,'width'), h=attr(a,'height'); let rx = attr(a,'rx') ?? attr(a,'ry') ?? 0; rx = Math.min(rx, w/2, h/2);
      out.push(rx ? `M${x+rx} ${y}h${w-2*rx}a${rx} ${rx} 0 0 1 ${rx} ${rx}v${h-2*rx}a${rx} ${rx} 0 0 1 ${-rx} ${rx}h${-(w-2*rx)}a${rx} ${rx} 0 0 1 ${-rx} ${-rx}v${-(h-2*rx)}a${rx} ${rx} 0 0 1 ${rx} ${-rx}z` : `M${x} ${y}h${w}v${h}h${-w}z`); }
  }
  return out.join('');
}
const read = n => fs.readFileSync(`${srcDir}/${n}.svg`, 'utf8');
let js = `// Generated from lucide-static v1.55.0 (ISC License, https://lucide.dev). 24×24 stroke paths usable as SVG <path d> and Canvas Path2D.\nexport const PRIZE_ICONS = Object.freeze({\n`;
for (const [k, n] of Object.entries(prize)) js += `  ${JSON.stringify(k)}: ${JSON.stringify(toPaths(read(n)))},\n`;
js += `});\n`;
fs.writeFileSync(outJs, js);
let cs = `namespace SamyrTechWay.SpinWin.Services;\n\n/// <summary>Built-in prize icon keys. Path data lives in wwwroot/js/app/icons.js (generated together).</summary>\npublic static class PrizeIcons\n{\n    public static readonly IReadOnlyList<(string Key, string Label)> All =\n    [\n`;
for (const k of Object.keys(prize)) cs += `        (${JSON.stringify(k)}, ${JSON.stringify(labels[k])}),\n`;
cs += `    ];\n\n    private static readonly HashSet<string> Keys = All.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);\n\n    public static bool IsValid(string? key) => key is not null && Keys.Contains(key);\n}\n`;
fs.writeFileSync(outCs, cs);
let sp = `<svg xmlns="http://www.w3.org/2000/svg"><!-- lucide-static v1.55.0, ISC License -->\n`;
for (const n of ui) sp += `<symbol id="i-${n}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="${toPaths(read(n))}"/></symbol>\n`;
for (const [k, n] of Object.entries(prize)) sp += `<symbol id="p-${k}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="${toPaths(read(n))}"/></symbol>\n`;
sp += `</svg>\n`;
fs.writeFileSync(outSprite, sp);
console.log('ok', Object.keys(prize).length, ui.length);
