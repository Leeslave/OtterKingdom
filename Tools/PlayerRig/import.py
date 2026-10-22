import pathlib,json,re,uuid,importlib.util
root=pathlib.Path(__file__).resolve().parents[2]
cast=[{'stem':'Player'}]
spec=importlib.util.spec_from_file_location('crop',root/'Tools/QuarterRig/crop_unity_parts.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
def guid():return uuid.uuid4().hex
def meta(p,folder=False):
 f=pathlib.Path(str(p)+'.meta')
 if not f.exists():f.write_text('fileFormatVersion: 2\nguid: '+guid()+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else ''),encoding='utf-8')
 return re.search(r'guid: (\w+)',f.read_text()).group(1)
for c in cast:
 stem=c['stem'];out=root/('Assets/Resources/'+stem+'RigV1');art=root/('ArtSource/Otter/'+stem+'Rig/V1');meta(out,True)
 for part in json.loads((art/'imports.json').read_text()):
  p=out/(part['name']+'.png');g=meta(p);px,py=part['pivot'];pathlib.Path(str(p)+'.meta').write_text(m.META.format(guid=g,px=px,py=py,ppu=200,platforms=''.join(m.PLATFORM.format(target=t) for t in ['DefaultTexturePlatform','Standalone','Android','WebGL'])),encoding='utf-8')
 meta(out/'rig.json')
 prefab=root/('Assets/Prefabs/Plaza/PlazaOtter_'+stem+'Rigged.prefab')
 prefab.write_text((root/'Assets/Prefabs/Plaza/PlazaOtter_ArtisanRigged.prefab').read_text().replace('Artisan',stem).replace('characterScale: 0.463','characterScale: 0.45'),encoding='utf-8');meta(prefab)
print('Imported PlayerRigV1 and prefab')
