import pathlib,json
r=pathlib.Path(__file__).resolve().parents[2]
p=r/'Tools/HeadRefresh/gallery.cjs';s=p.read_text(encoding='utf-8');s=s[:s.index('const old=')].replace('34종','22종');p.write_text(s,encoding='utf-8')
p=r/'ArtSource/Otter/PersonalityCast/index.html';p.write_text('<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=../HeadRefresh/V1/index.html"><a href="../HeadRefresh/V1/index.html">정리된 해달 목록</a>',encoding='utf-8')
p=r/'Tools/HeadRefresh/apply.cjs';s=p.read_text(encoding='utf-8').replace("&&!['Flower','Stripe','Hoodie','Knit','Overalls','Plaid','BearPajama','Beanie','Scholar','Varsity'].includes(name)",'').replace('ArtSource/Otter/ShyRig/V1/preview.html','ArtSource/Otter/WalkerRig/V1/preview.html');p.write_text(s,encoding='utf-8')
# References must not point to any prefab or sprite moved out of Assets.
archive=r/'ArtSource/Otter/RetiredRigDuplicates/20261019/Assets'
import re
retired=set()
for f in archive.rglob('*.meta'):
 m=re.search(r'^guid: (\w+)',f.read_text(encoding='utf-8'),re.M)
 if m:retired.add(m.group(1))
bad=[]
for f in (r/'Assets').rglob('*'):
 if f.suffix not in ['.prefab','.unity','.asset','.cs']:continue
 text=f.read_text(encoding='utf-8');matches=retired.intersection(re.findall(r'guid: (\w+)',text))
 if matches:bad.append(str(f.relative_to(r)))
assert not bad,bad
cast=json.loads((r/'ArtSource/Otter/HeadRefresh/V1/manifest.json').read_text(encoding='utf-8'));assert len(cast)==22
print('PASS: 22 retained looks; no dangling archived-asset GUIDs.')
