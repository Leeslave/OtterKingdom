// Only writes the isolated StarterRig study; never imports into Unity automatically.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const out = path.resolve(__dirname, '../../ArtSource/Otter/StarterRig');
async function main() {
  fs.mkdirSync(path.join(out,'parts'),{recursive:true});
  const atlas = path.join(out,'parts-atlas.png');
  const meta = await sharp(atlas).metadata();
  if (!meta.hasAlpha) throw new Error('Atlas must have alpha');
  const images = {}, dimensions = {};
  const names=['head','body','armRight','armLeft','leg','tail'];
  // Measured from the generated 1536 x 1024 atlas, not assumed equal cells.
  const regions=[[25,45,655,510],[695,125,460,435],[1250,160,270,370],[130,590,270,355],[640,600,260,345],[1100,695,355,210]];
  for(let i=0;i<names.length;i++){
    const [left,top,w,h]=regions[i];
    const right=left+w,bottom=top+h;
    const cell=await sharp(atlas).extract({left,top,width:right-left,height:bottom-top}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    let x0=cell.info.width,y0=cell.info.height,x1=-1,y1=-1,transparent=0;
    for(let y=0;y<cell.info.height;y++)for(let x=0;x<cell.info.width;x++){
      const a=cell.data[(y*cell.info.width+x)*4+3];
      if(a>12){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}else transparent++;
    }
    if(x1<0 || transparent<cell.info.width*cell.info.height*.1) throw new Error('Missing part or opaque backdrop: '+names[i]);
    const width=x1-x0+1,height=y1-y0+1;
    const data=await sharp(atlas).extract({left:left+x0,top:top+y0,width,height}).png().toBuffer();
    fs.writeFileSync(path.join(out,'parts',names[i]+'.png'),data);
    images[names[i]]='data:image/png;base64,'+data.toString('base64');
    dimensions[names[i]]={width,height,atlasRect:[left+x0,top+y0,width,height]};
  }
  // Coordinates: y down, sole at 0. All positions are parent-local.
  const rig={version:1,view:'front',units:'pixels, x right, y down, rotation radians',source:'parts-atlas.png',dimensions,
    bones:[
      {name:'root',parent:null,pos:[0,0]},
      {name:'hips',parent:'root',pos:[0,-40]},
      {name:'tail',parent:'hips',pos:[49,-22],part:'tail',width:92,height:56,pivot:[.12,.55],layer:0},
      {name:'legLeft',parent:'hips',pos:[-35,-5],part:'leg',width:53,height:67,pivot:[.5,.33],layer:1},
      {name:'legRight',parent:'hips',pos:[35,-5],part:'leg',width:53,height:67,pivot:[.5,.33],mirror:true,layer:2},
      {name:'body',parent:'hips',pos:[0,-43],part:'body',width:159,height:162,pivot:[.5,.65],layer:3},
      {name:'armLeft',parent:'body',pos:[-66,-54],part:'armLeft',width:52,height:102,pivot:[.5,.16],layer:4},
      {name:'armRight',parent:'body',pos:[66,-54],part:'armRight',width:52,height:102,pivot:[.5,.16],layer:5},
      {name:'head',parent:'body',pos:[0,-62],part:'head',width:287,height:215,pivot:[.5,.91],layer:6}
    ],
    slots:[{name:'hat',parent:'head',pos:[0,-166]},{name:'shirt',parent:'body',pos:[0,0]},{name:'sleeveLeft',parent:'armLeft',pos:[0,0]},{name:'sleeveRight',parent:'armRight',pos:[0,0]}],
    clips:['idle','walk','wave'],notes:['Rigid cutout prototype, not deformable skinning.','Face and proportions fixed.','Front only. No gameplay or Unity scene changes.','Hat/shirt slots only; no clothing assets yet.']};
  fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(rig,null,2));
  const template=fs.readFileSync(path.join(__dirname,'preview.html'),'utf8');
  fs.writeFileSync(path.join(out,'preview.html'),template.replace('/*RIG_DATA*/',JSON.stringify(rig)).replace('/*IMAGE_DATA*/',JSON.stringify(images)));
  console.log(JSON.stringify({out,dimensions},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
