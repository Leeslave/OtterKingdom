// Embedded in the standalone preview. Defaults reproduce the frozen V9 pose.
const tuningDefaults=structuredClone(RIG.bones),tuningKey='otter-starter-v9-tuning-approved-20261013';
const tuningParts={nearArm:'앞쪽 팔',farArm:'뒤쪽 팔',nearLeg:'앞쪽 발·다리',farLeg:'뒤쪽 발·다리',body:'몸통',head:'머리',tail:'꼬리'};
const tuningFields={x:['좌우 위치',-100,100,1,'px'],y:['상하 위치',-100,100,1,'px'],width:['가로 크기',40,180,1,'%'],height:['세로 크기',40,180,1,'%'],pawWidth:['발끝 가로',40,180,1,'%'],pawHeight:['발끝 세로',40,180,1,'%']};
const freshTuning=()=>Object.fromEntries(Object.keys(tuningParts).map(k=>[k,{x:0,y:0,width:100,height:100,pawWidth:100,pawHeight:100}]));
const approvedTuning=()=>structuredClone(RIG.approvedSettings.adjustments);
let adjustments=approvedTuning();
RIG.tuning=adjustments;
const panel=document.createElement('section');panel.id='tuning';
panel.innerHTML=`<hr><h2>몸 조절하기</h2><p class="note">부위를 선택하고 슬라이더나 숫자로 조절하세요. 위치는 기본값에서의 이동량입니다. 상하 +는 아래쪽이에요.</p><label for="tunePart">조절할 부위</label><select id="tunePart">${Object.entries(tuningParts).map(([k,v])=>`<option value="${k}">${v}</option>`).join('')}</select><div id="tuneFields"></div><div class="buttons"><button id="resetPart">이 부위 초기화</button><button id="resetAll">전체 V9 초기화</button></div><div class="buttons"><button id="saveTuning">설정 내보내기</button><button id="loadTuning">설정 불러오기</button></div><input id="tuneFile" type="file" accept=".json,application/json" hidden><p id="tuneMessage" class="note" role="status">변경한 값은 이 브라우저에 자동 저장돼요.</p>`;
document.querySelector('aside').prepend(panel);
document.getElementById('resetAll').textContent='보내준 설정으로';
document.getElementById('resetPart').textContent='이 부위 되돌리기';
const style=document.createElement('style');style.textContent=`.layout{grid-template-columns:minmax(0,1fr) 310px;align-items:start}.stage{position:sticky;top:18px}#tuning select{width:100%;padding:11px;border:1px solid #d4dccc;border-radius:9px;background:#fffef9;font:inherit;color:inherit}#tuning label{margin:12px 0 7px}#tuning .tune-value{display:flex;align-items:center;gap:7px}#tuning input[type=number]{width:80px;padding:7px;border:1px solid #d4dccc;border-radius:7px;font:inherit;color:inherit}#tuning input[type=range]{flex:1;width:0;margin:0;min-width:50px}#tuning .buttons{flex-wrap:wrap;margin-top:15px}#tuning button{font-size:12px;padding:10px}#tuneMessage{min-height:40px}@media(max-width:760px){.layout{grid-template-columns:1fr}.stage{position:static}}`;document.head.append(style);
const notice=text=>document.getElementById('tuneMessage').textContent=text;
function applyTuning(persist=true){
 for(const b of RIG.bones){const a=adjustments[b.name],base=tuningDefaults.find(x=>x.name===b.name);if(!a)continue;
  b.pos=[base.pos[0]+a.x,base.pos[1]+a.y];b.width=base.width*a.width/100;b.height=base.height*a.height/100;
 }
 RIG.tuning=adjustments;
 if(persist)try{localStorage.setItem(tuningKey,JSON.stringify({version:1,adjustments}));notice('이 브라우저에 자동 저장했어요.');}catch{notice('자동 저장을 사용할 수 없어요. 설정 내보내기로 저장하세요.');}
 render(clock);
}
function drawFields(){
 const part=document.getElementById('tunePart').value,leg=part.endsWith('Leg');
 document.getElementById('tuneFields').innerHTML=Object.entries(tuningFields).filter(([k])=>leg||!k.startsWith('paw')).map(([k,[label,min,max,step,unit]])=>`<label for="tune-${k}">${label}</label><div class="tune-value"><input id="tune-${k}" data-field="${k}" type="range" min="${min}" max="${max}" step="${step}" value="${adjustments[part][k]}"><input aria-label="${label} 숫자" data-number="${k}" type="number" min="${min}" max="${max}" step="${step}" value="${adjustments[part][k]}"><span>${unit}</span></div>`).join('');
 document.querySelectorAll('#tuneFields input').forEach(el=>el.addEventListener('input',()=>{
  if(el.value==='')return;const k=el.dataset.field||el.dataset.number,n=Number(el.value);if(!Number.isFinite(n))return;
  const [,min,max]=tuningFields[k],v=Math.max(min,Math.min(max,n));adjustments[part][k]=v;
  document.querySelector(`[data-field="${k}"]`).value=v;document.querySelector(`[data-number="${k}"]`).value=v;applyTuning();
 }));
}
function validateTuning(data){
 if(data?.version!==1||!data.adjustments)throw Error('이 미리보기에서 내보낸 설정 파일을 선택해 주세요.');
 const clean=freshTuning();
 for(const k of Object.keys(clean))for(const f of Object.keys(tuningFields)){
  const v=data.adjustments[k]?.[f],[,min,max]=tuningFields[f];
  if(typeof v!=='number'||!Number.isFinite(v)||v<min||v>max)throw Error('설정값 범위가 올바르지 않아요.');clean[k][f]=v;
 }return clean;
}
const originalLegPose=legPose;
legPose=function(t,near,mats){const p=originalLegPose(t,near,mats),a=adjustments[near?'nearLeg':'farLeg'];p.sole=[p.sole[0]+a.x,p.sole[1]+a.y];p.floor+=a.y;return p;};
document.getElementById('tunePart').onchange=drawFields;
document.getElementById('resetPart').onclick=()=>{const k=document.getElementById('tunePart').value;adjustments[k]=approvedTuning()[k];applyTuning();drawFields();};
document.getElementById('resetAll').onclick=()=>{adjustments=approvedTuning();applyTuning();drawFields();notice('보내주신 설정으로 되돌렸어요.');};
document.getElementById('saveTuning').onclick=()=>{
 const blob=new Blob([JSON.stringify({version:1,adjustments},null,2)],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='starter-otter-v9-settings.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);notice('설정 파일을 내보냈어요. 이 파일을 전달하면 조절한 값을 다시 적용할 수 있어요.');
};
document.getElementById('loadTuning').onclick=()=>document.getElementById('tuneFile').click();
document.getElementById('tuneFile').onchange=async e=>{try{const file=e.target.files[0];if(!file)return;if(file.size>100000)throw Error('설정 파일이 너무 커요.');const clean=validateTuning(JSON.parse(await file.text()));adjustments=clean;applyTuning();drawFields();notice('저장한 설정을 불러왔어요.');}catch(err){notice(err.message);}finally{e.target.value='';}};
window.rigTuning={get values(){return structuredClone(adjustments)},apply(data){adjustments=validateTuning(data);applyTuning();drawFields();}};
window.rigReady.then(()=>{try{const saved=localStorage.getItem(tuningKey);if(saved)adjustments=validateTuning(JSON.parse(saved));}catch{notice('저장값을 읽지 못해 기본값으로 시작했어요.');}applyTuning(false);drawFields();});

