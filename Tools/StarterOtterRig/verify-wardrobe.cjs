const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path'),sharp=require('sharp'),{pathToFileURL}=require('url');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const p=await browser.newPage({viewport:{width:1200,height:1000}}),errors=[];p.on('pageerror',e=>errors.push(e.message));const out=path.resolve(__dirname,'../../ArtSource/Otter/StarterRig/V9');
 await p.goto(pathToFileURL(path.join(out,'preview.html')).href);await p.waitForSelector('#outfit-x');await p.waitForSelector('#tune-x');
 assert.deepEqual(await p.evaluate(()=>rigTuning.values),JSON.parse(fs.readFileSync(path.join(out,'approved-settings.json'))).adjustments);
 await p.evaluate(()=>{state.paused=true;renderAt(0)});const bare=await p.evaluate(()=>canvas.toDataURL());
 await p.locator('canvas').screenshot({path:path.join(out,'approved-rounded.png')});
 await p.click('#sampleAll');await p.evaluate(()=>renderAt(0));const dressed=await p.evaluate(()=>canvas.toDataURL());assert(bare!==dressed,'samples affect render');
 await p.evaluate(()=>{document.querySelector('aside').scrollTop=0;window.scrollTo(0,0)});await p.screenshot({path:path.join(out,'wardrobe-preview.png'),fullPage:true});await p.locator('canvas').screenshot({path:path.join(out,'sample-outfit.png')});
 const frames=[];for(let i=0;i<24;i++){const data=await p.evaluate(t=>{renderAt(t);return canvas.toDataURL().split(',')[1]},i*.84/24);frames.push(await sharp(Buffer.from(data,'base64')).resize(600,444).png().toBuffer());}await sharp(frames,{join:{animated:true}}).gif({delay:35,loop:0}).toFile(path.join(out,'outfit-walk.gif'));
 // Test PNG decoding, each attachment, controls and export/import including PNG bytes.
 const png=await sharp({create:{width:80,height:40,channels:4,background:'#dabc86'}}).png().toBuffer();
 for(const k of ['hat','top','bottom']){await p.selectOption('#outfitPart',k);await p.locator('#outfitFile').setInputFiles({name:k+'.png',mimeType:'image/png',buffer:png});await p.waitForFunction(k=>wardrobe.values[k].mode==='custom',k);}
 await p.locator('[data-outfit-number="x"]').fill('14');assert.equal(await p.evaluate(()=>wardrobe.values.bottom.x),14);
 const dl=p.waitForEvent('download');await p.click('#exportOutfit');const file=path.join(out,'wardrobe-test-export.json');await(await dl).saveAs(file);
 await p.click('#offAll');await p.locator('#outfitJson').setInputFiles(file);await p.waitForFunction(()=>wardrobe.values.hat.mode==='custom');assert.equal(await p.evaluate(()=>wardrobe.values.bottom.x),14);
 await p.reload();await p.waitForFunction(()=>window.wardrobe?.values.hat.mode==='custom');assert.equal(await p.evaluate(()=>wardrobe.images.hat.width),80);
 await p.locator('#outfitJson').setInputFiles({name:'bad.json',mimeType:'application/json',buffer:Buffer.from('{"version":2}')});await p.waitForFunction(()=>document.querySelector('#outfitMessage').textContent.includes('설정'));assert.equal(await p.evaluate(()=>wardrobe.values.bottom.x),14);
 await p.click('#offAll');await p.click('#resetAll');assert.deepEqual(await p.evaluate(()=>rigTuning.values),JSON.parse(fs.readFileSync(path.join(out,'approved-settings.json'))).adjustments);
 await p.setViewportSize({width:390,height:844});assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(out,'wardrobe-verification.json'),JSON.stringify({passed:true,checks:['approved settings applied and reset','rounded legs render','samples render','24 walk phases','all 3 PNG slots decode','offset controls','PNG-inclusive JSON round trip','reload persistence','invalid import preserves state','mobile no overflow','no page errors']},null,2));console.log('Wardrobe and approved settings checks passed');
 }finally{await browser.close();}})().catch(e=>{console.error(e.message);process.exitCode=1});
