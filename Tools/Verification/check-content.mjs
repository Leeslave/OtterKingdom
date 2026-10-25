import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const read = p => fs.readFileSync(p, 'utf8');
function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(e => e.isDirectory() ? walk(path.join(dir, e.name)) : [path.join(dir, e.name)]);
}
const value = (s, field) => s.match(new RegExp(`^  ${field}: ([^\\r\\n]*)`, 'm'))?.[1].trim() ?? '';
const files = walk(path.join(root, 'Assets'));
const guids = new Map();
for (const p of files.filter(p => p.endsWith('.meta'))) {
  const guid = read(p).match(/^guid: ([a-f0-9]{32})/m)?.[1];
  if (guid) guids.set(guid, p.slice(0, -5));
}
const assets = files.filter(p => p.endsWith('.asset') && p.includes('Scriptable Obejects'));
const byPath = new Map(assets.map(p => [p, read(p)]));
const missing = [];
for (const [p, s] of byPath) {
  for (const [, guid] of s.matchAll(/guid: ([a-f0-9]{32})/g))
    if (!guid.startsWith('00000000') && !guids.has(guid)) missing.push(`${path.relative(root, p)} -> ${guid}`);
}
assert.equal(missing.length, 0, `Dangling references:\n${missing.slice(0, 15).join('\n')}`);
const db = read(path.join(root, 'Assets/Scriptable Obejects/Quest/QuestDatabase.asset'));
const quests = [...db.matchAll(/guid: ([a-f0-9]{32})/g)].map(m => guids.get(m[1]))
  .filter(p => byPath.has(p) && value(byPath.get(p), '_questId'));
const questSet = new Set(quests);
const ids = new Set();
for (const p of quests) {
  const s = byPath.get(p), id = value(s, '_questId');
  assert(!ids.has(id), `Duplicate quest ID: ${id}`); ids.add(id);
  const visited = new Set([p]);
  let cursor = p;
  while (cursor) {
    const text = byPath.get(cursor);
    const guid = value(text, '_prerequisite').match(/guid: ([a-f0-9]{32})/)?.[1];
    if (!guid) break;
    const previous = guids.get(guid);
    assert(questSet.has(previous), `Unregistered prerequisite for ${id}`);
    assert(!visited.has(previous), `Quest cycle for ${id}`);
    if (Number(value(text, '_kind')) === 0)
      assert(Number(value(byPath.get(previous), '_requiredLevel')) <= Number(value(text, '_requiredLevel')), `Prerequisite at a later level: ${id}`);
    visited.add(previous); cursor = previous;
  }
}
for (let level = 16; level < 50; level++)
  assert(quests.some(p => Number(value(byPath.get(p), '_requiredLevel')) === level && Number(value(byPath.get(p), '_kind')) === 0), `No main quest at Lv.${level}`);
const spec = JSON.parse(read(path.join(root, 'Tools/Progression/flow.json')));
for (const q of spec.quests ?? []) {
  const s = read(path.join(root, `Assets/Scriptable Obejects/Quest/Quests/${q.asset}.asset`));
  assert.equal(Number(value(s, '_goalType')), q.goalType, q.asset);
  assert.equal(Number(value(s, '_goal')), q.goal, q.asset);
  assert.equal(value(s, '_target'), q.target, q.asset);
}
const requests = new Map();
for (const r of spec.requests) {
  const s = read(path.join(root, `Assets/Scriptable Obejects/Settlement/Requests/${r.asset}.asset`));
  assert.equal(value(s, '_requiredDevelopment'), r.requires, r.asset);
  assert.equal(Number(value(s, '_kingdomLevel')), r.level, r.asset);
  requests.set(value(s, '_resultDevelopment'), r);
}
// 생산 기록은 대응 시설의 완공 기록을 필요로 한다. 새 표에 순환 선행 조건이 생기지 않아야 한다.
const productionParents = { produced_bld_workshop: 'p4_workshop_built', produced_bld_quarry: 'p4_quarry_built' };
for (const r of spec.requests) {
  const visited = new Set([r.asset]);
  let next = requests.get(productionParents[r.requires] ?? r.requires);
  while (next) {
    assert(!visited.has(next.asset), `Story cycle: ${r.asset}`);
    visited.add(next.asset);
    next = requests.get(productionParents[next.requires] ?? next.requires);
  }
}
for (const b of spec.buildings) {
  const s = read(path.join(root, `Assets/Scriptable Obejects/Decor/Buildings/${b.asset}.asset`));
  assert.equal(Number(value(s, '_requiredLevel')), b.level, b.asset);
  if (b.requires !== undefined) assert.equal(value(s, '_requiredDevelopment'), b.requires, b.asset);
  if (b.noTraits) assert.equal(value(s, '_traits'), '[]', b.asset);
  assert.equal(Number(value(s, '_gold')), b.gold, b.asset);
  const request = spec.requests.find(r => r.asset === ({Building_Workshop:'req_p4_workshop',Building_Quarry:'req_p4_quarry',Building_Granary:'req_p4_granary',Building_Shop:'req_p4_shop',Building_Workyard:'req_p5_workyard',Building_Fishery:'req_p5_fishery',Building_Archive:'req_p5_archive'}[b.asset]));
  assert(request.level > b.level, `${b.asset} must be buildable before its level reward`);
}
const photo = read(path.join(root, 'Assets/Scriptable Obejects/Decor/Buildings/Building_PhotoStudio.asset'));
assert.equal(Number(value(photo, '_requiredCollectionPercent')), 50);
console.log(`Content checks: ${assets.length} data assets have valid references; ${quests.length} quests have unique IDs and acyclic prerequisites; Lv.16–49 coverage; ${spec.requests.length} story steps; ${spec.buildings.length} buildings; photo studio 50% passed`);

