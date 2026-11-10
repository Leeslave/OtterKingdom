import json, re
from pathlib import Path
from collections import Counter

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Assets/Scriptable Obejects'
def parse(path):
    text = path.read_text(encoding='utf-8-sig')
    data = {}
    matches = list(re.finditer(r'^  (\w+):(?: (.*))?$', text, re.M))
    for i,m in enumerate(matches):
        raw = text[m.start():matches[i+1].start() if i+1<len(matches) else len(text)].split(':',1)[1].strip()
        if raw.startswith('"'):
            raw = re.sub(r'\\\r?\n\s*', '', raw)
            raw = ' '.join(line.strip() for line in raw.splitlines())
            raw = re.sub(r'\\x([0-9a-fA-F]{2})', r'\\u00\1', raw)
            raw = re.sub(r'\\U([0-9a-fA-F]{8})', lambda m: chr(int(m[1],16)), raw)
            data[m[1]] = json.loads(raw)
        elif re.fullmatch(r'-?\d+',raw): data[m[1]] = int(raw)
        elif re.fullmatch(r'-?\d+\.\d+',raw): data[m[1]] = float(raw)
        else: data[m[1]] = raw
    return data
assets = {}
by_path = {}
for p in BASE.rglob('*.asset'):
    meta=Path(str(p)+'.meta')
    if not meta.exists(): continue
    guid = re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
    d = parse(p); d['path'] = p.relative_to(ROOT).as_posix(); d['guid']=guid
    assets[guid]=d; by_path[p.relative_to(BASE).as_posix()]=d
def refs(v): return [assets[g] for g in re.findall(r'guid: ([a-f0-9]{32})', str(v)) if g in assets]
def ref(v):
    a=refs(v); return a[0] if a else {}
def name(d): return d.get('_displayName') or d.get('_title') or d.get('_name') or d.get('m_Name','')
qs=refs(by_path['Quest/QuestDatabase.asset']['_quests'])
config=by_path['Settlement/SettlementConfig.asset']
data={'assets':assets,'quests':qs,'config':config, 'paths':by_path}
(ROOT/'tmp/game_flow_data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('Quest counts', Counter(q['_kind'] for q in qs))
for level in range(1,51):
    row=sorted([q for q in qs if q['_kind']==0 and q['_requiredLevel']==level],key=lambda q:q['_sortOrder'])
    print(level, ' → '.join(f"{q['_title']} [{q['_description']}; XP {q['_expReward']}]" for q in row))
print('CONFIG', {k:v for k,v in config.items() if isinstance(v,(int,float))})
for field in ('_requests','_projects','_buildings'):
    print(field)
    for d in refs(config.get(field,'')):
        print(name(d), d.get('_requestId',d.get('_projectId',d.get('_buildingId'))), 'level',d.get('_requiredLevel'), 'reach',d.get('_kingdomLevel'), 'requires',d.get('_requiredDevelopment'), 'result',d.get('_resultDevelopment'))
