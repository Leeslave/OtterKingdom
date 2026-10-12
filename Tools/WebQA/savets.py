import os,json,sys,urllib.request
# 저장 시각만 바꿈 (오프라인 보상 시험: 과거로 돌리고 q.sh reload). savets.py get | savets.py set 2026-10-11T09:00:00.0000000Z
PORT=os.environ.get('QA_PORT','8777')
# Unity가 정한 IndexedDB 폴더 (같은 게임 주소면 같음). 다르면 idb.py keys로 확인해 QA_IDB에
IDB=os.environ.get('QA_IDB','/idbfs/6005a7260c1b7009def43bce62143115')
# usage: savets.py get | savets.py set ISO
mode=sys.argv[1]; ts=sys.argv[2] if len(sys.argv)>2 else ''
js='''(async()=>{const db=await new Promise(r=>{const q=indexedDB.open('/idbfs');q.onsuccess=()=>r(q.result)});const k='%s/save.json';
const v=await new Promise(r=>{const q=db.transaction('FILE_DATA').objectStore('FILE_DATA').get(k);q.onsuccess=()=>r(q.result)});
const s=new TextDecoder().decode(v.contents);const d=JSON.parse(s);
if('%s'==='set'){const s2=s.replace(/"lastSaveUtc":\\s*"[^"]*"/,'"lastSaveUtc": "%s"');v.contents=new TextEncoder().encode(s2);v.timestamp=new Date();
await new Promise((r,j)=>{const t=db.transaction('FILE_DATA','readwrite');t.objectStore('FILE_DATA').put(v,k);t.oncomplete=r;t.onerror=j});}
const v2=await new Promise(r=>{const q=db.transaction('FILE_DATA').objectStore('FILE_DATA').get(k);q.onsuccess=()=>r(q.result)});const d2=JSON.parse(new TextDecoder().decode(v2.contents));
return [d2.lastSaveUtc,d2.profile.level,d2.settlement.completedRequests.length]})()'''%(IDB,mode,ts)
r=urllib.request.Request(f'http://127.0.0.1:{PORT}/eval',data=json.dumps({'js':js}).encode(),method='POST')
print(urllib.request.urlopen(r).read().decode())
