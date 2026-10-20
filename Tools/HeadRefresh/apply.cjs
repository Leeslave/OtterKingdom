const fs=require('fs'),path=require('path'),sharp=require('sharp');
sharp.cache(false);
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtSource/Otter/HeadRefresh/V1');
const names=['Quarter','Waiter','Chef','Fisher','Baseball','Artisan','Pajama','Reception','Miner','Photographer','Merchant','Bard','Diver','Cleaner','Flower','Stripe','Hoodie','Knit','Overalls','Plaid','BearPajama','Beanie','Scholar','Varsity','Shy','Playful','Laidback','Neat','Fluffy','Fashion','Sleepy','Walker','Quiet','Lively'];
const labels=['농부','웨이터','요리사','뱃사람','야구 해달','작업반장','잠옷 해달','안내원','광부','사진가','행상인','음악가','잠수부','청소부','꽃 카디건','줄무늬 니트','후드티','케이블 니트','노랑 멜빵','체크 치마','곰돌이 잠옷','방울 모자','안경 학자','야구 점퍼','수줍은','장난꾸러기','느긋한','단정한','복슬복슬','꾸미는','잠꾸러기','산책하는','조용한','활발한'];
function backup(file,dest){if(fs.existsSync(file)&&!fs.existsSync(dest)){fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(file,dest);}}
(async()=>{
const atlas=path.join(out,'heads.png'),meta=await sharp(atlas).metadata(),manifest=[];
for(let i=0;i<names.length;i++){
if(require('./retired.json')[names[i]])continue;
const name=names[i],row=Math.floor(i/6),col=i%6,version=['Quarter','Waiter'].includes(name)?'V14':'V1',res=path.join(root,'Assets/Resources',name+'Rig'+version),art=path.join(root,'ArtSource/Otter',name+'Rig',name==='Quarter'?'':version),file=path.join(res,'front-head.png');
const left=Math.round(col*meta.width/6),top=Math.max(0,Math.round(row*meta.height/6)-(row===5?22:0)),width=Math.round((col+1)*meta.width/6)-left,height=Math.round((row+1)*meta.height/6)-top;
let raw=await sharp(atlas).extract({left,top,width,height}).ensureAlpha().raw().toBuffer(),seen=new Uint8Array(width*height),largest=[];
for(let p=0;p<seen.length;p++)if(!seen[p]&&raw[p*4+3]>100){const stack=[p],g=[];seen[p]=1;while(stack.length){const j=stack.pop(),x=j%width,y=Math.floor(j/width);g.push(j);for(const n of[x?j-1:-1,x<width-1?j+1:-1,y?j-width:-1,y<height-1?j+width:-1])if(n>=0&&!seen[n]&&raw[n*4+3]>100){seen[n]=1;stack.push(n);}}if(g.length>largest.length)largest=g;}
const keep=new Uint8Array(width*height);for(const j of largest){const x=j%width,y=Math.floor(j/width);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<width&&y+dy>=0&&y+dy<height)keep[(y+dy)*width+x+dx]=1;}for(let p=0;p<keep.length;p++)if(!keep[p])raw[p*4+3]=0;
const clean=await sharp(raw,{raw:{width,height,channels:4}}).png().toBuffer(),trim=await sharp(clean).trim({threshold:10}).png().toBuffer();
fs.mkdirSync(path.join(out,'parts'),{recursive:true});fs.writeFileSync(path.join(out,'parts',name+'.png'),trim);
const old=await sharp(file).metadata();backup(file,path.join(out,'backup',name,'front-head.png'));
let png=await sharp(trim).resize(old.width,old.height,{fit:'fill'}).png().toBuffer(); if(['Beanie','Walker'].includes(name)){const w=Math.round(old.width*.85),left=Math.round((old.width-w)/2); const short=await sharp(png).resize(w,old.height,{fit:'fill'}).png().toBuffer();png=await sharp({create:{width:old.width,height:old.height,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:short,left,top:0}]).png().toBuffer();}if(!fs.readFileSync(file).equals(png)){fs.writeFileSync(file+'.tmp',png);fs.renameSync(file+'.tmp',file);}
const preview=path.join(art,'preview.html');backup(preview,path.join(out,'backup',name,'preview.html'));
let html;
if(fs.existsSync(preview)){
html=fs.readFileSync(preview,'utf8');const key=name==='Quarter'?'head':'front-head',re=new RegExp('"'+key+'":"data:image/png;base64,[^"]+"');if(!re.test(html))throw Error('Missing source '+name);html=html.replace(re,()=>JSON.stringify(key)+':'+JSON.stringify('data:image/png;base64,'+png.toString('base64')));
}else{
html=fs.readFileSync(path.join(root,'ArtSource/Otter/WalkerRig/V1/preview.html'),'utf8');const rig=JSON.parse(fs.readFileSync(path.join(res,'rig.json'))),sources={};for(const f of fs.readdirSync(res).filter(n=>/^(front|rear)-.*\.png$/.test(n)))sources[f.slice(0,-4)]='data:image/png;base64,'+fs.readFileSync(path.join(res,f)).toString('base64');
html=html.replace(/const RIG=[\s\S]*?;const SOURCES=\{[\s\S]*?\};/,()=>`const RIG=${JSON.stringify(rig)};const SOURCES=${JSON.stringify(sources)};`).replaceAll('수줍은',labels[i]).replaceAll('otter-shy-',`otter-${name.toLowerCase()}-`).replaceAll('shy-otter-',name.toLowerCase()+'-otter-');
}
fs.writeFileSync(preview,html);manifest.push({name,label:labels[i],art:path.relative(root,art).replaceAll('\\','/'),resource:path.relative(root,res).replaceAll('\\','/'),width:old.width,height:old.height});
}
fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2));console.log('Updated '+manifest.length+' front heads; original sprite sizes and meta pivots retained.');
})().catch(e=>{console.error(e);process.exit(1)});
