using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 농부 해달을 바탕으로 색 · 소품만 바꾼 해달 20마리(Tools/OtterCast/farmer_cast.py가 만든 그림)를 게임에 넣는다.
/// 해달마다 묶음 시트(Assets/Sprites/Characters/Cast/Cast_&lt;id&gt;.png, 6열 × 312×180 칸, 0.75배)를 잘라
/// 농부 해달 애니메이션(FarmerOtter.controller)의 클립을 같은 박자로 그림만 바꿔 덮어쓴 컨트롤러와 광장 프리팹
/// (PlazaOtter_Cast_&lt;id&gt;)을 만들고, Assignments의 해달(농부 새싹이를 뺀 20마리)에게 광장 모습 · 얼굴 · 도감 얼굴을 연결한다.
/// 옛 얼굴을 쓰던 퀘스트 · 부탁 · 튜토리얼 안내 얼굴도 그 해달의 새 얼굴로, 광장의 건설 해달(뚝딱이)도 새 모습으로 바꾼다.
/// 광산 · 낚시터에서 일하는 모습(곡괭이질 · 낚싯대)은 농부 그림에 없는 동작이라 그대로.
/// 배치 모드: -executeMethod OtterCastSetup.Apply
/// </summary>
public static class OtterCastSetup
{
    private const string SheetDir = "Assets/Sprites/Characters/Cast";
    private const string IconDir = "Assets/Art/Otter";
    private const string AnimDir = "Assets/Animations/Cast";
    private const string PrefabDir = "Assets/Prefabs/Plaza";
    private const string BasePrefabPath = PrefabDir + "/PlazaOtter.prefab";

    // farmer_cast.py와 같아야 함: 칸 크기 · 열 수 · 클립 순서 (클립마다 8칸)
    private const int Cols = 6;
    private const int CellW = 312;
    private const int CellH = 180;
    private const int FramesPerClip = 8;
    // 농부 해달 시트(PPU 120)를 0.75배로 줄였으므로 같은 크기로 보이게 120 × 0.75
    private const float PixelsPerUnit = 90f;
    private static readonly string[] Clips = { "Walk", "Walk_Down", "Walk_Up", "Harvest", "Eat", "Net", "Squat", "Stretch" };

    /// <summary>만든 모습 20가지 (Tools/OtterCast/cast.json의 id)</summary>
    internal static readonly string[] Looks =
    {
        "Blossom", "Sailor", "Sprout", "Cherry", "Explorer", "Night", "Snow", "Honey", "Mint", "Cocoa",
        "Lilac", "Pumpkin", "Ocean", "Berry", "Forest", "Sunny", "Ink", "Miner", "Sky", "Fisher",
    };

    /// <summary>해달 → 모습 (농부 새싹이만 원래 모습)</summary>
    private static readonly (string otterId, string look)[] Assignments =
    {
        ("otter_first", "Explorer"),        // 몽실: 처음 온 탐험가
        ("otter_sleepy", "Night"),          // 꾸벅이: 늘 졸린 (밤하늘 · 별)
        ("otter_painter", "Berry"),         // 물감이
        ("otter_builder", "Sunny"),         // 뚝딱이: 건설
        ("otter_miner", "Miner"),           // 깡깡이: 머리 등
        ("otter_fisher", "Fisher"),         // 첨벙이: 물고기 핀
        ("otter_receptionist", "Sailor"),   // 또박이: 단정한 제복
        ("otter_p3_neighbor", "Snow"),      // 포근이: 목도리
        ("otter_toy_sallang", "Blossom"),   // 살랑이: 벚꽃
        ("otter_toy_kungkung", "Forest"),   // 나무꾼
        ("otter_toy_degul", "Mint"),        // 조약돌
        ("otter_toy_bodeul", "Sprout"),     // 풀잎 · 농사
        ("otter_toy_pongdang", "Ocean"),    // 물방울 · 낚시
        ("otter_toy_yeongcha", "Pumpkin"),  // 힘센 운반
        ("otter_toy_jjalrang", "Honey"),    // 동전 · 장사 (금색 방울)
        ("otter_toy_jaejal", "Cherry"),     // 수다쟁이
        ("otter_toy_kongkong", "Cocoa"),    // 망치 소리 · 건설
        ("otter_toy_kkomkkom", "Lilac"),    // 손재주
        ("otter_toy_duribeon", "Sky"),      // 길찾기
        ("otter_toy_kkeujeok", "Ink"),      // 밤의 메모쟁이 (먹물)
    };

