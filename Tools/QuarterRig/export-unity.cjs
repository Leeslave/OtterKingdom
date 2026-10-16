// Bake only the part artwork; Unity retains the V14 hierarchy and animation.
// Parts are drawn on 1024 canvases centred on their joint, then crop_unity_parts.py
// trims them and writes Sprite import settings with that joint as the pivot.
const {chromium}=require('playwright');
const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
(async()=>{
 const out=path.resolve(__dirname,'../../Assets/Resources/QuarterRigV14');
 fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try {
 const page=await browser.newPage();
 await page.goto(pathToFileURL(path.resolve(__dirname,'../../ArtSource/Otter/QuarterRig/preview.html')).href);
 await page.evaluate(()=>window.rigReady);
 const result=await page.evaluate(()=>{
  const parts=[];
  for(const rear of [false,true]) for(const b of rear?RIG.rearBones:RIG.bones){
   if(!b.part)continue;
   const canvas=document.createElement('canvas');canvas.width=canvas.height=1024;
   const c=canvas.getContext('2d');c.scale(2,2);c.translate(256,256);
   if(b.name.endsWith('Leg')){
    const near=b.name==='nearLeg',w=60*(near?1:.94),h=76*(near?1:.94),im=rear?imgs.rearLeg:imgs.solidLeg,start=rear?.73:.78;
    c.drawImage(im,0,im.height*start,im.width,im.height*(1-start),-w*.55,-h*.22,w,h*.22);
    c.fillStyle=rear?'#628767':'#698653';c.strokeStyle='#301007';c.lineWidth=5.2;
    c.beginPath();c.roundRect(-w*.51+.85,-h+2.85,w*.85-1.7,h*.80-1.7,10);c.fill();c.stroke();
   }else{
    if(rear&&b.name==='farArm')c.scale(-1,1);
    if(b.name==='body'){
     c.fillStyle=rear?'#628767':'#73976a';
     if(rear){c.beginPath();c.roundRect(-59,-6,118,51,17);c.fill();}
     else c.fillRect(-60,-30,62,54);
    }
    const im=imgs[b.part],h=b.height??b.width*im.height/im.width;
    c.drawImage(im,-b.pivot[0]*b.width,-b.pivot[1]*h,b.width,h);
   }
   parts.push({name:(rear?'rear-':'front-')+b.name,png:canvas.toDataURL().split(',')[1]});
  }
  return {rig:RIG,parts};
 });
 fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(result.rig,null,2));
 for(const p of result.parts)fs.writeFileSync(path.join(out,p.name+'.png'),Buffer.from(p.png,'base64'));
 console.log('Exported '+result.parts.length+' V14 rig parts');
 require('child_process').execFileSync('python',[path.join(__dirname,'crop_unity_parts.py')],{stdio:'inherit'});
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
