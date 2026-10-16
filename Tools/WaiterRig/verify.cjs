const {chromium}=require('playwright'),assert=require('assert/strict'),fs=require('fs'),path=require('path'),sharp=require('sharp'),{pathToFileURL}=require('url'),crypto=require('crypto');
const out=path.resolve(__dirname,'../../ArtSource/Otter/WaiterRig/V9'),base=path.resolve(__dirname,'../../ArtSource/Otter/QuarterRig/Baseline-V9');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const rig=JSON.parse(fs.readFileSync(path.join(out,'rig.json'))),old=JSON.parse(fs.readFileSync(path.join(base,'rig.json')));assert.deepEqual(rig.legSolver,old.legSolver);
 const source=fs.readFileSync(path.join(out,'preview.html'),'utf8'),original=fs.readFileSync(path.join(base,'preview.html'),'utf8'),motion=s=>s.slice(s.indexOf('function pose(t)'),s.indexOf('function drawLeg('));assert.equal(motion(source),motion(original));
 const provenance=JSON.parse(fs.readFileSync(path.join(out,'provenance.json')));for(const [f,h]of Object.entries(provenance.hashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(base,f))).digest('hex'),h);
 const p=await browser.newPage({viewport:{width:1200,height:1000}}),errors=[];p.on('pageerror',e=>errors.push(e.message));await p.goto(pathToFileURL(path.join(out,'preview.html')).href);await p.waitForSelector('#tune-x');await p.evaluate(()=>{state.paused=true;renderAt(0)});
 await p.screenshot({path:path.join(out,'preview.png'),fullPage:true});
 const rendered={};
 for(const appearance of ['waiter','bare']){
  await p.click('#'+appearance);const frames=[];for(let i=0;i<24;i++){const data=await p.evaluate(t=>{renderAt(t);return canvas.toDataURL().split(',')[1]},i*.84/24);frames.push(await sharp(Buffer.from(data,'base64')).resize(600,444).png().toBuffer());}
  await sharp(frames,{join:{animated:true}}).gif({delay:35,loop:0}).toFile(path.join(out,appearance+'-walk.gif'));await p.evaluate(()=>renderAt(0));await p.locator('canvas').screenshot({path:path.join(out,appearance+'.png')});rendered[appearance]=await p.evaluate(()=>canvas.toDataURL());
  const thumbs=await Promise.all([0,6,12,18].map(i=>sharp(frames[i]).resize(300,222).toBuffer()));await sharp({create:{width:1200,height:222,channels:4,background:'#e8eddf'}}).composite(thumbs.map((input,i)=>({input,left:i*300,top:0}))).png().toFile(path.join(out,appearance+'-phases.png'));
 }
 assert(rendered.bare!==rendered.waiter);
 const contact=await p.evaluate(()=>{let error=0,n=0;for(let i=0;i<240;i++)for(const foot of legDiagnostics(i*.84/240))if(foot.stance){error=Math.max(error,Math.abs(foot.sole[1]-foot.floor));n++;}return{error,n}});assert.equal(contact.error,0);
 await p.click('#waiter');await p.click('#idle');await p.click('#bones');await p.click('#explode');await p.evaluate(()=>renderAt(0));await p.locator('canvas').screenshot({path:path.join(out,'inspection.png')});await p.click('#explode');await p.click('#bones');await p.click('#flip');await p.click('#small');await p.evaluate(()=>renderAt(.21));await p.locator('canvas').screenshot({path:path.join(out,'flipped.png')});
 await p.locator('[data-number="x"]').fill('10');assert.equal(await p.evaluate(()=>rigTuning.values.nearArm.x),10);await p.click('#resetAll');assert.equal(await p.evaluate(()=>rigTuning.values.nearArm.x),0);
 await p.setViewportSize({width:390,height:844});assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,unchangedBaseline:true,exactV9Motion:true,contact,walkFramesPerAppearance:24,bareWaiterSwitch:true,controls:['idle','walk','flip','small','bones','explode','tuning','reset'],mobileWidth:390,pageErrors:errors},null,2));console.log('Waiter rig checks passed');
 }finally{await browser.close();}})().catch(e=>{console.error(e.message);process.exitCode=1});
