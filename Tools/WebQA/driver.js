const http = require('http'); const fs = require('fs'); const path = require('path');
// Playwright 위치는 컨테이너마다 다름: PLAYWRIGHT_PATH → 예전 경로 → 전역 설치(node22)
const PW = [process.env.PLAYWRIGHT_PATH, '/opt/node-tools/node_modules/playwright', '/opt/node22/lib/node_modules/playwright'].find(p => p && fs.existsSync(p)) || 'playwright';
const { chromium } = require(PW);
// 게임 칸 주소 (Unity Play 페이지가 아니라 그 안의 frame). 다른 빌드면 QA_URL로 바꿈
const URL_ = process.env.QA_URL || 'https://play-prod.struckd.com/api/v1/games/game/1b13367a-07d5-4ca7-80a9-9ff2598b3cc5/build/latest/frame';
// 브라우저 프로필(IndexedDB 세이브 포함) · 스크린샷이 남는 곳. 저장소 밖(스크래치패드 등)을 권장
const DIR = process.env.QA_DIR || path.join(__dirname, '.qa');
fs.mkdirSync(path.join(DIR, 'shots'), { recursive: true }); let page, ctx, logs = [];
(async () => {
  ctx = await chromium.launchPersistentContext(path.join(DIR, 'profile'), {
    headless: true, viewport: { width: 450, height: 900 }, deviceScaleFactor: 1,
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'],
  });
  page = ctx.pages()[0] || await ctx.newPage();
  page.on('console', m => { logs.push(`[${m.type()}] ${m.text()}`); if (logs.length > 3000) logs.splice(0, 1000); });
  page.on('pageerror', e => logs.push('[pageerror] ' + e.message));
  await page.goto(URL_, { waitUntil: 'domcontentloaded', timeout: 120000 });
  http.createServer(async (req, res) => {
    let body = ''; req.on('data', c => body += c); await new Promise(r => req.on('end', r));
    const q = body ? JSON.parse(body) : {}; let out = 'ok';
    try {
      const u = req.url.split('?')[0];
      if (u === '/shot') { const f = path.join(DIR, 'shots', (q.name || 'shot') + '.png'); await page.screenshot({ path: f }); out = f; }
      else if (u === '/click') { const hold = q.hold ?? 60; for (let i = 0; i < (q.n || 1); i++) { await page.mouse.move(q.x, q.y); await page.mouse.down(); await page.waitForTimeout(hold); await page.mouse.up(); await page.waitForTimeout(q.gap ?? 120); } }
      else if (u === '/drag') { const steps = q.steps || 20; await page.mouse.move(q.x1, q.y1); await page.mouse.down(); await page.waitForTimeout(80);
        for (let i = 1; i <= steps; i++) { await page.mouse.move(q.x1 + (q.x2 - q.x1) * i / steps, q.y1 + (q.y2 - q.y1) * i / steps); await page.waitForTimeout(q.dt ?? 30); }
        await page.waitForTimeout(150); await page.mouse.up(); }
      else if (u === '/wheel') { await page.mouse.move(q.x, q.y); await page.mouse.wheel(0, q.dy); }
      else if (u === '/eval') { out = JSON.stringify(await page.evaluate(q.js)); }
      else if (u === '/logs') { out = logs.slice(-(q.n || 50)).join('\n'); if (q.clear) logs = []; }
      else if (u === '/reload') { await page.goto(URL_, { waitUntil: 'domcontentloaded', timeout: 120000 }); }
      else if (u === '/wait') { await page.waitForTimeout(q.ms || 1000); }
      else if (u === '/quit') { res.end('bye'); await ctx.close(); process.exit(0); }
    } catch (e) { out = 'ERR ' + e.message; }
    res.end(String(out));
  }).listen(Number(process.env.QA_PORT || 8777), () => console.log('driver ready'));
})();
