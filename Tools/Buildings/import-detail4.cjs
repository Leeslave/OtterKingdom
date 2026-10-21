// Mechanical sprite slicing/import. Background removal was done with image_gen.
// Run with sharp available on NODE_PATH. Existing import GUIDs and canvas sizes stay intact.
const fs = require('fs'), path = require('path'), crypto = require('crypto'), sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
process.chdir(root);
const source = 'ArtSource/Buildings/Detail4Runtime';
const output = 'Assets/Art/Buildings/Detail4';
const groups = [
  ['homes-alpha.png', 3, 2, ['HouseRed','HouseBlue','SmallHouse','Castle','TownHall','Hospital']],
  ['production-alpha.png',3,2,['Shop','Workshop','Workyard','Quarry','Fishery','Granary']],
  ['guild-alpha.png',2,3,['Archive','GuildOffice','GuildBranch','VillageGuild','FishingDock','MineEntrance']]
];
const aliases = {
  HouseRed:['Assets/Art/Plaza/Props/Prop_House_Red.png','Assets/Art/Plaza/Props/Prop_House_Red_OtterStyle.png','Assets/Art/Settlement/Stage_House_Red_3.png','Assets/Art/Settlement/ICON_House_Red.png'],
  HouseBlue:['Assets/Art/Plaza/Props/Prop_House_Blue.png','Assets/Art/Plaza/Props/Prop_House_Blue_OtterStyle.png','Assets/Art/Settlement/Stage_House_Blue_3.png','Assets/Art/Settlement/ICON_House_Blue.png'],
  Castle:['Assets/Art/Settlement/Prop_Castle.png'],
  TownHall:['Assets/Art/Settlement/Prop_TownHall.png','Assets/Art/Settlement/ICON_TownHall.png'],
  GuildOffice:['Assets/Art/Settlement/Prop_GuildOffice.png','Assets/Art/Settlement/ICON_GuildOffice.png'],
  FishingDock:['Assets/Art/Settlement/ICON_FishingDock.png'],
  MineEntrance:['Assets/Art/Settlement/ICON_MinePath.png']
};
for(const name of ['Shop','Workshop','Workyard','Quarry','Fishery','Granary','Archive'])
  aliases[name]=[`Assets/Art/Settlement/Building_${name}.png`];
