const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..'),cast=require('./cast.json');
const bounds=[[0,435,820,1015,1235,1536],[0,435,765,955,1190,1536],[0,430,785,1010,1225,1536],[0,420,770,975,1180,1536],[0,465,805,1005,1215,1536],[0,445,820,1005,1215,1536],[0,420,790,1000,1225,1536],[0,455,800,985,1205,1536],[0,425,795,985,1210,1536],[0,410,750,975,1200,1536]];
for(const c of cast){const i=c.number-41;
 const dir=path.join(root,'Tools',c.stem+'Rig'),art=path.join(root,'ArtSource/Otter',c.stem+'Rig/V1');fs.mkdirSync(dir,{recursive:true});fs.mkdirSync(art,{recursive:true});
 const fixes=require('./fixes.json'),generated='C:/Users/이상현/.codex/generated_images/01a12f48-17ef-7873-b304-9063292668c1';
 if(!fs.existsSync(path.join(art,'atlas.png')))fs.copyFileSync(path.join(generated,fixes[i]||c.source),path.join(art,'atlas.png'));
 if(fixes[i]&&!fs.existsSync(path.join(art,'original-atlas.png')))fs.copyFileSync(path.join(generated,c.source),path.join(art,'original-atlas.png'));
 if(!fs.existsSync(path.join(art,'approved-settings.json')))fs.copyFileSync(path.join(root,'ArtSource/Otter/PajamaRig/V1/approved-settings.json'),path.join(art,'approved-settings.json'));
 let s=fs.readFileSync(path.join(root,'Tools/PajamaRig/build.cjs'),'utf8').replaceAll('Pajama',c.stem).replaceAll('pajama',c.stem.toLowerCase()).replaceAll('잠옷',c.label).replace('달과 별 무늬 '+c.label+'을 입은 '+c.label+' 해달.',c.label+' 해달 · '+c.number+'번.');
 const start=s.indexOf(' const bounds='),end=s.indexOf(' const rig=');
 s=s.slice(0,start)+` const images=await require('../PersonalityRig/extract.cjs')(atlas,art,${JSON.stringify(bounds[i])},${[0,4,5,8].includes(i)});\n`+s.slice(end);
 s=s.replace('b.height=230','b.height='+([0,5].includes(i)?235:215));
 if([0,5].includes(i))s=s.replace('b.height=135','b.height=155');
 fs.writeFileSync(path.join(dir,'build.cjs'),s);
 fs.writeFileSync(path.join(dir,'check.cjs'),fs.readFileSync(path.join(root,'Tools/PajamaRig/check.cjs'),'utf8').replaceAll('Pajama',c.stem));
 fs.writeFileSync(path.join(art,'README.md'),'# '+c.number+' '+c.label+' 해달\n\n앞·뒤 ¾뷰 14개 파츠, V14 걷기. preview.html에서 네 방향 및 파츠 위치/크기를 조절합니다. 조절값은 JSON으로 내보내며 Unity에 자동 반영되지 않습니다.\n\n원화: 내장 image_gen. 프롬프트: Tools/PersonalityRig/PROMPTS.md.\n');
}