    // 옛 얼굴을 직접 쓰던 곳: (에셋, 칸, 이 해달의 얼굴로). 부탁 그림은 부탁한 해달, 퀘스트는 그 퀘스트의 해달
    private static readonly (string asset, string field, string otterId)[] FaceUses =
    {
        ("Assets/Scriptable Obejects/Quest/Quests/M03_Miner.asset", "_icon", "otter_miner"),
        ("Assets/Scriptable Obejects/Quest/Quests/M06_Clerk.asset", "_icon", "otter_receptionist"),
        ("Assets/Scriptable Obejects/Quest/Quests/M15_Fisher.asset", "_icon", "otter_fisher"),
        ("Assets/Scriptable Obejects/Quest/Quests/M16_Builder.asset", "_icon", "otter_toy_kongkong"),
        ("Assets/Scriptable Obejects/Quest/Quests/M17_Recorder.asset", "_icon", "otter_toy_kkeujeok"),
        ("Assets/Scriptable Obejects/Quest/Quests/Main_Build_3.asset", "_icon", "otter_builder"),
        ("Assets/Scriptable Obejects/Quest/Quests/Main_Otter_1.asset", "_icon", "otter_fisher"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_assign_receptionist.asset", "_icon", "otter_receptionist"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_assign_miner.asset", "_icon", "otter_miner"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_assign_fisher.asset", "_icon", "otter_fisher"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p4_trader_home.asset", "_icon", "otter_toy_jjalrang"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p4_farmer_home.asset", "_icon", "otter_toy_bodeul"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p4_miner_home.asset", "_icon", "otter_toy_degul"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p4_lumber_home.asset", "_icon", "otter_sleepy"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p5_fisher_home.asset", "_icon", "otter_toy_pongdang"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p5_builder_home.asset", "_icon", "otter_toy_kongkong"),
        ("Assets/Scriptable Obejects/Settlement/Requests/req_p5_recorder_home.asset", "_icon", "otter_toy_kkeujeok"),
        ("Assets/Scriptable Obejects/Settlement/LifeRequests/Life_snack.asset", "_icon", "otter_first"),
        ("Assets/Resources/TutorialStyle.asset", "_guidePortrait", "otter_first"),
    };

    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";
    private const string BuilderOtterId = "otter_builder";
    // 발 기준점 (칸 아래에서 발바닥까지 = 농부 시트 240칸 중 9칸) — 루트에 그림을 바로 다는 건설 해달용
    private const float FeetPivotY = 9f / 240f;

    internal static string LookOf(string otterId) =>
        Assignments.Where(a => a.otterId == otterId).Select(a => a.look).FirstOrDefault();

    internal static GameObject Prefab(string otterId)
    {
        string look = LookOf(otterId);
        return look == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(look));
    }

    internal static Sprite Portrait(string otterId)
    {
        string look = LookOf(otterId);
        return look == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/ICON_Otter_Cast_{look}.png");
    }

    /// <summary>다른 설정 메뉴가 해달을 다시 채울 때 부름: 모습이 정해진 해달이면 광장 모습 · 얼굴을 덮어씀 (아직 안 만들었으면 그대로)</summary>
    internal static void ApplyTo(SerializedObject otterSo, string otterId)
    {
        var prefab = Prefab(otterId);
        var portrait = Portrait(otterId);
        if (prefab == null || portrait == null)
            return;
        // 광장 프리팹이 없던 해달(건설 해달은 씬에 따로 있음)은 그대로 없게
        var plaza = otterSo.FindProperty("_plazaPrefab");
        if (plaza.objectReferenceValue != null)
            plaza.objectReferenceValue = prefab;
        otterSo.FindProperty("_plazaTint").colorValue = Color.white;
        otterSo.FindProperty("_portrait").objectReferenceValue = portrait;
    }

