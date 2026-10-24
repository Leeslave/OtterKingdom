// Unity가 실행되지 않을 때도 기획 표와 커밋된 YAML을 일치시킨다. 기존 ID · GUID · 완료 기록은 유지한다.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const spec = JSON.parse(fs.readFileSync(path.join(root, 'Tools/Progression/flow.json'), 'utf8'));
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const write = (p, s) => fs.writeFileSync(path.join(root, p), s, 'utf8');
function set(s, key, value) {
  const re = new RegExp(`^  ${key}:.*(?:\\r?\\n(?!  [A-Za-z_][A-Za-z_0-9]*:|---|%)[^\\r\\n]+)*`, 'm');
  if (!re.test(s)) throw new Error(`Missing ${key}`);
  return s.replace(re, () => `  ${key}:${String(value).startsWith('\n') ? '' : ' '}${value}`);
}
const dir = 'Assets/Scriptable Obejects/Settlement/Otters';
const otters = new Map();
for (const f of fs.readdirSync(path.join(root, dir)).filter(f => f.endsWith('.asset'))) {
  const id = read(`${dir}/${f}`).match(/^  _otterId: (.+)$/m)?.[1].trim();
  const guid = read(`${dir}/${f}.meta`).match(/^guid: (.+)$/m)?.[1].trim();
  otters.set(id, guid);
}
let changed = 0;
function save(p, before, after) { if (before !== after) { write(p, after); changed++; } }
for (const q of spec.quests ?? []) {
  const p = `Assets/Scriptable Obejects/Quest/Quests/${q.asset}.asset`;
  const before = read(p);
  let s = set(before, '_title', JSON.stringify(q.title));
  s = set(s, '_description', JSON.stringify(q.description));
  s = set(s, '_goalType', q.goalType);
  s = set(s, '_goal', q.goal);
  s = set(s, '_target', q.target);
  s = set(s, '_itemFilter', '{fileID: 0}');
  if (q.iconFrom) {
    const icon = read(`Assets/Scriptable Obejects/Quest/Quests/${q.iconFrom}.asset`).match(/^  _icon: ([^\r\n]+)/m)?.[1];
    if (!icon) throw new Error(`Missing icon source: ${q.iconFrom}`);
    s = set(s, '_icon', icon);
  }
  save(p, before, s);
}
for (const r of spec.requests) {
  const p = `Assets/Scriptable Obejects/Settlement/Requests/${r.asset}.asset`;
  const before = read(p);
  let s = set(before, '_requiredDevelopment', r.requires);
  s = set(s, '_order', r.order);
  s = set(s, '_kingdomLevel', r.level);
  s = set(s, '_completionMessage', JSON.stringify(r.done));
  if (r.arrival && !otters.has(r.arrival)) throw new Error(`Missing arrival: ${r.arrival}`);
  s = set(s, '_arrivals', r.arrival ? `\n  - _otter: {fileID: 11400000, guid: ${otters.get(r.arrival)}, type: 2}\n    _state: 0` : '[]');
  save(p, before, s);
}
for (const r of spec.buildings) {
  const p = `Assets/Scriptable Obejects/Decor/Buildings/${r.asset}.asset`;
  const before = read(p);
  let s = set(before, '_requiredLevel', r.level);
  s = set(s, '_gold', r.gold);
  s = set(s, '_buildSeconds', r.seconds);
  let index = 0;
  const amounts = [r.wood, r.stone];
  const items = s.match(/^  _items:.*(?:\r?\n(?!  [A-Za-z_][A-Za-z_0-9]*:)[^\r\n]+)*/m)?.[0];
  if (!items) throw new Error(`Missing items: ${r.asset}`);
  const updated = items.replace(/(_amount: )\d+/g, (_, prefix) => prefix + amounts[index++]);
  if (index !== 2) throw new Error(`Expected wood + stone: ${r.asset}`);
  s = s.replace(items, updated);
  if (r.perHour > 0) {
    s = set(s, '_productionPerHour', r.perHour);
    s = set(s, '_description', JSON.stringify('한가한 특성 주민이 한 마리당 1분에 1개씩 모아요. 정찰·개간에 나간 주민은 생산을 쉬어요. 최대 8시간 보관해요.'));
  }
  save(p, before, s);
}
console.log(`Progression flow: ${changed} assets updated`);
