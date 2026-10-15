import pathlib,shutil,json
root=pathlib.Path(__file__).resolve().parents[2];out=root/'tmp/PersonalityRigValidation';cast=json.loads((root/'Tools/PersonalityRig/cast.json').read_text(encoding='utf-8'))
for d in ['Assets/Resources','Assets/Editor','ProjectSettings','Packages']:(out/d).mkdir(parents=True,exist_ok=True)
shutil.copy2(root/'Assets/Scripts/View/QuarterRigView.cs',out/'Assets/QuarterRigView.cs')
shutil.copy2(root/'ProjectSettings/ProjectVersion.txt',out/'ProjectSettings/ProjectVersion.txt')
(out/'Packages/manifest.json').write_text(json.dumps({'dependencies':{'com.unity.modules.'+k:'1.0.0' for k in ['animation','audio','imgui','imageconversion','jsonserialize','physics','physics2d','ui','uielements']}}))
for c in cast:
 p=root/('Assets/Resources/'+c['stem']+'RigV1');shutil.copytree(p,out/'Assets/Resources'/p.name,dirs_exist_ok=True);shutil.copy2(str(p)+'.meta',str(out/'Assets/Resources'/p.name)+'.meta')
names=','.join('"'+c['stem']+'"' for c in cast)
code='''using UnityEngine;using UnityEditor;using System.Reflection;using System.IO;
public static class ValidateCast {public static void Run(){foreach(var name in new[]{NAMES}){
var go=new GameObject(name);go.SetActive(false);var rig=go.AddComponent<QuarterRigView>();var so=new SerializedObject(rig);so.FindProperty("resourceFolder").stringValue=name+"RigV1";so.FindProperty("characterScale").floatValue=.45f;so.ApplyModifiedPropertiesWithoutUndo();go.SetActive(true);
var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(QuarterRigView).GetField("walkBlend",flags)?.SetValue(rig,1f);typeof(QuarterRigView).GetField("phase",flags)?.SetValue(rig,.25f);
int i=0;foreach(var dir in new[]{new Vector2(1,-1),new Vector2(-1,-1),Vector2.one,new Vector2(-1,1)}){rig.Face(dir);typeof(QuarterRigView).GetMethod("ApplyPose",flags).Invoke(rig,null);var rs=go.GetComponentsInChildren<SpriteRenderer>();if(rs.Length!=7)throw new System.Exception(name+" part count "+rs.Length);foreach(var r in rs)if(r.sprite==null)throw new System.Exception(name+" missing sprite");
var cameraGo=new GameObject("Camera");var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.05f;camera.transform.position=new Vector3(0,.85f,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.88f,.91f,.84f);var rt=new RenderTexture(512,512,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(512,512,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,512,512),0,0);tex.Apply();File.WriteAllBytes(Path.GetFullPath("../../ArtSource/Otter/"+name+"Rig/V1/unity-"+i+".png"),tex.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(cameraGo);i++;}
Object.DestroyImmediate(go);Debug.Log(name+" validated");}Debug.Log("CAST_VALIDATION_PASSED");}}
'''.replace('NAMES',names)
(out/'Assets/Editor/ValidateCast.cs').write_text(code,encoding='utf-8')
