const {chromium}=require('playwright'),sharp=require('sharp'),fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const {pathToFileURL}=require('url');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtSource/Otter/StarterRig/V9'),base=path.join(root,'ArtSource/Otter/QuarterRig/Baseline-V9');
async function main(){
 const rig=JSON.parse(fs.readFileSync(path.join(out,'rig.json'))),ref=JSON.parse(fs.readFileSync(path.join(base,'rig.json')));
 assert.deepEqual(rig.legSolver,ref.legSolver);
 const original=fs.readFileSync(path.join(base,'preview.html'),'utf8'),updated=fs.readFileSync(path.join(out,'preview.html'),'utf8');
 const motions=s=>s.slice(s.indexOf('function pose(t)'),s.indexOf('function drawLeg('));
 assert.equal(motions(updated),motions(original),'V9 pose/legPose must stay byte-identical');
 for(const b of rig.bones){const r=ref.bones.find(x=>x.name===b.name);for(const k of ['parent','pos','width','pivot','layer'])assert.deepEqual(b[k],r[k]);}
 const provenance=JSON.parse(fs.readFileSync(path.join(out,'provenance.json')));
 for(const [f,h]of Object.entries(provenance.baselineHashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(base,f))).digest('hex'),h);
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
 const page=await browser.newPage({viewport:{width:1200,height:960}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(path.join(out,'preview.html')).href);await page.evaluate(()=>window.rigReady);await page.click('#pause');
 await page.evaluate(()=>renderAt(0));await page.screenshot({path:path.join(out,'preview.png'),fullPage:true});
 const checks=await page.evaluate(()=>{
  let maxGroundError=0,maxSlideError=0,checks=0;const dt=1e-5,cfg=rigStudy.rig.legSolver;
  for(const stride of [0,.5,1,1.5]){rigStudy.state.stride=stride;
   for(let i=0;i<168;i++){const t=i*cfg.cycleSeconds/168;const a=legDiagnostics(t),b=legDiagnostics(t+dt);
    for(let j=0;j<2;j++)if(a[j].stance){maxGroundError=Math.max(maxGroundError,Math.abs(a[j].sole[1]-a[j].floor));if(b[j].stance){const velocity=(b[j].sole[0]-a[j].sole[0])/dt;maxSlideError=Math.max(maxSlideError,Math.abs(velocity+2*cfg.stepHalfWidth*stride/(cfg.stanceFraction*cfg.cycleSeconds)));}checks++;}
   }
  }rigStudy.state.stride=1;return{maxGroundError,maxSlideError,checks};
 });
 assert(checks.maxGroundError<1e-8);assert(checks.maxSlideError<1e-5);
 const animation=[],samples=[];
 for(let i=0;i<24;i++){
  const t=i*.84/24,data=await page.evaluate(t=>{renderAt(t);return canvas.toDataURL().split(',')[1]},t);
  const frame=await sharp(Buffer.from(data,'base64')).resize(600,444).png().toBuffer();animation.push(frame);
  if(i%6===0){fs.writeFileSync(path.join(out,'walk-phase-'+i+'.png'),frame);samples.push(frame);}
 }
 await sharp(animation,{join:{animated:true}}).gif({delay:35,loop:0}).toFile(path.join(out,'walk.gif'));
 const thumbs=await Promise.all(samples.map(x=>sharp(x).resize(300,222).toBuffer()));
 await sharp({create:{width:1200,height:222,channels:4,background:'#e8eddf'}}).composite(thumbs.map((input,i)=>({input,left:i*300,top:0}))).png().toFile(path.join(out,'walk-phases.png'));
 await page.click('#idle');await page.evaluate(()=>renderAt(.5));await page.locator('canvas').screenshot({path:path.join(out,'idle.png')});
 await page.click('#bones');await page.click('#explode');await page.evaluate(()=>renderAt(0));await page.screenshot({path:path.join(out,'rig-inspection.png'),fullPage:true});
 await page.click('#bones');await page.click('#explode');await page.click('#flip');await page.click('#small');await page.click('#walk');await page.evaluate(()=>renderAt(.21));await page.locator('canvas').screenshot({path:path.join(out,'flipped-small.png')});
 await page.locator('#speed').fill('0.5');await page.locator('#speed').dispatchEvent('input');assert.equal(await page.locator('#speedVal').textContent(),'0.50×');
 await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,baselineUnchanged:true,exactV9LegSolver:true,byteIdenticalMotionFunctions:true,bonePositionsPreserved:true,checks,walkFrames:24,pageErrors:errors,controlsChecked:['walk','idle','pause','speed','bones','explode','flip','small'],mobileWidth:390},null,2));
 console.log(JSON.stringify({passed:true,...checks}));
 }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});
