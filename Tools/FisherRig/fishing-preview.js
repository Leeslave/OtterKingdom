// Preview-only fishing pose layered over the shared walk rig.
const fishingButton=document.createElement('button');fishingButton.id='fish';fishingButton.textContent='낚싯대 들기';
document.getElementById('idle').after(fishingButton);
state.fishing=false;
const walkPose=pose;
pose=function(t){
 if(!state.fishing)return walkPose(t);
 const mats={},angle=-.95+.025*Math.sin(t*2);
 for(const b of activeBones()){
  let [x,y]=b.pos,r=0;
  if(b.name==='hips')y+=8;
  if(b.name==='body')y+=.6*Math.sin(t*2);
  if(b.name==='nearArm')r=angle;
  if(b.name==='farArm')r=.75;
  if(b.name==='head')r=.025*Math.sin(t*1.5);
  mats[b.name]=mul(b.parent?mats[b.parent]:[1,0,0,1,0,0],local(x,y,r));
 }
 return mats;
};
const walkCharacter=character;
character=function(t,x,y,scale){
 walkCharacter(t,x,y,scale);
 if(!state.fishing)return;
 const m=pose(t).nearArm,b=activeBones().find(b=>b.name==='nearArm');
 const localY=b.height*(1-b.pivot[1])*.82;
 const hand=[m[4]+m[2]*localY,m[5]+m[3]*localY];
 const tilt=-.36+.015*Math.sin(t*2),length=200;
 ctx.save();ctx.translate(x,y);ctx.scale(state.flip?-scale:scale,scale);
 ctx.save();ctx.translate(...hand);ctx.rotate(tilt);ctx.drawImage(imgs.rod,-15,-6,length,12);ctx.restore();
 const tip=[hand[0]+(length-15)*Math.cos(tilt),hand[1]+(length-15)*Math.sin(tilt)];
 ctx.strokeStyle='#654b36';ctx.lineWidth=1.5;ctx.beginPath();ctx.moveTo(...tip);ctx.lineTo(tip[0]+Math.sin(t*2)*2,8);ctx.stroke();
 ctx.strokeStyle='#82b8c5';ctx.lineWidth=2;ctx.beginPath();ctx.ellipse(tip[0],10,12+2*Math.sin(t*2),4,0,0,Math.PI*2);ctx.stroke();
 ctx.restore();
};
fishingButton.onclick=()=>{
 state.fishing=true;state.mode='idle';setDirection(state.flip?'sw':'se');
 for(const id of ['walk','idle'])document.getElementById(id).classList.remove('active');
 fishingButton.classList.add('active');status();render(clock);
};
for(const id of ['walk','idle']){
 const original=document.getElementById(id).onclick;
 document.getElementById(id).onclick=()=>{state.fishing=false;fishingButton.classList.remove('active');original();render(clock);};
}
for(const dir of ['nw','ne','sw','se']){
 const original=document.getElementById(dir).onclick;
 document.getElementById(dir).onclick=()=>{state.fishing=false;fishingButton.classList.remove('active');original();render(clock);};
}
const help=document.createElement('p');help.className='note';help.textContent='낚싯대 들기는 앞쪽 자세 미리보기입니다. Unity에는 걷기 리그가 연결되어 있어요.';fishingButton.parentElement.after(help);