// Resource prices must agree in the bag and in the mine's direct-sale model.
for (const item of ['Inventory/Items/Mining/목재', 'Inventory/Items/Mining/돌', 'MineRework/Ores/Ore_ore_stone']) {
  const asset = read(path.join(root, `Assets/Scriptable Obejects/${item}.asset`));
  assert.equal(Number(value(asset, '_sellPrice')), 3, `${item}: resource sell price`);
}
const shop = spec.buildings.find(b => b.asset === 'Building_Shop');
assert.equal(shop.level, 9);
assert.equal(spec.requests.find(r => r.asset === 'req_p4_shop').level, shop.level + 1, 'Shop milestone must allow reaching its construction level');
assert.equal(spec.buildings.find(b => b.asset === 'Building_Workshop').perHour, 30);
for (const file of assets.filter(p => /Project_territory_.*_mission\.asset$/.test(p))) {
  const tier = Number(file.match(/_(\d)_mission\.asset$/)[1]);
  assert.equal(Number(value(byPath.get(file), '_gold')), [0, 980, 2450, 4900][tier], `${file}: clearing gold`);
}
assert.equal(Number(value(read(path.join(root, 'Assets/Scriptable Obejects/Settlement/Tasks/task_farm_till.asset')), '_requiredGold')), 140);
console.log('Progression balance: resource sell prices, Lv.9 shop, workshop rate and 30% clearing discount passed');

const requestOrder = id => Number(value(read(path.join(root, `Assets/Scriptable Obejects/Settlement/Requests/${id}.asset`)), '_order'));
assert(requestOrder('req_p4_granary') < requestOrder('req_fishing_dock'), 'Granary must precede dock event');
assert(requestOrder('req_fishing_dock') < requestOrder('req_assign_fisher'), 'Dock must precede fisher assignment');
console.log('Dock progression: granary precedes dock and fisher assignment passed');

const granaryRequest = spec.requests.find(r => r.asset === 'req_p4_granary');
const granaryBuilding = spec.buildings.find(b => b.asset === 'Building_Granary');
assert.equal(granaryRequest.requires, 'farmland', 'Granary event must unlock with farmland');
assert.equal(granaryBuilding.requires, 'farmland', 'Granary construction must not depend on farmer settlement');
assert.equal(granaryBuilding.level, 0, 'Granary construction must be immediately available');
assert.equal(granaryBuilding.noTraits, true, 'Granary construction must not wait for extra farmers');
for (const id of ['req_p5_builder_home', 'req_p5_workyard'])
  assert(granaryRequest.order < requestOrder(id), `Granary must precede ${id}`);
console.log('Granary progression: farmland unlocks event and construction before workyard; no extra level or trait gate passed');

const granaryQuest = read(path.join(root, 'Assets/Scriptable Obejects/Quest/Quests/M13_Granary.asset'));
assert.equal(Number(value(granaryQuest, '_requiredLevel')), 10, 'Granary quest must be available at the farm unlock level');
const farmlandQuestPath = path.join(root, 'Assets/Scriptable Obejects/Quest/Quests/M04_Farmland.asset');
const granaryPrevious = value(granaryQuest, '_prerequisite').match(/guid: ([a-f0-9]{32})/)[1];
assert.equal(guids.get(granaryPrevious), farmlandQuestPath, 'Granary quest must immediately follow farmland');
console.log('Granary main quest: Lv.10, immediately after farmland passed');
