using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 농부 해달을 바탕으로 색 · 소품만 바꾼 해달 20마리(Tools/OtterCast/farmer_cast.py가 만든 그림)를 게임에 넣는다.
/// 해달마다 묶음 시트(Assets/Sprites/Characters/Cast/Cast_&lt;id&gt;.png, 6열 × 312×180 칸, 0.75배)를 잘라
/// 농부 해달 애니메이션(FarmerOtter.controller)의 클립을 같은 박자로 그림만 바꿔 덮어쓴 컨트롤러와 광장 프리팹
/// (PlazaOtter_Cast_&lt;id&gt;)을 만들고, Assignments의 해달에게 광장 모습 · 얼굴 · 도감 얼굴을 연결한다.
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
        "Lilac", "Pumpkin", "Ocean", "Berry", "Forest", "Sunny", "Ink", "Peach", "Sky", "Clover",
    };

    /// <summary>해달 → 모습. 다른 해달과 모습을 나눠 쓰던 장난감 해달 11마리 + 포근이(농부와 같은 모습이었음)</summary>
    private static readonly (string otterId, string look)[] Assignments =
    {
        ("otter_toy_kungkung", "Forest"),   // 나무꾼
        ("otter_toy_degul", "Ink"),         // 조약돌
        ("otter_toy_bodeul", "Sprout"),     // 풀잎 · 농사
        ("otter_toy_pongdang", "Ocean"),    // 물방울 · 낚시
        ("otter_toy_yeongcha", "Pumpkin"),  // 힘센 운반
        ("otter_toy_jjalrang", "Honey"),    // 동전 · 장사 (금색 방울)
        ("otter_toy_jaejal", "Cherry"),     // 수다쟁이
        ("otter_toy_kongkong", "Cocoa"),    // 망치 소리 · 건설
        ("otter_toy_kkomkkom", "Lilac"),    // 손재주
        ("otter_toy_duribeon", "Explorer"), // 길찾기
        ("otter_toy_kkeujeok", "Night"),    // 밤의 메모쟁이
        ("otter_p3_neighbor", "Snow"),      // 포근이 (목도리)
    };

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
        otterSo.FindProperty("_plazaPrefab").objectReferenceValue = prefab;
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

        int made = 0;
        foreach (var look in Looks)
        {
            string sheetPath = $"{SheetDir}/Cast_{look}.png";
            if (!File.Exists(sheetPath))
            {
                Debug.LogWarning($"[OtterCastSetup] 그림이 없습니다: {sheetPath} (python Tools/OtterCast/farmer_cast.py)");
                continue;
            }
            var frames = Slice(sheetPath, look);
            var controller = BuildController(look, baseController, frames);
            BuildPrefab(look, controller, frames["Harvest"][0]);
            CollectionSetup.ImportSprite(IconDir, $"ICON_Otter_Cast_{look}");
            made++;
        }
        AssetDatabase.SaveAssets();

        int linked = Link();
        AssetDatabase.SaveAssets();
        Debug.Log($"[OtterCastSetup] 모습 {made}가지를 만들고 해달 {linked}마리에 연결했습니다.");
    }

    // 클립 → 8칸 (칸 순서는 farmer_cast.py의 PACK_CLIPS)
    private static Dictionary<string, Sprite[]> Slice(string path, string look)
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
            }
        }
#pragma warning disable CS0618 // spritesheet는 쓰지 말라고 하지만 여전히 가장 간단한 방법 (FarmerOtterSpriteSetup과 같음)
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        return Clips.ToDictionary(clip => clip,
            clip => Enumerable.Range(0, FramesPerClip).Select(f => all[$"Cast_{look}_{clip}_{f}"]).ToArray());
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

            var entry = so.FindProperty("_collectionEntry").objectReferenceValue;
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
}
