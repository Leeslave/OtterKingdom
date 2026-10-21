// Non-destructive V14 walker variant. Crops generated parts, preserving their alpha.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..');
const art=path.join(root,'ArtSource/Otter/WalkerRig/V1');
const out=path.join(root,'Assets/Resources/WalkerRigV1');
(async()=>{
 fs.mkdirSync(out,{recursive:true});fs.mkdirSync(path.join(art,'parts'),{recursive:true});
 const atlas=path.join(art,'atlas.png');
 const images=await require('../PersonalityRig/extract.cjs')(atlas,art,[0,455,800,985,1205,1536],false);
 const rig=JSON.parse(fs.readFileSync(path.join(root,'ArtSource/Otter/QuarterRig/rig.json')));
 const approved=JSON.parse(fs.readFileSync(path.join(art,'approved-settings.json'),'utf8'));
 if(approved.version!==1)throw Error('Unsupported settings version');
 for(const a of Object.values(approved.adjustments))for(const k of ['x','y','width','height']){
  if(!Number.isFinite(a[k])||a[k]<(k==='x'||k==='y'?-100:40)||a[k]>(k==='x'||k==='y'?100:180))throw Error('Invalid setting '+k);
 }
 rig.approvedSettings=approved;
 const exported=[];
 for(const back of [false,true]){
  const key=back?'rearBones':'bones',prefix=back?'rear':'front';
  rig[key]=rig[key].filter(b=>b.name!=='backpack');
  for(const b of rig[key]){
   if(!b.part)continue;
   let type=b.name.endsWith('Arm')?'arm':b.name.endsWith('Leg')?'leg':b.name;
   if(type==='head'){b.width=270;b.height=215;b.pivot=[.5,.9];b.pos=[0,-83];}
   if(type==='body'){b.width=150;b.height=135;b.pivot=[.5,.6];}
   if(type==='arm'){
    const near=b.name==='nearArm';b.width=near?48:40;b.height=near?88:76;b.pivot=[.5,.15];
    b.pos=back?(near?[60,-54]:[-61,-53]):(near?[-60,-53]:[61,-51]);
   }
   if(type==='tail'){b.width=100;b.height=55;b.pivot=back?[.9,.23]:[.9,.55];}
   if(type==='leg'){const near=b.name==='nearLeg';b.width=60*(near?1:.94);b.height=76*(near?1:.94);b.pivot=[.55,1];}
   const a=approved.adjustments[b.name];
   let png=await sharp(images[prefix+'-'+type]).resize(Math.round(b.width*2*a.width/100),Math.round(b.height*2*a.height/100),{fit:'fill'}).png().toBuffer();
   let pivot=[...b.pivot];
   if(back&&b.name==='farArm'){png=await sharp(png).flop().png().toBuffer();pivot[0]=1-pivot[0];}
   const name=prefix+'-'+b.name;
   fs.writeFileSync(path.join(out,name+'.png'),png);
   exported.push({name,pivot:[pivot[0],1-pivot[1]]});
  }
 }
 rig.source='Attached walker-cap otter, image_gen atlas; V14 farmer animation and separate sleeve joints';
 const unityRig=structuredClone(rig);
 for(const key of ['bones','rearBones'])for(const b of unityRig[key]){
  const a=approved.adjustments[b.name];if(!a)continue;
  b.pos=[b.pos[0]+a.x,b.pos[1]+a.y];b.width*=a.width/100;b.height*=a.height/100;
 }
 fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(unityRig,null,2));
 fs.writeFileSync(path.join(art,'rig.json'),JSON.stringify(rig,null,2));
 fs.writeFileSync(path.join(art,'imports.json'),JSON.stringify(exported,null,2));
 const sources=Object.fromEntries(exported.map(p=>[p.name,'data:image/png;base64,'+fs.readFileSync(path.join(out,p.name+'.png')).toString('base64')]));
 let html=fs.readFileSync(path.join(root,'Tools/QuarterRig/preview.html'),'utf8');
 html=html.replace('/*__DATA__*/',`const RIG=${JSON.stringify(rig)};const SOURCES=${JSON.stringify(sources)};`);
 html=html.replace(/function drawLeg\([\s\S]*?(?=function render\()/,`function drawLeg(t,near,mats){
 const p=legPose(t,near,mats),im=imgs[(state.rear?'rear-':'front-')+(near?'nearLeg':'farLeg')];
 const a=RIG.tuning?.[near?'nearLeg':'farLeg']||{width:100,height:100};
 const w=60*(near?1:.94)*a.width/100,h=76*(near?1:.94)*a.height/100;
 ctx.save();ctx.translate(...p.sole);ctx.rotate(p.angle);ctx.drawImage(im,-w*.55,-h,w,h);ctx.restore();
 }
 function character(t,x,y,scale){
 ctx.save();ctx.translate(x,y);ctx.scale(state.flip?-scale:scale,scale);const mats=pose(t);
 for(const b of [...activeBones()].filter(b=>b.part).sort((a,b)=>a.layer-b.layer)){
 if(b.name.endsWith('Leg')){drawLeg(t,b.name==='nearLeg',mats);continue;}
 const im=imgs[(state.rear?'rear-':'front-')+b.name];
 ctx.save();ctx.transform(...mats[b.name]);ctx.drawImage(im,-b.pivot[0]*b.width,-b.pivot[1]*b.height,b.width,b.height);ctx.restore();
 }ctx.restore();
 }
 `);
 html=html.replace('<h1>앞으로, 뒤로</h1>','<h1>산책하는 해달, 앞으로 뒤로</h1>').replace('<title>','<title>산책하는 해달 · ');
 html=html.replace(/<p class="note">[\s\S]*?<\/p>/,'<p class="note">산책하는 해달 · 48번.<br>팔과 다리는 반대 박자로 움직이고,<br>꼬리가 살짝 늦게 따라와요.</p>');
 html=html.replace('<label><input id="bones"','<label hidden><input id="bones"');
 html=html.replace(/<footer>[\s\S]*?<\/footer>/,'<footer><input id="legacy" type="checkbox" hidden>산책하는 해달 앞·뒤 ¾뷰 · 농부 V14 걷기 동작. Plaza_RiggedPlacement 씬에도 같은 파츠와 리깅을 연결했습니다.</footer>');
 html=html.replace("setDirection('ne');","setDirection('se');");
 html=html.replace('state.explode?1.13:1.48','state.explode?.95:1.25');
 html=html.replace('window.renderAt=t=>{render(t);return pose(t)};', 'window.renderAt=t=>{clock=t;render(t);return pose(t)};');
 let tuning=fs.readFileSync(path.join(root,'Tools/StarterOtterRig/tuning.js'),'utf8')
  .replace('structuredClone(RIG.bones)', 'structuredClone({bones:RIG.bones,rearBones:RIG.rearBones})')
  .replace('otter-starter-v9-tuning-approved-20261013','otter-walker-v14-straight-approved-v2')
  .replace('for(const b of RIG.bones){const a=adjustments[b.name],base=tuningDefaults.find(x=>x.name===b.name);',
   'for(const view of ["bones","rearBones"])for(const b of RIG[view]){const a=adjustments[b.name],base=tuningDefaults[view].find(x=>x.name===b.name);')
  .replace("filter(([k])=>leg||!k.startsWith('paw'))","filter(([k])=>!k.startsWith('paw'))")
  .replaceAll('보내준 설정','기본 설정').replaceAll('보내주신 설정','기본 설정')
  .replace('starter-otter-v9-settings.json','walker-otter-v14-settings.json')
  .replace('상하 +는 아래쪽이에요.','상하 +는 아래쪽이에요. 앞·뒤 방향에 함께 적용되며 Unity 씬에는 자동 반영되지 않아요.');
 html=html.replace('</script>',()=>tuning+'\n</script>');
 fs.writeFileSync(path.join(art,'preview.html'),html);
 console.log('Exported '+exported.length+' walker parts');
})().catch(e=>{console.error(e);process.exit(1)});
