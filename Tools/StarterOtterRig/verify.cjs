const {chromium}=require('playwright');
const sharp=require('sharp');
const fs=require('fs'),path=require('path');
const {pathToFileURL}=require('url');
const out=path.resolve(__dirname,'../../ArtSource/Otter/StarterRig');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
 const page=await browser.newPage({viewport:{width:1200,height:960},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(path.join(out,'preview.html')).href);
 await page.waitForFunction(()=>window.rigReady===true);
 await page.click('#pause');
 await page.evaluate(()=>window.rigPreview.renderAt(0));
 await page.screenshot({path:path.join(out,'preview.png'),fullPage:true});
 const frames=[];
 for(const mode of ['idle','walk','wave']){
  await page.click('[data-mode="'+mode+'"]');
  for(const t of [0,.2125,.425,.6375]){
   const positions=await page.evaluate(t=>{window.rigPreview.renderAt(t);return window.rigPreview.matrices(t);},t);
   if(Object.values(positions).flat().some(v=>!Number.isFinite(v)))throw new Error('Nonfinite transform');
   const filename=mode+'-'+t+'.png';await page.locator('#stage').screenshot({path:path.join(out,filename)});frames.push(filename);
  }
 }
 // Render the actual rig poses into a portable animated preview.
 const animation=[];
 for(const mode of ['idle','walk','wave']){
  await page.click('[data-mode="'+mode+'"]');
  for(let i=0;i<24;i++){
   const data=await page.evaluate(t=>{window.rigPreview.renderAt(t);return document.getElementById('stage').toDataURL('image/png').split(',')[1];},i/12);
   animation.push(await sharp(Buffer.from(data,'base64')).resize(480,370).png().toBuffer());
  }
 }
 await sharp(animation,{join:{animated:true}}).gif({delay:83,loop:0}).toFile(path.join(out,'motion-preview.gif'));
 await page.click('#bones');await page.click('#explode');await page.click('#slots');
 await page.evaluate(()=>window.rigPreview.renderAt(0));
 await page.screenshot({path:path.join(out,'rig-inspection.png'),fullPage:true});
 await page.click('#explode');await page.click('#bones');await page.click('#slots');await page.click('#small');
 await page.evaluate(()=>window.rigPreview.renderAt(.2));
 await page.locator('#stage').screenshot({path:path.join(out,'game-size.png')});
 await page.setViewportSize({width:390,height:844});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 if(overflow)throw new Error('Mobile horizontal overflow');
 if(errors.length)throw new Error(errors.join('\n'));
 fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({passed:true,browser:'Chromium headless',clips:['idle','walk','wave'],phaseSamples:4,finiteTransforms:true,controls:true,mobileWidth:390,noHorizontalOverflow:true,pageErrors:errors,frames},null,2));
 console.log('Verified 3 clips, 12 phase samples, controls, mobile layout; wrote preview and inspection images.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
