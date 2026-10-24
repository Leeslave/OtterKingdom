// 현재 에셋에서 계산한 수급 지표. 플레이 시간이나 체감 난이도를 측정한 결과가 아니다.
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
const read = relative => fs.readFileSync(fileURLToPath(new URL('../../' + relative, import.meta.url)), 'utf8');
const value = (s, name) => Number(s.match(new RegExp(`^  ${name}: ([^\\r\\n]+)`, 'm'))?.[1]);
const levels = read('Assets/Scriptable Obejects/Profile/LevelTable.asset');
const rewards = [...levels.matchAll(/_rewardAmount: (\d+)/g)].map(m => Number(m[1]));
const banner = read('Assets/Scriptable Obejects/Gacha/Banners/Banner_Standard.asset');
const cost = value(banner, '_gemCost');
const flow = JSON.parse(read('Tools/Progression/flow.json'));
const report = {
  basis: 'Current asset arithmetic only; quests, gifts, exchanges, daily income and live play time are excluded',
  levelRewards: [10, 15, 20, 50].map(level => {
    const shells = rewards.slice(0, level - 1).reduce((a, b) => a + b, 0);
    return { level, shells, singlePulls: Math.floor(shells / cost) };
  }),
  dailyGoldPulls: [6, 8, 15, 20, 50].map(level => ({ level, gold: value(banner, '_goldPullBase') + value(banner, '_goldPullPerLevel') * level })),
  earlyFacilities: flow.buildings.filter(b => b.perHour > 0).map(b => ({
    building: b.asset, level: b.level, gold: b.gold,
    firstItemSecondsWithOneIdleWorker: 3600 / b.perHour,
    itemsAfterEightHoursWithOneIdleWorker: b.perHour * 8
  }))
};
console.log(JSON.stringify(report, null, 2));
