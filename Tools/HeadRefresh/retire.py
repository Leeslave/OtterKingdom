import pathlib,json,re
root=pathlib.Path(__file__).resolve().parents[2]
mapping=json.loads((root/'Tools/HeadRefresh/retired.json').read_text(encoding='utf-8'))
guids={}
for old,new in mapping.items():
 def guid(n):return re.search(r'guid: (\w+)',(root/f'Assets/Prefabs/Plaza/PlazaOtter_{n}Rigged.prefab.meta').read_text(encoding='utf-8')).group(1)
 guids[guid(old)]=guid(new)
changed=[]
for file in (root/'Assets').rglob('*'):
 if file.suffix not in ['.unity','.prefab','.asset']:continue
 if any(file.name==f'PlazaOtter_{n}Rigged.prefab' for n in mapping):continue
 text=file.read_text(encoding='utf-8');s=text
 if file.name=='Plaza_RiggedPlacement.unity':
  docs=re.split(r'(?=^--- !u!)',s,flags=re.M);remove=set()
  for d in docs:
   if d.startswith('--- !u!1001') and any('value: PlazaOtter_'+n+'Rigged\n' in d for n in mapping):remove.add(re.search(r'&(-?\d+)',d).group(1))
  for d in docs:
   if any('m_PrefabInstance: {fileID: '+n+'}' in d for n in remove.copy()):remove.add(re.search(r'&(-?\d+)',d).group(1))
  s=''.join(d for d in docs if not (re.match(r'--- !u!\d+ &(-?\d+)',d) and re.match(r'--- !u!\d+ &(-?\d+)',d).group(1) in remove))
  for n in remove:s=re.sub(r'^\s*- \{fileID: '+n+r'\}\n','',s,flags=re.M)
 for old,new in guids.items():s=s.replace(old,new)
 if s!=text:file.write_text(s,encoding='utf-8');changed.append(str(file.relative_to(root)))
setup=root/'Assets/Editor/Settlement/RiggedLookSetup.cs';s=setup.read_text(encoding='utf-8')
for old,new in mapping.items():s=s.replace('"'+old+'"','"'+new+'"')
s=re.sub(r'private static readonly string\[\] Wanderers = .*?;', 'private static readonly string[] Wanderers = { "BearPajama", "Walker", "Stripe", "Plaid", "Flower", "Hoodie", "Quiet" };',s)
setup.write_text(s,encoding='utf-8')
manifest=root/'ArtSource/Otter/HeadRefresh/V1/manifest.json';cast=json.loads(manifest.read_text(encoding='utf-8'));manifest.write_text(json.dumps([c for c in cast if c['name'] not in mapping],ensure_ascii=False,indent=2),encoding='utf-8')
p=root/'Tools/PersonalityRig/cast.json';cast=json.loads(p.read_text(encoding='utf-8'));p.write_text(json.dumps([c for c in cast if c['stem'] not in mapping],ensure_ascii=False,indent=2),encoding='utf-8')
p=root/'Tools/SheetRigs/build.py';s=p.read_text(encoding='utf-8');s='\n'.join(line for line in s.split('\n') if not any(re.match(r'\s*"'+n+r'":',line) for n in mapping));p.write_text(s,encoding='utf-8')
p=root/'Tools/HeadRefresh/apply.cjs';s=p.read_text(encoding='utf-8');s=s.replace('const name=names[i],row=',"if(require('./retired.json')[names[i]])continue;\nconst name=names[i],row=");p.write_text(s,encoding='utf-8')
p=root/'Tools/PersonalityRig/prepare.cjs';s=p.read_text(encoding='utf-8').replace('for(const [i,c]of cast.entries()){','for(const c of cast){const i=c.number-41;');p.write_text(s,encoding='utf-8')
print(json.dumps({'retired':list(mapping),'updatedReferences':changed},ensure_ascii=False,indent=2))
