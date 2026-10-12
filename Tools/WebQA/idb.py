"""브라우저 IndexedDB의 게임 세이브 다루기 (driver.js가 떠 있어야 함).

  python3 idb.py keys                 IndexedDB에 있는 파일 경로 (세이브 폴더 hash 확인)
  python3 idb.py load <save.json>     세이브 파일을 브라우저에 넣음 → q.sh reload 로 새로 고치면 그 세이브로 시작
  python3 idb.py dump <out.json>      지금 브라우저의 세이브를 파일로 꺼냄 (먼저 게임에서 저장시키기: README "저장")

Unity Play 빌드는 autoSyncPersistentDataPath가 꺼져 있어 게임이 save.json을 바꿔도 IndexedDB로 바로 안 옮겨짐.
설정 화면에서 값을 바꾸고 닫으면 PlayerPrefs.Save()가 파일 전체를 IndexedDB로 옮긴다.
"""
import base64
import json
import os
import sys
import urllib.request

PORT = os.environ.get('QA_PORT', '8777')
IDB = os.environ.get('QA_IDB', '/idbfs/de024c23b4f64e0d2cae79afd60e4395')

OPEN = "const db=await new Promise((r,j)=>{const q=indexedDB.open('/idbfs');q.onsuccess=()=>r(q.result);q.onerror=j});"


def run(js):
    req = urllib.request.Request(f'http://127.0.0.1:{PORT}/eval', data=json.dumps({'js': js}).encode(), method='POST')
    out = urllib.request.urlopen(req, timeout=300).read().decode()
    if out.startswith('ERR'):
        sys.exit(out)
    return json.loads(out)


def keys():
    return run("(async()=>{" + OPEN + "return await new Promise(r=>{const q=db.transaction('FILE_DATA').objectStore('FILE_DATA').getAllKeys();q.onsuccess=()=>r(q.result.map(String))})})()")


def load(path):
    b64 = base64.b64encode(open(path, 'rb').read()).decode()
    js = ("(async()=>{" + OPEN +
          f"const base='{IDB}';"
          "const pp=await new Promise(r=>{const q=db.transaction('FILE_DATA').objectStore('FILE_DATA').get(base+'/PlayerPrefs');q.onsuccess=()=>r(q.result)});"
          f"const bin=atob('{b64}');const u=new Uint8Array(bin.length);for(let i=0;i<bin.length;i++)u[i]=bin.charCodeAt(i);"
          "await new Promise((r,j)=>{const t=db.transaction('FILE_DATA','readwrite');t.objectStore('FILE_DATA').put({timestamp:new Date(),mode:pp?pp.mode:33206,contents:u},base+'/save.json');t.oncomplete=r;t.onerror=j});"
          "return u.length})()")
    return run(js)


def dump(path):
    js = ("(async()=>{" + OPEN +
          f"const v=await new Promise(r=>{{const q=db.transaction('FILE_DATA').objectStore('FILE_DATA').get('{IDB}/save.json');q.onsuccess=()=>r(q.result)}});"
          "return v?new TextDecoder().decode(v.contents):null})()")
    text = run(js)
    if text is None:
        sys.exit('save.json이 아직 IndexedDB에 없음 (게임에서 먼저 저장)')
    open(path, 'w', encoding='utf-8').write(text)
    d = json.loads(text)
    return {'lastSaveUtc': d.get('lastSaveUtc'), 'profile': d.get('profile')}


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    if cmd == 'keys':
        print('\n'.join(keys()))
    elif cmd == 'load' and len(sys.argv) > 2:
        print('넣음', load(sys.argv[2]), 'bytes → q.sh reload')
    elif cmd == 'dump' and len(sys.argv) > 2:
        print(dump(sys.argv[2]))
    else:
        print(__doc__)
