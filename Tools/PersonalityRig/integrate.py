import pathlib,json,re,uuid,importlib.util
root=pathlib.Path(__file__).resolve().parents[2]
cast=json.loads((root/'Tools/PersonalityRig/cast.json').read_text(encoding='utf-8'))
spec=importlib.util.spec_from_file_location('crop',root/'Tools/QuarterRig/crop_unity_parts.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
def guid():return uuid.uuid4().hex
def meta(p,folder=False):
 f=pathlib.Path(str(p)+'.meta')
 if not f.exists():f.write_text('fileFormatVersion: 2\nguid: '+guid()+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else ''),encoding='utf-8')
 return re.search(r'guid: (\w+)',f.read_text()).group(1)
scene=root/'Assets/Scenes/Plaza_RiggedPlacement.unity';s=scene.read_text(encoding='utf-8')
docs=re.findall(r'(?ms)^--- !u!.*?(?=^--- !u!|\Z)',s);idx=next(i for i,d in enumerate(docs) if 'value: PlazaOtter_ArtisanRigged' in d)
template=docs[idx]+docs[idx+1];oldid=re.search(r'&(-?\d+)',docs[idx]).group(1);oldtr=re.search(r'&(-?\d+)',docs[idx+1]).group(1);oldguid=re.search(r'guid: (\w+)',template).group(1)
for i,c in enumerate(cast):
 stem=c['stem'];out=root/('Assets/Resources/'+stem+'RigV1');art=root/('ArtSource/Otter/'+stem+'Rig/V1');meta(out,True)
 for part in json.loads((art/'imports.json').read_text()):
  p=out/(part['name']+'.png');g=meta(p);px,py=part['pivot'];pathlib.Path(str(p)+'.meta').write_text(m.META.format(guid=g,px=px,py=py,ppu=200,platforms=''.join(m.PLATFORM.format(target=t) for t in ['DefaultTexturePlatform','Standalone','Android','WebGL'])),encoding='utf-8')
 meta(out/'rig.json')
 prefab=root/('Assets/Prefabs/Plaza/PlazaOtter_'+stem+'Rigged.prefab')
 prefab.write_text((root/'Assets/Prefabs/Plaza/PlazaOtter_ArtisanRigged.prefab').read_text().replace('Artisan',stem).replace('characterScale: 0.463','characterScale: 0.45'),encoding='utf-8');g=meta(prefab)
 if 'value: PlazaOtter_'+stem+'Rigged' not in s:
  a=880028001+i*1000;b=a+1
  while re.search(r'&'+str(a)+r'\b',s):a+=100000;b=a+1
  block=template.replace(oldid,str(a)).replace(oldtr,str(b)).replace(oldguid,g).replace('Artisan',stem)
  for axis,v in [('x',-4+(i%5)*2),('y',-5.5-(i//5)*1.5)]:block=re.sub(r'(propertyPath: m_LocalPosition\.'+axis+r'\n\s+value:) [^\n]+',lambda x:x.group(1)+' '+str(v),block)
  s=s.replace('- {fileID: '+oldtr+'}','- {fileID: '+oldtr+'}\n  - {fileID: '+str(b)+'}',1)
  s=s.replace('--- !u!1660057539',block+'--- !u!1660057539',1)
ids=re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M);assert len(ids)==len(set(ids))
scene.write_text(s,encoding='utf-8')
print('Imported sprites and placed',len(cast),'prefabs')
