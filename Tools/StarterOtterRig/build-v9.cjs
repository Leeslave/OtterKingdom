const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),baseline=path.join(root,'ArtSource/Otter/QuarterRig/Baseline-V9'),out=path.join(root,'ArtSource/Otter/StarterRig/V9');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
async function main(){
 fs.mkdirSync(path.join(out,'parts'),{recursive:true});
 const original=fs.readFileSync(path.join(baseline,'preview.html'),'utf8');
 const rig=JSON.parse(fs.readFileSync(path.join(baseline,'rig.json'),'utf8'));
 const sources=JSON.parse(original.match(/const SOURCES=(.*);/)[1]);
 const bounds=JSON.parse(fs.readFileSync(path.join(out,'atlas-regions.json'),'utf8'));
 const images={},dimensions={};
 for(const [name,[left,top,width,height]] of Object.entries(bounds)){
  const raw=await sharp(path.join(out,'parts-atlas.png')).extract({left,top,width,height}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=width,y0=height,x1=-1,y1=-1;
  for(let y=0;y<height;y++)for(let x=0;x<width;x++)if(raw.data[(y*width+x)*4+3]>12){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
  if(x1<0)throw Error('Empty part '+name);
  const rect={left:left+x0,top:top+y0,width:x1-x0+1,height:y1-y0+1};
  const data=await sharp(path.join(out,'parts-atlas.png')).extract(rect).png().toBuffer();
  fs.writeFileSync(path.join(out,'parts',name+'.png'),data);images[name]='data:image/png;base64,'+data.toString('base64');dimensions[name]=rect;
 }
 images.leg=images.solidLeg;
 // Preserve baseline coordinates, dimensions and animation, removing only absent gear.
 rig.bones=rig.bones.filter(b=>b.name!=='backpack');
 for(const b of rig.bones.filter(b=>b.part)){
  const old=await sharp(Buffer.from(sources[b.part].split(',')[1],'base64')).metadata();
  b.height=b.width*old.height/old.width;
 }
 rig.baseline='QuarterRig/Baseline-V9';
 rig.slots=[{name:'hat',parent:'head',pos:[0,-165]},{name:'shirt',parent:'body',pos:[0,0]},{name:'nearSleeve',parent:'nearArm',pos:[0,0]},{name:'farSleeve',parent:'farArm',pos:[0,0]},{name:'backpack',parent:'body',pos:[-62,-27]}];
 rig.parts=dimensions;
 rig.approvedSettings=JSON.parse(fs.readFileSync(path.join(out,'approved-settings.json'),'utf8'));
 fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(rig,null,2));
 let html=original.replace(/const RIG=.*?;(?=\s*const SOURCES)/,()=>`const RIG=${JSON.stringify(rig)};`).replace(/const SOURCES=.*;/,()=>`const SOURCES=${JSON.stringify(images)};`);
 html=html.replace('<title>수달 ¾뷰 리깅 실험</title>','<title>처음 해달 · V9 기준 리깅</title>')
 .replace('OTTER KINGDOM · MOTION STUDY 04','OTTER KINGDOM · STARTER / V9')
 .replace('수달, 또박또박','처음 해달, V9 걸음으로')
 .replace('다리 원화 재제작 · 통통한 바지와 작은 발','확정한 ¾뷰 · 작은 보폭 · 발바닥 기준 걷기')
 .replace('새 다리 원화 적용','V9 기준 적용')
 .replace('머리와 모자는 함께 움직이고,','머리는 몸통을 따라 움직이고,')
 .replace('꼬리와 가방에는 조금 늦게','꼬리는 조금 늦게')
 .replace(/<footer>[\s\S]*?<\/footer>/,'<footer>새 주인공 외형에 확정 V9의 관절 배치와 동작을 적용했습니다. 0.84초 주기 · 반 보폭 6px · 발 들기 6px · 다리 60×76px.<br>모자·옷·가방은 없는 기본 몸입니다. 앞쪽 ¾뷰와 좌우 반전 미리보기이며, 측면·뒷면과 Unity 연결은 후속 작업입니다.</footer><input id="legacy" type="checkbox" hidden>')
 .replace("h=b.width*im.height/im.width","h=b.height??b.width*im.height/im.width")
 .replace("ctx.fillStyle='#698653'","ctx.fillStyle='#c99168'")
 .replace("ctx.strokeStyle='#301007'","ctx.strokeStyle='#472010'")
 .replace("ctx.fillStyle='#73976a'","ctx.fillStyle='#c99168'");
 // Expose deterministic pose rendering for phase/contact verification and GIF export.
 html=html.replace('window.renderAt=t=>{render(t);return pose(t)};','window.renderAt=t=>{clock=t;render(t);return pose(t)};\nwindow.rigStudy={state,rig:RIG,legPose,pose,render};');
 html=html.replace('const width=RIG.legSolver.width*(near?1:.94),height=RIG.legSolver.height*(near?1:.94);',
  'const adjustment=RIG.tuning?.[near?"nearLeg":"farLeg"]||{width:100,height:100,pawWidth:100,pawHeight:100};\n const width=RIG.legSolver.width*(near?1:.94)*adjustment.width/100,height=RIG.legSolver.height*(near?1:.94)*adjustment.height/100;');
 html=html.replace('-width*.55,-height*.22,width,height*.22);','-width*.55*adjustment.pawWidth/100,-height*.22*adjustment.pawHeight/100,width*adjustment.pawWidth/100,height*.22*adjustment.pawHeight/100);');
 html=html.replace('ctx.fillRect(-60,-30,62,54);','ctx.fillRect(-60*b.width/170,-30*h/(170*1156/1298),62*b.width/170,54*h/(170*1156/1298));');
 html=html.replace('ctx.roundRect(-width*.51,-height+2,width*.85,height*.80,5);','ctx.roundRect(-width*.51,-height+2,width*.85,height*.80,Math.min(width*.34,height*.22));');
 html=html.replace('if(state.bones){ctx.strokeStyle=\'#ed5e66\';','if(window.drawWardrobeLeg)window.drawWardrobeLeg(near,width,height);\n if(state.bones){ctx.strokeStyle=\'#ed5e66\';');
 html=html.replace(/ctx.restore\(\);\s*}\s*if\(state.bones\)/, 'if(window.drawWardrobePart)window.drawWardrobePart(b,h);\n    ctx.restore();\n  }\n  if(state.bones)');
 html=html.replace('모자·옷·가방은 없는 기본 몸입니다.','보내주신 체형 설정과 둥근 다리를 적용했습니다. 모자·상의·하의는 오른쪽에서 시험할 수 있습니다.');
 html=html.replace('</script>',()=>fs.readFileSync(path.join(__dirname,'tuning.js'),'utf8')+'\n'+fs.readFileSync(path.join(__dirname,'wardrobe.js'),'utf8')+'\n</script>');
 fs.writeFileSync(path.join(out,'preview.html'),html);
 const provenance={baselineHashes:Object.fromEntries(['preview.html','rig.json','walk.gif'].map(f=>[f,hash(fs.readFileSync(path.join(baseline,f)))])),motion:'pose and legPose functions copied unchanged from frozen V9',changes:['New unclothed three-quarter artwork','Backpack removed; attachment slot retained','Original displayed part dimensions preserved','Straight leg upper section uses fur color instead of trouser color','UI copy updated; legacy comparison hidden']};
 fs.writeFileSync(path.join(out,'provenance.json'),JSON.stringify(provenance,null,2));
 console.log(JSON.stringify({out,dimensions,legSolver:rig.legSolver},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
