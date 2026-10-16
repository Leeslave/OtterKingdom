const {chromium}=require('playwright'),assert=require('assert/strict'),path=require('path'),fs=require('fs'),{pathToFileURL}=require('url');
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});try{
 const p=await b.newPage({viewport:{width:1200,height:1000}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 const out=path.resolve(__dirname,'../../ArtSource/Otter/StarterRig/V9');await p.goto(pathToFileURL(path.join(out,'preview.html')).href);await p.waitForSelector('#tune-x');
 await p.click('#pause');const initial=await p.evaluate(()=>{renderAt(0);return canvas.toDataURL()});
 async function set(k,v){await p.locator(`[data-number="${k}"]`).fill(String(v));}
 await set('x',18);await set('width',130);
 assert.equal(await p.evaluate(()=>rigStudy.rig.bones.find(b=>b.name==='nearArm').pos[0]),-47);
 assert.equal(await p.evaluate(()=>rigStudy.rig.bones.find(b=>b.name==='nearArm').width),71.5);
 const changed=await p.evaluate(()=>{renderAt(0);return canvas.toDataURL()});assert.notEqual(changed,initial);
 await p.locator('#tunePart').selectOption('nearLeg');await set('y',12);await set('pawWidth',150);
 assert.equal(await p.evaluate(()=>legDiagnostics(0)[0].sole[1]),12);
 const downloadPromise=p.waitForEvent('download');await p.click('#saveTuning');const download=await downloadPromise;const exportFile=path.join(out,'tuning-test-export.json');await download.saveAs(exportFile);
 await p.reload();await p.waitForSelector('#tune-x');assert.equal(await p.evaluate(()=>rigTuning.values.nearArm.x),18);assert.equal(await p.evaluate(()=>rigTuning.values.nearLeg.pawWidth),150);
 await p.click('#resetAll');assert.equal(await p.evaluate(()=>rigTuning.values.nearArm.x),0);
 await p.locator('#tuneFile').setInputFiles(exportFile);await p.waitForFunction(()=>rigTuning.values.nearArm.x===18);
 await p.locator('#tuneFile').setInputFiles({name:'bad.json',mimeType:'application/json',buffer:Buffer.from('{"version":1,"adjustments":{}}')});await p.waitForFunction(()=>document.getElementById('tuneMessage').textContent.includes('올바르지'));assert.equal(await p.evaluate(()=>rigTuning.values.nearArm.x),18);
 await p.click('#resetAll');await p.click('#pause');await p.evaluate(()=>renderAt(0));assert.deepEqual(await p.evaluate(()=>rigTuning.values),Object.fromEntries(['nearArm','farArm','nearLeg','farLeg','body','head','tail'].map(k=>[k,{x:0,y:0,width:100,height:100,pawWidth:100,pawHeight:100}])));
 await p.locator('#tunePart').selectOption('body');await set('width',120);await set('height',90);assert.equal(await p.evaluate(()=>rigStudy.rig.bones.find(b=>b.name==='body').width),204);
 await p.click('#resetPart');assert.equal(await p.evaluate(()=>rigStudy.rig.bones.find(b=>b.name==='body').width),170);
 await p.screenshot({path:path.join(out,'tuning-preview.png'),fullPage:true});
 await p.setViewportSize({width:390,height:844});assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(out,'tuning-verification.json'),JSON.stringify({passed:true,checks:['arm position/scale affects render','leg sole offset and paw scale','reload restores settings','JSON export/import round trip','invalid import preserves state','reset restores all default values','body resize/reset','390px layout','no page errors']},null,2));console.log('Tuning checks passed');
 }finally{await b.close();}})().catch(e=>{console.error(e);process.exitCode=1});

