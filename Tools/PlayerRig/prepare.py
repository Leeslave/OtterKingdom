import pathlib,re
r=pathlib.Path(__file__).resolve().parents[2];tools=r/'Tools/PlayerRig';art=r/'ArtSource/Otter/PlayerRig/V1';art.mkdir(parents=True,exist_ok=True)
s=(r/'Tools/WalkerRig/build.cjs').read_text(encoding='utf-8').replace('Walker','Player').replace('walker','player').replace('산책하는','주인공').replace('48번','V2 시안').replace('[0,455,800,985,1205,1536]','[0,475,815,1010,1230,1536]').replace('b.height=215','b.height=235').replace('Plaza_RiggedPlacement 씬에도 같은 파츠와 리깅을 연결했습니다.','주인공 V2 시안을 리깅한 미리보기입니다.')
(tools/'build.cjs').write_text(s,encoding='utf-8')
(tools/'check.cjs').write_text((r/'Tools/WalkerRig/check.cjs').read_text(encoding='utf-8').replace('Walker','Player'),encoding='utf-8')
(art/'approved-settings.json').write_text((r/'ArtSource/Otter/WalkerRig/V1/approved-settings.json').read_text(encoding='utf-8'),encoding='utf-8')
s=(r/'Tools/PersonalityRig/validate-unity.py').read_text(encoding='utf-8');s=re.sub(r"cast=json.loads\(.*?\)\n", "cast=[{'stem':'Player'}]\n",s,count=1).replace('PersonalityRigValidation','PlayerRigValidation');(tools/'validate-unity.py').write_text(s,encoding='utf-8')
