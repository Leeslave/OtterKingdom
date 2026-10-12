// Rebuild the self-contained prototype from the generated transparent atlas.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const out = path.resolve(__dirname, '../../ArtSource/Otter/QuarterRig');
const regions = {
  head: [20,70,570,485], body: [610,150,450,405], arm: [1150,155,280,400],
  leg: [150,565,350,400], tail: [570,620,450,300], backpack: [1090,560,400,400]
};
async function main() {
  fs.mkdirSync(path.join(out,'parts'),{recursive:true});
  const images = {};
  for (const [name,[left,top,width,height]] of Object.entries(regions)) {
    const cell = await sharp(path.join(out,'atlas.png')).extract({left,top,width,height}).png().toBuffer();
    const data = await sharp(cell).trim().png().toBuffer();
    fs.writeFileSync(path.join(out,'parts',name+'.png'),data);
    images[name] = 'data:image/png;base64,' + data.toString('base64');
  }
  const meta=await sharp(path.join(out,'legs-v2.png')).metadata();
  for(const [i,name] of ['trouser','foot'].entries()){
    const width=Math.floor(meta.width/2);
    const cell=await sharp(path.join(out,'legs-v2.png')).extract({left:i*width,top:0,width,height:meta.height}).png().toBuffer();
    const data=await sharp(cell).trim().png().toBuffer();
    fs.writeFileSync(path.join(out,'parts',name+'.png'),data);
    images[name]='data:image/png;base64,'+data.toString('base64');
  }
  const solidLeg=await sharp(path.join(out,'leg-straight-v8.png')).trim().png().toBuffer();
  fs.writeFileSync(path.join(out,'parts','solid-leg.png'),solidLeg);
  images.solidLeg='data:image/png;base64,'+solidLeg.toString('base64');
  const flatBody=await sharp(path.join(out,'body-flat-v6.png')).trim().png().toBuffer();
  fs.writeFileSync(path.join(out,'parts','body-flat.png'),flatBody);
  images.body='data:image/png;base64,'+flatBody.toString('base64');
  const rig = {
    units:'pixels; x right, y down; rotations in radians', view:'front-right three-quarter',
    legSolver:{type:'rigid straight leg, sole-anchored',stanceFraction:.6,cycleSeconds:.84,stepHalfWidth:10,lift:11,width:60,height:76,nearX:-10,farX:31,swingAngle:.055,rearInset:0,frontInset:0},
    bodyRearInset:0,
    bones:[
      {name:'hips',parent:null,pos:[0,-43]},
      {name:'body',parent:'hips',pos:[0,-54],part:'body',width:170,pivot:[.5,.6],layer:5},
      {name:'tail',parent:'hips',pos:[-42,-5],part:'tail',width:100,pivot:[.9,.55],layer:0},
      {name:'backpack',parent:'body',pos:[-62,-27],part:'backpack',width:78,pivot:[.6,.3],layer:2},
      {name:'farArm',parent:'body',pos:[68,-51],part:'arm',width:44,pivot:[.55,.18],layer:1},
      {name:'farLeg',parent:'hips',pos:[34,0],part:'leg',width:47,pivot:[.45,.22],layer:3},
      {name:'nearLeg',parent:'hips',pos:[-26,-4],part:'leg',width:50,pivot:[.45,.22],layer:4},
      {name:'nearArm',parent:'body',pos:[-65,-53],part:'arm',width:55,pivot:[.55,.18],layer:6},
      {name:'head',parent:'body',pos:[0,-54],part:'head',width:270,pivot:[.5,.9],layer:7}
    ]
  };
  fs.writeFileSync(path.join(out,'rig.json'),JSON.stringify(rig,null,2));
  let html=fs.readFileSync(path.join(__dirname,'preview.html'),'utf8');
  html=html.replace('/*__DATA__*/',`const RIG=${JSON.stringify(rig)}; const SOURCES=${JSON.stringify(images)};`);
  fs.writeFileSync(path.join(out,'preview.html'),html);
  console.log('Built '+out);
}
main().catch(e=>{console.error(e);process.exit(1)});