    private static string PrefabPath(string look) => $"{PrefabDir}/PlazaOtter_Cast_{look}.prefab";

    [MenuItem("Tools/Settlement/Apply Otter Cast")]
    public static void Apply()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        var baseAnimator = basePrefab != null ? basePrefab.GetComponentInChildren<Animator>(true) : null;
        var baseController = baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
        if (baseController == null)
        {
            Debug.LogError($"[OtterCastSetup] {BasePrefabPath}의 농부 해달 애니메이션이 없습니다.");
            return;
        }

        RemoveStaleLooks();
        int made = 0;
        Dictionary<string, Sprite[]> builderFrames = null;
        foreach (var look in Looks)
        {
            string sheetPath = $"{SheetDir}/Cast_{look}.png";
            if (!File.Exists(sheetPath))
            {
                Debug.LogWarning($"[OtterCastSetup] 그림이 없습니다: {sheetPath} (python Tools/OtterCast/farmer_cast.py)");
                continue;
            }
            bool forBuilder = look == LookOf(BuilderOtterId);
            var frames = Slice(sheetPath, look, forBuilder);
            if (forBuilder)
                builderFrames = frames;
            var controller = BuildController(look, baseController, frames);
            BuildPrefab(look, controller, frames["Harvest"][0]);
            CollectionSetup.ImportSprite(IconDir, $"ICON_Otter_Cast_{look}");
            made++;
        }
        AssetDatabase.SaveAssets();

