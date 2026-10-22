const fs=require('fs'),path=require('path'),sharp=require('sharp'),cast=require('./cast.json');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtSource/Otter/PersonalityCast');fs.mkdirSync(out,{recursive:true});
(async()=>{
 const cells=[];
 for(const [i,c]of cast.entries()){
 const art=path.join(root,'ArtSource/Otter',c.stem+'Rig/V1');
 cells.push({input:await sharp(path.join(art,'se.png')).resize(320,250,{fit:'cover'}).png().toBuffer(),left:i%5*320,top:Math.floor(i/5)*250});
 }
 await sharp({create:{width:1600,height:500,channels:4,background:'#e9eee0'}}).composite(cells).png().toFile(path.join(out,'overview.png'));
 fs.writeFileSync(path.join(out,'index.html'),`<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>해달 41–50 리깅</title><style>body{margin:0;background:#edf3e8;color:#293d31;font:16px system-ui}main{max-width:1250px;margin:50px auto;padding:20px}h1{font-size:32px}p{color:#607263}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:16px}a{color:inherit;text-decoration:none;background:#fff;border-radius:20px;overflow:hidden;box-shadow:0 4px 16px #21342110}a:hover{outline:3px solid #78a983}img{width:100%;aspect-ratio:1.25;object-fit:cover}strong{display:block;padding:16px;font-size:18px}small{display:block;padding:0 16px 20px;color:#738174}</style><main><h1>해달 친구들 · 41–50</h1><p>각 캐릭터를 눌러 네 방향 걷기와 팔·다리 위치, 머리·몸통 크기를 조절하세요.</p><section>${cast.map(c=>`<a href="../${c.stem}Rig/V1/preview.html"><img src="../${c.stem}Rig/V1/se.png" alt="${c.label} 해달"><strong>${c.number} · ${c.label} 해달</strong><small>걷기 미리보기 열기 →</small></a>`).join('')}</section></main></html>`);
})();
