const {chromium}=require('playwright'),fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
(async()=>{
 const out=path.resolve(__dirname,'../../ArtSource/Otter/PlayerRig/V1');
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
 const page=await browser.newPage({viewport:{width:1200,height:950}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(path.join(out,'preview.html')).href);await page.evaluate(()=>window.rigReady);
 await page.evaluate(()=>{state.paused=true;window.requestAnimationFrame=()=>0;});await page.waitForTimeout(100);
 for(const dir of ['se','sw','ne','nw']){
  await page.evaluate(d=>{setDirection(d);renderAt(.21)},dir);
  const png=await page.evaluate(()=>document.querySelector('canvas').toDataURL().split(',')[1]);
  fs.writeFileSync(path.join(out,dir+'.png'),Buffer.from(png,'base64'));
 }
 await page.selectOption('#tunePart','head');await page.locator('[data-number="width"]').fill('110');await page.locator('[data-number="width"]').dispatchEvent('input');
 if(await page.evaluate(()=>rigTuning.values.head.width)!==110)throw Error('Tuning');await page.click('#resetAll');
 const checks=await page.evaluate(()=>{
 let error=0;
 for(let i=0;i<100;i++)for(const p of legDiagnostics(i*.0084))if(p.stance)error=Math.max(error,Math.abs(p.sole[1]-p.floor));
 return {plantedFootError:error,parts:Object.keys(imgs).length};
 });
 if(errors.length||checks.plantedFootError>1e-6||checks.parts!==14)throw Error(JSON.stringify({errors,checks}));
 fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({errors,...checks},null,2));console.log(checks);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