        int linked = Link();
        int faces = RelinkFaces();
        AssetDatabase.SaveAssets();
        bool builder = builderFrames != null && ReskinBuilder(builderFrames);
        Debug.Log($"[OtterCastSetup] 모습 {made}가지를 만들고 해달 {linked}마리 · 얼굴 {faces}곳에 연결했습니다. 건설 해달 {(builder ? "바꿈" : "그대로")}");
    }

    // 클립 → 8칸 (칸 순서는 farmer_cast.py의 PACK_CLIPS)
    // forBuilder: 건설 해달(그림을 루트에 바로 닮)용으로 발 기준점 그림("…_Feet")도 같이 자름
    private static Dictionary<string, Sprite[]> Slice(string path, string look, bool forBuilder)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.maxTextureSize = 2048;

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        int rows = Mathf.CeilToInt(Clips.Length * FramesPerClip / (float)Cols);
        if (texW != Cols * CellW || texH != rows * CellH)
            Debug.LogError($"[OtterCastSetup] {path}는 {texW}×{texH}, {Cols * CellW}×{rows * CellH}이어야 합니다. farmer_cast.py로 다시 만드세요.");

        var metas = new List<SpriteMetaData>();
        for (int c = 0; c < Clips.Length; c++)
        {
            for (int f = 0; f < FramesPerClip; f++)
            {
                int k = c * FramesPerClip + f;
                int col = k % Cols, row = k / Cols;
                metas.Add(new SpriteMetaData
                {
                    name = $"Cast_{look}_{Clips[c]}_{f}",
                    rect = new Rect(col * CellW, texH - (row + 1) * CellH, CellW, CellH),
                    // 농부 해달과 같은 가운데 기준점: 좌우 뒤집어도 제자리
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
                if (forBuilder)
                {
                    metas.Add(new SpriteMetaData
                    {
                        name = $"Cast_{look}_{Clips[c]}_{f}_Feet",
                        rect = new Rect(col * CellW, texH - (row + 1) * CellH, CellW, CellH),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, FeetPivotY),
                    });
                }
            }
        }
#pragma warning disable CS0618 // spritesheet는 쓰지 말라고 하지만 여전히 가장 간단한 방법 (FarmerOtterSpriteSetup과 같음)
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        var result = Clips.ToDictionary(clip => clip,
            clip => Enumerable.Range(0, FramesPerClip).Select(f => all[$"Cast_{look}_{clip}_{f}"]).ToArray());
        if (forBuilder)
        {
            foreach (var clip in Clips)
                result[clip + "_Feet"] = Enumerable.Range(0, FramesPerClip).Select(f => all[$"Cast_{look}_{clip}_{f}_Feet"]).ToArray();
        }
        return result;
    }

    // 농부 해달 그림 이름 → (클립, 칸). 본 시트 "FarmerOtter_Sheet_Walk_Down_3", 동작 시트 "FarmerOtter_IdleAction_Eat_0_5"
    private static Sprite Map(Sprite farmer, Dictionary<string, Sprite[]> frames)
    {
        if (farmer == null)
            return null;
        string name = farmer.name;
        const string main = "FarmerOtter_Sheet_";
        const string action = "FarmerOtter_IdleAction_";
        if (name.StartsWith(main))
        {
            string rest = name.Substring(main.Length);
            int cut = rest.LastIndexOf('_');
            string clip = rest.Substring(0, cut);
            if (clip == "Walk_Left")
                clip = "Walk"; // 왼쪽은 좌우 뒤집기로 (안 씀)
            if (frames.TryGetValue(clip, out var row) && int.TryParse(rest.Substring(cut + 1), out int col))
                return row[Mathf.Clamp(col, 0, row.Length - 1)];
        }
        else if (name.StartsWith(action))
        {
            var parts = name.Substring(action.Length).Split('_');
            if (parts.Length == 3 && frames.TryGetValue(parts[0], out var row) && int.TryParse(parts[2], out int col))
                return row[Mathf.Clamp(col, 0, row.Length - 1)];
        }
        Debug.LogWarning($"[OtterCastSetup] 바꿀 그림을 모릅니다: {name}");
        return farmer;
    }

    private static AnimatorOverrideController BuildController(string look, RuntimeAnimatorController baseController, Dictionary<string, Sprite[]> frames)
    {
        string dir = $"{AnimDir}/{look}";
        if (AssetDatabase.IsValidFolder(dir))
            AssetDatabase.DeleteAsset(dir);
        Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();

        var controller = new AnimatorOverrideController(baseController) { name = $"Cast_{look}" };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            var original = overrides[i].Key;
            var clip = new AnimationClip { frameRate = original.frameRate };
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(original));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(original))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(original, binding);
                for (int k = 0; k < keys.Length; k++)
                    keys[k].value = Map(keys[k].value as Sprite, frames);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(original))
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(original, binding));
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(original));
            AssetDatabase.CreateAsset(clip, $"{dir}/{original.name.Replace("FarmerOtter", $"Cast_{look}")}.anim");
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clip);
        }
        controller.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(controller, $"{dir}/Cast_{look}.overrideController");
        return controller;
    }

    // 농부 모습 광장 프리팹(PlazaOtter)을 복사해 애니메이션 · 첫 그림만 바꿈. 이미 있으면 그 자리에서 고침
    private static void BuildPrefab(string look, RuntimeAnimatorController controller, Sprite idle)
    {
        string path = PrefabPath(look);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(BasePrefabPath, path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = $"PlazaOtter_Cast_{look}";
            var animator = root.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            var renderer = animator.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = idle;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 정해진 해달에게 광장 모습 · 얼굴 · 도감 얼굴
    private static int Link()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(SettlementSetup.ConfigPath);
        if (config == null)
        {
            Debug.LogError("[OtterCastSetup] 정착 설정이 없습니다.");
            return 0;
        }
        int linked = 0;
        foreach (var otter in config.Otters)
        {
            if (otter == null || LookOf(otter.OtterId) == null)
                continue;
            var so = new SerializedObject(otter);
            ApplyTo(so, otter.OtterId);
            so.ApplyModifiedPropertiesWithoutUndo();

            var entry = so.FindProperty("_collectionEntry").objectReferenceValue ?? FindEntry(otter.OtterId);
            if (entry != null)
            {
                var entrySo = new SerializedObject(entry);
                entrySo.FindProperty("_portrait").objectReferenceValue = Portrait(otter.OtterId);
                entrySo.ApplyModifiedPropertiesWithoutUndo();
            }
            linked++;
        }
        return linked;
    }

    private static CollectionEntry FindEntry(string otterId) =>
        AssetDatabase.FindAssets("t:CollectionEntry")
            .Select(guid => AssetDatabase.LoadAssetAtPath<CollectionEntry>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(e => e != null && e.EntryId == otterId);

    private static int RelinkFaces()
    {
        int count = 0;
        foreach (var (asset, field, otterId) in FaceUses)
        {
            var target = AssetDatabase.LoadAssetAtPath<Object>(asset);
            var face = Portrait(otterId);
            if (target == null || face == null)
            {
                Debug.LogWarning($"[OtterCastSetup] 얼굴을 바꿀 수 없습니다: {asset}");
                continue;
            }
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = face;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    // 광장 씬의 건설 해달: 걷기 · 쉬기는 새 모습, 일하기(망치질)는 쪼그려 앉는 동작으로 (수확 동작은 손에 풀이 있음)
    private static bool ReskinBuilder(Dictionary<string, Sprite[]> frames)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;
        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var builder = Object.FindFirstObjectByType<BuilderOtterController>(FindObjectsInactive.Include);
        if (builder == null)
        {
            Debug.LogWarning("[OtterCastSetup] 광장 씬에 건설 해달이 없습니다.");
            return false;
        }
        var idle = frames["Harvest_Feet"][0];
        var clips = new (string name, Sprite[] frames, float fps)[]
        {
            (BuilderOtterController.ClipIdle, new[] { idle }, 1f),
            (BuilderOtterController.ClipWalkRight, frames["Walk_Feet"], 10f),
            (BuilderOtterController.ClipWalkLeft, frames["Walk_Feet"], 10f), // 좌우 뒤집어 씀 (_mirrorLeftWalk)
            (BuilderOtterController.ClipWalkDown, frames["Walk_Down_Feet"], 10f),
            (BuilderOtterController.ClipWalkUp, frames["Walk_Up_Feet"], 10f),
            (BuilderOtterController.ClipWork, frames["Squat_Feet"], 6f),
        };
        var animator = builder.GetComponent<SpriteFrameAnimator>();
        var animSo = new SerializedObject(animator);
        var clipsProp = animSo.FindProperty("clips");
        clipsProp.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var element = clipsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("name").stringValue = clips[i].name;
            element.FindPropertyRelative("fps").floatValue = clips[i].fps;
            element.FindPropertyRelative("loop").boolValue = true;
            var list = element.FindPropertyRelative("frames");
            list.arraySize = clips[i].frames.Length;
            for (int f = 0; f < clips[i].frames.Length; f++)
                list.GetArrayElementAtIndex(f).objectReferenceValue = clips[i].frames[f];
        }
        animSo.ApplyModifiedPropertiesWithoutUndo();
        builder.GetComponent<SpriteRenderer>().sprite = idle;
        // 광부 그림은 작아서 1.1배였음. 새 모습은 광장 해달과 같은 크기
        float oldScale = builder.transform.localScale.x;
        builder.transform.localScale = Vector3.one;
        var bubble = builder.transform.Find("WorkBubble");
        if (bubble != null && oldScale > 0f)
        {
            bubble.localScale = Vector3.one;
            bubble.localPosition *= oldScale;
        }
        var controllerSo = new SerializedObject(builder);
        controllerSo.FindProperty("_mirrorLeftWalk").boolValue = true;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        return EditorSceneManager.SaveScene(scene);
    }

    // 목록에서 빠진 모습(이전에 만든 것)의 프리팹 · 애니메이션 · 그림을 지움
    private static void RemoveStaleLooks()
    {
        foreach (var guid in AssetDatabase.FindAssets("PlazaOtter_Cast_ t:Prefab", new[] { PrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string look = Path.GetFileNameWithoutExtension(path).Substring("PlazaOtter_Cast_".Length);
            if (Looks.Contains(look))
                continue;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset($"{AnimDir}/{look}");
            AssetDatabase.DeleteAsset($"{SheetDir}/Cast_{look}.png");
            AssetDatabase.DeleteAsset($"{IconDir}/ICON_Otter_Cast_{look}.png");
            Debug.Log($"[OtterCastSetup] 목록에서 빠진 모습을 지웠습니다: {look}");
        }
    }
}
