const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtSource/Otter/WaiterRig/V9'),base=path.join(root,'ArtSource/Otter/QuarterRig/Baseline-V9');
async function main(){
 fs.mkdirSync(path.join(out,'parts'),{recursive:true});
 const regions=JSON.parse(fs.readFileSync(path.join(out,'atlas-regions.json'))),images={},parts={};
 for(const [name,[left,top,width,height]]of Object.entries(regions)){
  const {data,info}=await sharp(path.join(out,'atlas.png')).extract({left,top,width,height}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=width,y0=height,x1=-1,y1=-1;
  for(let y=0;y<height;y++)for(let x=0;x<width;x++)if(data[(y*width+x)*4+3]>12){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
  if(x1<0)throw Error('Empty '+name);
  const rect={left:left+x0,top:top+y0,width:x1-x0+1,height:y1-y0+1};
  const png=await sharp(path.join(out,'atlas.png')).extract(rect).png().toBuffer();
  fs.writeFileSync(path.join(out,'parts',name+'.png'),png);images[name]='data:image/png;base64,'+png.toString('base64');parts[name]=rect;
 }
 images.body=images.bodyWaiter;images.arm=images.armWaiter;images.leg=images.solidLeg;
 const rig=JSON.parse(fs.readFileSync(path.join(base,'rig.json')));
 rig.bones=rig.bones.filter(b=>b.name!=='backpack');
 const heights={body:155,head:225,tail:55,nearArm:88,farArm:70,nearLeg:76,farLeg:72};
 for(const b of rig.bones)if(b.part)b.height=heights[b.name];
 // This character has a full forelock instead of the V9 hat silhouette.
 // Lift the neck anchor so the supplied orange bowtie is visible below the jaw.
 rig.bones.find(b=>b.name==='head').pos=[0,-98];
 rig.pelvisWidthScale=0.72;
 rig.approvedSettings={version:1,adjustments:Object.fromEntries(['nearArm','farArm','nearLeg','farLeg','body','head','tail'].map(k=>[k,{x:0,y:0,width:100,height:100,pawWidth:100,pawHeight:100}]))};
 rig.parts=parts;rig.source='Attached waiter/bare character; new three-quarter cutout art';
 fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(rig,null,2));
 let html=fs.readFileSync(path.join(base,'preview.html'),'utf8');
 html=html.replace(/const RIG=.*?;(?=\s*const SOURCES)/,()=>`const RIG=${JSON.stringify(rig)};`).replace(/const SOURCES=.*;/,()=>`const SOURCES=${JSON.stringify(images)};`);
 html=html.replace('<title>수달 ¾뷰 리깅 실험</title>','<title>웨이터 해달 · V9 리깅</title>').replace('OTTER KINGDOM · MOTION STUDY 04','OTTER KINGDOM · WAITER / V9').replace('수달, 또박또박','졸린 눈의 웨이터 해달').replace('다리 원화 재제작 · 통통한 바지와 작은 발','첨부 캐릭터 · 맨몸과 웨이터 복장 · V9 걷기').replace('새 다리 원화 적용','V9 모션 적용');
 html=html.replace(/<p class="note">머리와 모자는[\s\S]*?<\/p>/,'<p class="note">앞머리와 졸린 눈을 살린 ¾뷰입니다.<br>맨몸과 웨이터 파츠가 같은 관절과 걷기를 공유합니다.</p>');
 html=html.replace(/<footer>[\s\S]*?<\/footer>/,'<footer>첨부 정면 그림을 참고해 ¾뷰 파츠로 재구성했습니다. V9의 0.84초 주기·반 보폭 6px·발 들기 6px를 사용합니다.<br>원본을 그대로 잘라낸 결과는 아닙니다. 앞쪽 ¾뷰 시제품이며 Unity 연결 전입니다.</footer><input id="legacy" type="checkbox" hidden>');
 html=html.replace('h=b.width*im.height/im.width','h=b.height??b.width*im.height/im.width');
 // Keep the chest/neck unchanged; narrow only the lower torso in the rig renderer.
 html=html.replace('else ctx.drawImage(im,-b.pivot[0]*b.width,-b.pivot[1]*h,b.width,h);',
  'else ctx.drawImage(b.name==="body"?slimPelvis(im):im,-b.pivot[0]*b.width,-b.pivot[1]*h,b.width,h);');
 html=html.replace('function render(',`const pelvisCache=new WeakMap();
function slimPelvis(im){
 if(pelvisCache.has(im))return pelvisCache.get(im);
 const surface=document.createElement('canvas');surface.width=im.width;surface.height=im.height;
 const painter=surface.getContext('2d');
 for(let row=0;row<im.height;row++){
  const u=(row+.5)/im.height,k=Math.max(0,Math.min(1,(u-.38)/.48));
  const factor=1-(1-RIG.pelvisWidthScale)*k*k*(3-2*k),width=im.width*factor;
  painter.drawImage(im,0,row,im.width,1,(im.width-width)/2,row,width,1);
 }
 pelvisCache.set(im,surface);return surface;
}
function render(`);
 // Render the supplied-style leg as one rigid part, without drawing trouser rectangles.
 const start=html.indexOf('function drawLeg('),end=html.indexOf('function character(',start);
 html=html.slice(0,start)+`function drawLeg(t,near,mats){
 const p=legPose(t,near,mats),a=RIG.tuning?.[near?'nearLeg':'farLeg']||{width:100,height:100,pawWidth:100,pawHeight:100};
 const w=RIG.legSolver.width*(near?1:.94)*a.width/100,h=RIG.legSolver.height*(near?1:.94)*a.height/100,im=imgs.solidLeg;
 ctx.save();ctx.translate(...p.sole);ctx.rotate(p.angle);
 ctx.drawImage(im,-w*.55,-h,w,h);
 if(state.bones){ctx.strokeStyle='#ed5e66';ctx.lineWidth=1.2;ctx.beginPath();ctx.moveTo(0,-h*.75);ctx.lineTo(0,0);ctx.stroke();ctx.beginPath();ctx.arc(0,0,3,0,Math.PI*2);ctx.stroke();}ctx.restore();
}
`+html.slice(end);
 // The new flat art has opaque interiors; remove the old green fabric backing.
 html=html.replace("ctx.fillStyle='#73976a';ctx.fillRect(-60,-30,62,54);",'');
 html=html.replace('window.renderAt=t=>{render(t);return pose(t)};','window.renderAt=t=>{clock=t;render(t);return pose(t)};window.rigStudy={state,rig:RIG,pose,legPose};');
 let tuning=fs.readFileSync(path.join(root,'Tools/StarterOtterRig/tuning.js'),'utf8').replace('otter-starter-v9-tuning-approved-20261013','otter-waiter-v9-tuning-v1').replaceAll('보내준 설정','기본 설정').replaceAll('보내주신 설정','기본 설정');
 // Keep only controls that the whole-sprite leg renderer supports.
 tuning=tuning.replace("filter(([k])=>leg||!k.startsWith('paw'))","filter(([k])=>!k.startsWith('paw'))");
 const variant=`
 const clothesPanel=document.createElement('section');clothesPanel.innerHTML='<h2>캐릭터 모습</h2><div class="buttons"><button id="waiter" class="active">웨이터</button><button id="bare">맨몸</button></div><hr>';document.querySelector('aside').prepend(clothesPanel);
 const fittingStyle=document.createElement('style');fittingStyle.textContent='aside{max-height:calc(100vh - 36px);overflow-y:auto;position:sticky;top:18px}@media(max-width:760px){aside{max-height:none;position:static}}';document.head.append(fittingStyle);
 window.waiterAppearance='waiter';
 window.setAppearance=kind=>{window.waiterAppearance=kind;imgs.body=imgs[kind==='waiter'?'bodyWaiter':'bodyBare'];imgs.arm=imgs[kind==='waiter'?'armWaiter':'armBare'];for(const k of ['waiter','bare'])document.getElementById(k).classList.toggle('active',k===kind);render(clock);};
 for(const k of ['waiter','bare'])document.getElementById(k).onclick=()=>setAppearance(k);
 `;
 html=html.replace('</script>',()=>tuning+variant+'\n</script>');
 fs.writeFileSync(path.join(out,'preview.html'),html);
 fs.writeFileSync(path.join(out,'provenance.json'),JSON.stringify({baseline:'QuarterRig/Baseline-V9',hashes:Object.fromEntries(['preview.html','rig.json','walk.gif'].map(f=>[f,crypto.createHash('sha256').update(fs.readFileSync(path.join(base,f))).digest('hex')])),changes:['New reference character artwork','No backpack','Display heights fitted to new parts','Whole rounded sprite legs on unchanged sole path','Bare/waiter body and arm swaps']},null,2));
 console.log(JSON.stringify({parts,out},null,2));
}
main().catch(e=>{console.error(e.message);process.exitCode=1});