function bounds(data,w,h,threshold=20){
  let x0=w,y0=h,x1=-1,y1=-1;
  for(let y=0;y<h;y++) for(let x=0;x<w;x++) if(data[(y*w+x)*4+3]>threshold){
    x0=Math.min(x0,x); y0=Math.min(y0,y); x1=Math.max(x1,x); y1=Math.max(y1,y);
  }
  if(x1<0) throw Error('Empty sprite');
  return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1};
}
async function cells(file,cols,rows){
  const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const w=info.width,h=info.height,seen=new Uint8Array(w*h),parts=[];
  for(let i=0;i<w*h;i++){
    if(seen[i]||data[i*4+3]<64)continue;
    let q=[i],n=0,x0=w,y0=h,x1=0,y1=0;seen[i]=1;
    while(n<q.length){
      const j=q[n++],x=j%w,y=Math.floor(j/w);
      x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);
      for(const k of [x?j-1:-1,x<w-1?j+1:-1,y?j-w:-1,y<h-1?j+w:-1])
        if(k>=0&&!seen[k]&&data[k*4+3]>=64){seen[k]=1;q.push(k);}
    }
    if(n>20)parts.push({x0,y0,x1,y1,n});
  }
  parts.sort((a,b)=>b.n-a.n);
  const main=parts.slice(0,cols*rows);
  if(main.length!==cols*rows)throw Error('Missing sheet objects');
  for(const p of parts.slice(cols*rows)){
    const near=main.reduce((a,b)=>{
      const d=t=>Math.max(t.x0-p.x1,p.x0-t.x1,0)**2+Math.max(t.y0-p.y1,p.y0-t.y1,0)**2;
      return d(a)<d(b)?a:b;
    });
    near.x0=Math.min(near.x0,p.x0);near.y0=Math.min(near.y0,p.y0);
    near.x1=Math.max(near.x1,p.x1);near.y1=Math.max(near.y1,p.y1);
  }
  main.sort((a,b)=>(a.y0+a.y1)-(b.y0+b.y1));
  const ordered=[];
  for(let row=0;row<rows;row++)ordered.push(...main.slice(row*cols,(row+1)*cols).sort((a,b)=>a.x0-b.x0));
  return ordered.map(p=>({left:Math.max(0,p.x0-3),top:Math.max(0,p.y0-3),
    width:Math.min(w,p.x1+4)-Math.max(0,p.x0-3),height:Math.min(h,p.y1+4)-Math.max(0,p.y0-3)}));
}
function meta(file){
  if(fs.existsSync(file+'.meta')) return fs.readFileSync(file+'.meta','utf8').match(/guid: (\w+)/)[1];
  const guid=crypto.randomBytes(16).toString('hex');
  let s=fs.readFileSync('Assets/Art/Settlement/Building_Workshop.png.meta','utf8');
  s=s.replace(/guid: \w+/,`guid: ${guid}`);
  fs.writeFileSync(file+'.meta',s);
  return guid;
}
function folder(dir){
  fs.mkdirSync(dir,{recursive:true});
  if(dir.startsWith('Assets/')&&!fs.existsSync(dir+'.meta'))
    fs.writeFileSync(dir+'.meta',`fileFormatVersion: 2\nguid: ${crypto.randomBytes(16).toString('hex')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);
}
(async()=>{
  folder('Assets/Art/Buildings'); folder(output);
  const report=[], ids={};
  for(const [sheet,cols,rows,names] of groups){
    const file=path.join(source,sheet), regions=await cells(file,cols,rows);
    for(let i=0;i<names.length;i++){
      const name=names[i],box=regions[i];
      const cropped=await sharp(file).extract(box).png().toBuffer();
      const fit=await sharp(cropped).resize({width:492,height:492,fit:'inside'}).toBuffer({resolveWithObject:true});
      const target=`${output}/${name}.png`;
      await sharp({create:{width:512,height:512,channels:4,background:'#00000000'}})
        .composite([{input:fit.data,left:Math.round((512-fit.info.width)/2),top:502-fit.info.height}]).png().toFile(target);
      ids[name]=meta(target);
      // Small-house construction frames were authored against the blue-house canvas.
      // Match that reference's world bounds/pivot, then use the new art for its final stage.
      if(name==='SmallHouse'){
        const legacy='Assets/Art/Plaza/Props/Prop_House_Blue.png';
        const original=path.join(source,'before',legacy);
        const old=await sharp(original).ensureAlpha().raw().toBuffer({resolveWithObject:true});
        const area=bounds(old.data,old.info.width,old.info.height);
        const scaled=await sharp(cropped).resize({width:area.width,height:area.height,fit:'inside'}).toBuffer({resolveWithObject:true});
        await sharp({create:{width:old.info.width,height:old.info.height,channels:4,background:'#00000000'}})
          .composite([{input:scaled.data,left:Math.round(area.left+(area.width-scaled.info.width)/2),top:area.top+area.height-scaled.info.height}])
          .png().toFile(target);
        const settings=fs.readFileSync(legacy+'.meta','utf8').replace(/guid: \w+/,`guid: ${ids[name]}`);
        fs.writeFileSync(target+'.meta',settings);
      }
      const replaced=[];
      for(const legacy of aliases[name]||[]){
        if(!fs.existsSync(legacy)) continue;
        const backup=path.join(source,'before',legacy);
        fs.mkdirSync(path.dirname(backup),{recursive:true});
        if(!fs.existsSync(backup)) fs.copyFileSync(legacy,backup);
        const old=await sharp(backup).ensureAlpha().raw().toBuffer({resolveWithObject:true});
        const area=bounds(old.data,old.info.width,old.info.height);
        const resized=await sharp(cropped).resize({width:area.width,height:area.height,fit:'inside'}).toBuffer({resolveWithObject:true});
        const icon=legacy.includes('/ICON_');
        const left=Math.round(area.left+(area.width-resized.info.width)/2);
        const top=icon?Math.round(area.top+(area.height-resized.info.height)/2):area.top+area.height-resized.info.height;
        const encoded=await sharp({create:{width:old.info.width,height:old.info.height,channels:4,background:'#00000000'}})
          .composite([{input:resized.data,left,top}]).png().toBuffer();
        if(!fs.readFileSync(legacy).equals(encoded)) fs.writeFileSync(legacy,encoded);
        replaced.push(legacy);
      }
      report.push({name,target,guid:ids[name],cell:box,replaced});
    }
  }
  const defs={SmallHouse:'Building_SmallHouse',Hospital:'Building_bld_mine_hospital',VillageGuild:'Building_bld_mine_guild',GuildBranch:'Building_GuildBranch',
    Shop:'Building_Shop',Workshop:'Building_Workshop',Workyard:'Building_Workyard',Quarry:'Building_Quarry',Fishery:'Building_Fishery',Granary:'Building_Granary',Archive:'Building_Archive'};
  for(const [name,def] of Object.entries(defs)){
    const p=`Assets/Scriptable Obejects/Decor/Buildings/${def}.asset`;
    let s=fs.readFileSync(p,'utf8');
    const reference=`{fileID: 21300000, guid: ${ids[name]}, type: 3}`;
    if(['SmallHouse','Hospital','VillageGuild','GuildBranch'].includes(name))
      s=s.replace(/_worldSprite: \{[^}]*\}/,`_worldSprite: ${reference}`);
    s=s.replace(/_icon: \{[^}]*\}/,`_icon: ${reference}`);
    if(name==='SmallHouse')
      s=s.replace(/(_stageSprites:\r?\n(?:  -[^\n]*\n){3})  -[^\n]*/,`$1  - ${reference}`);
    fs.writeFileSync(p,s);
  }
  fs.writeFileSync(path.join(source,'manifest.json'),JSON.stringify(report,null,2)+'\n');
  console.log(`Imported ${report.length} sprites; updated ${report.flatMap(r=>r.replaced).length} existing images and ${Object.keys(defs).length} building definitions.`);
})().catch(e=>{console.error(e);process.exit(1)});
