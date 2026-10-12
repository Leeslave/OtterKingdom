// Headless visual QA and a frame sequence from the actual preview renderer.
const {chromium}=require('playwright');
const path=require('path'),fs=require('fs');
const {pathToFileURL}=require('url');
(async()=>{
const out=path.resolve(__dirname,'../../ArtSource/Otter/QuarterRig');
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const page=await browser.newPage({viewport:{width:1180,height:950},deviceScaleFactor:1});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto(pathToFileURL(path.join(out,'preview.html')).href);
await page.evaluate(()=>window.rigReady);
await page.click('#pause');
await page.evaluate(()=>window.renderAt(0));
await page.screenshot({path:path.join(out,'preview.png')});
await page.click('#idle');
if(!(await page.locator('#status').innerText()).includes('대기'))throw Error('Idle toggle failed');
await page.click('#walk');
for(const id of ['bones','explode','flip','small','legacy']){await page.check('#'+id);await page.evaluate(()=>window.renderAt(.21));await page.uncheck('#'+id);}
const diagnostics=await page.evaluate(()=>{
 const failures=[];
 for(const stride of [0,1,1.5]){
  const slider=document.querySelector('#stride');slider.value=stride;slider.dispatchEvent(new Event('input'));
  for(let i=0;i<120;i++)for(const p of window.legDiagnostics(i*.84/120)){
   if(!p.sole.every(Number.isFinite))failures.push('Invalid sole position');
   if(p.stance&&(Math.abs(p.angle)>1e-8||Math.abs(p.sole[1]-p.floor)>1e-6))failures.push('Planted sole drift');
  }
 }
 const slider=document.querySelector('#stride');slider.value=1;slider.dispatchEvent(new Event('input'));
 return failures;
});
if(diagnostics.length)throw Error(diagnostics.slice(0,5).join(', '));
const frames=path.resolve(__dirname,'../../tmp/quarter-rig-frames');fs.mkdirSync(frames,{recursive:true});
// Keep the RAF loop from replacing explicit sample times during capture.
await page.evaluate(()=>{window.requestAnimationFrame=()=>0;});
await page.waitForTimeout(100);
for(let i=0;i<24;i++){
 await page.evaluate(t=>window.renderAt(t),i*.84/24);
 const data=await page.locator('canvas').evaluate(c=>c.toDataURL('image/png').split(',')[1]);
 fs.writeFileSync(path.join(frames,`${String(i).padStart(2,'0')}.png`),Buffer.from(data,'base64'));
}
await browser.close();if(errors.length)throw Error(errors.join('\n'));console.log('PASS: load, controls, 24 animation frames, no browser errors.');
})().catch(e=>{console.error(e);process.exit(1)});
