const fs=require('fs'),path=require('path'),sharp=require('sharp'),{chromium}=require('playwright'),{pathToFileURL}=require('url');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtSource/Otter/HeadRefresh/V1'),cast=require('../../ArtSource/Otter/HeadRefresh/V1/manifest.json');
(async()=>{const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true}),results=[],cells=[];try{
const page=await browser.newPage({viewport:{width:1200,height:950}});let errors=[];page.on('pageerror',e=>errors.push(e.message));
for(const [i,c]of cast.entries()){
console.log(c.name);errors=[];await page.goto(pathToFileURL(path.join(root,c.art,'preview.html')).href);await page.evaluate(()=>window.rigReady);if(errors.length)throw Error(c.name+errors.join());await page.evaluate(()=>{state.paused=true;window.requestAnimationFrame=()=>0;});await page.waitForTimeout(30);
for(const dir of ['se','sw','ne','nw']){await page.evaluate(d=>{setDirection(d);renderAt(.21)},dir);if(dir==='se'){const png=Buffer.from(await page.evaluate(()=>document.querySelector('canvas').toDataURL().split(',')[1]),'base64');fs.writeFileSync(path.join(out,c.name+'.png'),png);cells.push({input:await sharp(png).resize(250,220,{fit:'cover'}).png().toBuffer(),left:i%6*250,top:Math.floor(i/6)*220});}}
if(errors.length)throw Error(c.name+errors.join());results.push({name:c.name,directions:4,errors});
}await sharp({create:{width:1500,height:1320,channels:4,background:'#e8eee0'}}).composite(cells).png().toFile(path.join(out,'overview.png'));
fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(results,null,2));console.log('PASS: '+results.length+' previews, '+(results.length*4)+' direction renders.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1)});
