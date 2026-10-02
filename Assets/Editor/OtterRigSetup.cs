using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

// Experimental 2D Animation (bone + SpriteSkin) rig for the farmer otter,
// built from the separated parts sheet (ArtSource/Otter/FarmerOtter_RigPartsSheet.png:
// front / side / back rows, left = mirrored side), repacked into
// FarmerOtter_RigParts.png by Tools/SpriteRepack/repack_otter_rig_parts.py. Does everything the Skinning Editor
// would normally be used for by hand:
//   1. slices the parts texture with per-part pivots (= joint positions),
//   2. writes sprite bones + a grid mesh + bone weights into the importer
//      (rigid parts get one bone, the tail a 3-bone chain so it bends),
//   3. builds a 4-view rig (Down/Right/Left/Up — the sheet draws each facing
//      separately, so each view has its own skeleton) with SpriteSkin per part,
//   4. generates Idle/Walk clips per view plus an AnimatorController,
//   5. creates Assets/Scenes/RiggingTestScene.unity with a keyboard test driver.
// Bones/weights stay editable afterwards in the Sprite Editor > Skinning Editor.
// Everything rig-specific lives in a RigConfig: config A (this file) is the 4-view
// rig, config B (OtterRigSetup.ArmSwap.cs) a front/back rig with arm-pose swaps.
// Run via: OtterKingdom > Tools > Setup Otter Rig Test
public static partial class OtterRigSetup
{
    private const string PrefabDir = "Assets/Prefabs/RigTest";
    // Grid mesh resolution in texture pixels. Fine enough for the tail to bend smoothly.
    private const int MeshCellPx = 16;
    private const int TailBoneCount = 3;

    // Must match OtterRigTestController.
    private const string DirParam = "Dir";
    private const string MovingParam = "Moving";
    private const int DirDown = 0, DirRight = 1, DirLeft = 2, DirUp = 3;

    private sealed class RigConfig
    {
        public string MenuTitle, LogTag, RootName, ClipPrefix;
        public string TexturePath, AnimDir, ControllerPath, PrefabPath, ScenePath;
        public float PixelsPerUnit;
        public Dictionary<string, RectInt> PartRects;
        // Normalized pivot = where the part hinges on its parent bone.
        public System.Func<string, Vector2> PivotOf;
        // Direction the tail bone chain runs in (degrees), from the pivot toward the tip.
        public System.Func<string, float> TailAngle;
        // Layout origin in source pixels: character centre line and feet line.
        public float LayoutCenterX, LayoutFeetY;
        // First view is the default. dirs = OtterRigTestController directions it shows.
        public (string name, int[] dirs, Part[] parts)[] Views;
        // Front/back arm swing while walking, in degrees.
        public float FrontArmSwing = 14f;
        // Extra per-clip curves (e.g. sprite swaps): clip, view, walking, period.
        public System.Action<AnimationClip, ViewRig, bool, float> ExtraCurves;
    }

    // The config being built; set by Build().
    private static RigConfig C;
    private static Dictionary<string, Sprite> s_Sprites;

    // --- Config A: front / side / back, left = mirrored side ----------------------

    // Rects inside FarmerOtter_RigParts.png (Unity bottom-left origin), printed by
    // Tools/SpriteRepack/repack_otter_rig_parts.py — re-paste whenever the PNG is regenerated.
    private static readonly Dictionary<string, RectInt> RectsA = new Dictionary<string, RectInt>
    {
        { "Head_Front", new RectInt(4, 507, 316, 217) },
        { "Head_Right", new RectInt(324, 499, 276, 225) },
        { "Head_Left", new RectInt(604, 499, 276, 225) },
        { "Head_Back", new RectInt(4, 264, 312, 231) },
        { "Body_Front", new RectInt(320, 327, 172, 168) },
        { "Body_Right", new RectInt(496, 330, 166, 165) },
        { "Body_Left", new RectInt(666, 330, 166, 165) },
        { "Body_Back", new RectInt(836, 312, 181, 183) },
        { "Arm_Front_L", new RectInt(4, 142, 78, 118) },
        { "Arm_Front_R", new RectInt(86, 142, 81, 118) },
        { "Arm_Back_L", new RectInt(171, 135, 85, 125) },
        { "Arm_Back_R", new RectInt(260, 136, 84, 124) },
        { "Arm_SideNear_R", new RectInt(348, 137, 84, 123) },
        { "Arm_SideFar_R", new RectInt(436, 138, 84, 122) },
        { "Arm_SideNear_L", new RectInt(524, 137, 84, 123) },
        { "Arm_SideFar_L", new RectInt(612, 138, 84, 122) },
        { "Leg_Front_L", new RectInt(700, 135, 84, 125) },
        { "Leg_Front_R", new RectInt(788, 134, 85, 126) },
        { "Leg_Back_L", new RectInt(877, 135, 83, 125) },
        { "Leg_Back_R", new RectInt(4, 4, 85, 126) },
        { "Leg_SideNear_R", new RectInt(93, 4, 92, 126) },
        { "Leg_SideFar_R", new RectInt(189, 4, 94, 126) },
        { "Leg_SideNear_L", new RectInt(287, 4, 92, 126) },
        { "Leg_SideFar_L", new RectInt(383, 4, 94, 126) },
        { "Tail_Front", new RectInt(481, 48, 85, 82) },
        { "Tail_Right", new RectInt(570, 45, 151, 85) },
        { "Tail_Left", new RectInt(725, 45, 151, 85) },
        { "Tail_Back", new RectInt(880, 31, 69, 99) },
    };

    private static Vector2 PivotOfA(string sprite)
    {
        // Head_* includes the hat (merged by the repack script); 0.08 is the neck.
        if (sprite.StartsWith("Head_")) return new Vector2(0.5f, 0.08f);
        if (sprite.StartsWith("Body_")) return new Vector2(0.5f, 0.4f);
        if (sprite.StartsWith("Arm_") || sprite.StartsWith("Leg_")) return new Vector2(0.5f, 0.85f);
        switch (sprite)
        {
            case "Tail_Front": return new Vector2(0.2f, 0.25f); // peeks out behind the body, lower right
            case "Tail_Right": return new Vector2(0.92f, 0.5f); // root on the right, tail trails left
            case "Tail_Left": return new Vector2(0.08f, 0.5f);
            case "Tail_Back": return new Vector2(0.5f, 0.85f);
        }
        return new Vector2(0.5f, 0.5f);
    }

    private static float TailAngleA(string sprite)
    {
        switch (sprite)
        {
            case "Tail_Front": return 50f;
            case "Tail_Right": return 180f;
            case "Tail_Left": return 0f;
            default: return -90f;
        }
    }

    private static bool IsTail(string sprite) => sprite.StartsWith("Tail_");

    // One part placed in a view, in "layout px" (x right, y down, see
    // RigConfig.LayoutCenterX/FeetY). By default posPx is the sprite centre;
    // AtPivot parts place their pivot there instead (arms whose pose sprites get
    // swapped keep the shoulder fixed that way).
    private struct Part
    {
        public string bone, sprite, parent;
        public Vector2 posPx;
        public int order;
        public bool atPivot;

        public Part(string bone, string sprite, string parent, float x, float y, int order)
        {
            this.bone = bone; this.sprite = sprite; this.parent = parent;
            posPx = new Vector2(x, y); this.order = order; atPivot = false;
        }

        public static Part AtPivot(string bone, string sprite, string parent, float x, float y, int order) =>
            new Part(bone, sprite, parent, x, y, order) { atPivot = true };
    }

    // Layout in source pixels: centre line x=190, feet y=410. Every view's feet land
    // on the feet line so the otter doesn't hop when turning. Left = Right mirrored around LayoutCenterX.
    // Order = sorting order. Parents before children.
    // Legs always sort behind the body: their tops hide inside the hips, so the leg
    // outlines never cross the overalls (a near leg drawn over the side body split
    // the trousers into several green pieces).
    private static readonly (string name, int[] dirs, Part[] parts)[] ViewsA =
    {
        ("Down", new[] { DirDown }, new[]
        {
            new Part("Hips", "Body_Front", null, 190, 278, 20),
            new Part("Head", "Head_Front", "Hips", 190, 112, 40),
            new Part("Arm_L", "Arm_Front_L", "Hips", 104, 265, 30),
            new Part("Arm_R", "Arm_Front_R", "Hips", 276, 265, 30),
            new Part("Leg_L", "Leg_Front_L", "Hips", 158, 352, 10),
            new Part("Leg_R", "Leg_Front_R", "Hips", 222, 352, 10),
            new Part("Tail", "Tail_Front", "Hips", 252, 350, 0),
        }),
        ("Right", new[] { DirRight }, new[]
        {
            new Part("Hips", "Body_Right", null, 182, 270, 20),
            new Part("Head", "Head_Right", "Hips", 200, 112, 50),
            new Part("Arm_Far", "Arm_SideFar_R", "Hips", 222, 262, 5),
            new Part("Arm_Near", "Arm_SideNear_R", "Hips", 170, 268, 40),
            new Part("Leg_Far", "Leg_SideFar_R", "Hips", 212, 352, 10),
            new Part("Leg_Near", "Leg_SideNear_R", "Hips", 168, 352, 15),
            new Part("Tail", "Tail_Right", "Hips", 100, 330, 0),
        }),
        ("Left", new[] { DirLeft }, new[]
        {
            new Part("Hips", "Body_Left", null, 198, 270, 20),
            new Part("Head", "Head_Left", "Hips", 180, 112, 50),
            new Part("Arm_Far", "Arm_SideFar_L", "Hips", 158, 262, 5),
            new Part("Arm_Near", "Arm_SideNear_L", "Hips", 210, 268, 40),
            new Part("Leg_Far", "Leg_SideFar_L", "Hips", 168, 352, 10),
            new Part("Leg_Near", "Leg_SideNear_L", "Hips", 212, 352, 15),
            new Part("Tail", "Tail_Left", "Hips", 280, 330, 0),
        }),
        ("Up", new[] { DirUp }, new[]
        {
            new Part("Hips", "Body_Back", null, 190, 278, 20),
            new Part("Head", "Head_Back", "Hips", 190, 118, 50),
            new Part("Arm_L", "Arm_Back_L", "Hips", 104, 265, 10),
            new Part("Arm_R", "Arm_Back_R", "Hips", 276, 265, 10),
            new Part("Leg_L", "Leg_Back_L", "Hips", 158, 352, 0),
            new Part("Leg_R", "Leg_Back_R", "Hips", 222, 352, 0),
            new Part("Tail", "Tail_Back", "Hips", 190, 350, 40),
        }),
    };

    private static RigConfig ConfigA() => new RigConfig
    {
        MenuTitle = "Setup Otter Rig Test",
        LogTag = "[OtterRigSetup]",
        RootName = "FarmerOtterRig",
        ClipPrefix = "OtterRig_",
        TexturePath = "Assets/Art/Otter/Rig/FarmerOtter_RigParts.png",
        AnimDir = "Assets/Animations/OtterRig",
        ControllerPath = "Assets/Animations/OtterRig/FarmerOtterRig.controller",
        PrefabPath = PrefabDir + "/FarmerOtterRig.prefab",
        ScenePath = "Assets/Scenes/RiggingTestScene.unity",
        // Parts are kept at source resolution (~406px tall) -> ~1.75 units, the same
        // on-screen size as the frame-animated FarmerOtter.
        PixelsPerUnit = 230f,
        PartRects = RectsA,
        PivotOf = PivotOfA,
        TailAngle = TailAngleA,
        LayoutCenterX = 190f,
        LayoutFeetY = 410f,
        Views = ViewsA,
    };

    [MenuItem("OtterKingdom/Tools/Setup Otter Rig Test")]
    public static void Run() => Build(ConfigA());

    private static void Build(RigConfig config)
    {
        C = config;
        if (Application.isPlaying)
        {
            Debug.LogError($"{C.LogTag} Stop Play mode first.");
            return;
        }
        if (File.Exists(C.ScenePath) &&
            !EditorUtility.DisplayDialog(C.MenuTitle,
                $"{C.ScenePath} 이(가) 이미 있습니다. 리그/애니메이션/씬을 다시 만들까요?",
                "다시 만들기", "취소"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        s_Sprites = ImportAndSkinParts();
        if (s_Sprites == null) return;

        EnsureFolder(C.AnimDir);
        EnsureFolder(PrefabDir);

        var root = BuildRig(s_Sprites, out var viewRigs);
        var controller = BuildAnimations(viewRigs);
        root.GetComponent<Animator>().runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(root, C.PrefabPath);
        Object.DestroyImmediate(root);

        BuildScene(AssetDatabase.LoadAssetAtPath<GameObject>(C.PrefabPath));
        AssetDatabase.SaveAssets();
        Debug.Log($"{C.LogTag} Done. Open {Path.GetFileNameWithoutExtension(C.ScenePath)} and press Play (WASD / arrows to walk).");
    }

    // --- 1+2. Texture: slicing, bones, mesh, weights -------------------------

    private static Dictionary<string, Sprite> ImportAndSkinParts()
    {
        AssetDatabase.ImportAsset(C.TexturePath);
        var importer = AssetImporter.GetAtPath(C.TexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"{C.LogTag} Parts texture not found at {C.TexturePath}.");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = C.PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
        dp.InitSpriteEditorDataProvider();

        // Keep sprite IDs across re-runs so prefab/scene references survive.
        var existingIds = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);

        var rects = new List<SpriteRect>();
        foreach (var kv in C.PartRects)
        {
            var r = kv.Value;
            rects.Add(new SpriteRect
            {
                name = kv.Key,
                rect = new Rect(r.x, r.y, r.width, r.height),
                alignment = SpriteAlignment.Custom,
                pivot = C.PivotOf(kv.Key),
                spriteID = existingIds.TryGetValue(kv.Key, out var id) ? id : GUID.Generate(),
            });
        }
        dp.SetSpriteRects(rects.ToArray());
        dp.GetDataProvider<ISpriteNameFileIdDataProvider>()?
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

        var boneProvider = dp.GetDataProvider<ISpriteBoneDataProvider>();
        var meshProvider = dp.GetDataProvider<ISpriteMeshDataProvider>();
        foreach (var r in rects)
        {
            var size = new Vector2(r.rect.width, r.rect.height);
            Vector2 pivotPx = Vector2.Scale(r.pivot, size);
            bool tail = IsTail(r.name);
            int boneCount = tail ? TailBoneCount : 1;
            // Rigid parts keep an unrotated bone so their local axes match world axes in clips.
            float angle = tail ? C.TailAngle(r.name) : 0f;
            float segLenPx = tail ? TailLengthPx(r.pivot, size, angle) / TailBoneCount : size.y * 0.3f;

            boneProvider.SetBones(r.spriteID, BuildSpriteBones(r.name, pivotPx, angle, segLenPx, boneCount));
            BuildGridMesh(size, pivotPx, angle, segLenPx, boneCount,
                out var vertices, out var indices, out var edges);
            meshProvider.SetVertices(r.spriteID, vertices);
            meshProvider.SetIndices(r.spriteID, indices);
            meshProvider.SetEdges(r.spriteID, edges);
        }

        dp.Apply();
        importer.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(C.TexturePath).OfType<Sprite>()
            .ToDictionary(s => s.name, s => s);
        foreach (var name in C.PartRects.Keys)
        {
            if (!sprites.ContainsKey(name))
            {
                Debug.LogError($"{C.LogTag} Sprite '{name}' missing after import.");
                return null;
            }
        }
        return sprites;
    }

    // Distance from the pivot to the far edge of the rect along the chain direction.
    private static float TailLengthPx(Vector2 pivot, Vector2 size, float angle)
    {
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float tx = dir.x > 0.01f ? (1f - pivot.x) * size.x / dir.x : dir.x < -0.01f ? pivot.x * size.x / -dir.x : float.MaxValue;
        float ty = dir.y > 0.01f ? (1f - pivot.y) * size.y / dir.y : dir.y < -0.01f ? pivot.y * size.y / -dir.y : float.MaxValue;
        return Mathf.Min(tx, ty) * 0.9f;
    }

    // Sprite bones live in sprite-rect pixel space: the root bone's position is
    // relative to the rect's bottom-left corner, child bones are in parent space.
    private static List<SpriteBone> BuildSpriteBones(string spriteName, Vector2 pivotPx, float angle,
        float segLenPx, int count)
    {
        string baseName = spriteName.Split('_')[0];
        var bones = new List<SpriteBone>();
        for (int i = 0; i < count; i++)
        {
            bones.Add(new SpriteBone
            {
                name = i == 0 ? baseName : $"{baseName}_{i + 1}",
                guid = GUID.Generate().ToString(),
                position = i == 0 ? new Vector3(pivotPx.x, pivotPx.y, 0f) : new Vector3(segLenPx, 0f, 0f),
                rotation = i == 0 ? Quaternion.Euler(0f, 0f, angle) : Quaternion.identity,
                length = segLenPx,
                parentId = i - 1,
                color = Color.white,
            });
        }
        return bones;
    }

    private static void BuildGridMesh(Vector2 size, Vector2 pivotPx, float angle, float segLenPx, int boneCount,
        out Vertex2DMetaData[] vertices, out int[] indices, out Vector2Int[] edges)
    {
        int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / MeshCellPx));
        int ny = Mathf.Max(1, Mathf.CeilToInt(size.y / MeshCellPx));
        var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        vertices = new Vertex2DMetaData[(nx + 1) * (ny + 1)];
        for (int y = 0; y <= ny; y++)
        {
            for (int x = 0; x <= nx; x++)
            {
                var p = new Vector2(size.x * x / nx, size.y * y / ny);
                vertices[y * (nx + 1) + x] = new Vertex2DMetaData
                {
                    position = p,
                    boneWeight = WeightAlongChain(Vector2.Dot(p - pivotPx, dir) / segLenPx, boneCount),
                };
            }
        }

        var tris = new List<int>();
        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < nx; x++)
            {
                int i0 = y * (nx + 1) + x, i1 = i0 + 1, i2 = i0 + nx + 1, i3 = i2 + 1;
                tris.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        }
        indices = tris.ToArray();

        // Outline edges, counter-clockwise around the rect.
        var loop = new List<int>();
        for (int x = 0; x <= nx; x++) loop.Add(x);
        for (int y = 1; y <= ny; y++) loop.Add(y * (nx + 1) + nx);
        for (int x = nx - 1; x >= 0; x--) loop.Add(ny * (nx + 1) + x);
        for (int y = ny - 1; y >= 1; y--) loop.Add(y * (nx + 1));
        edges = new Vector2Int[loop.Count];
        for (int i = 0; i < loop.Count; i++) edges[i] = new Vector2Int(loop[i], loop[(i + 1) % loop.Count]);
    }

    // t = distance along the chain in segment lengths. Each bone owns its
    // segment's middle; weights cross-fade linearly around the joints.
    private static BoneWeight WeightAlongChain(float t, int boneCount)
    {
        if (boneCount == 1) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        float f = Mathf.Clamp(t - 0.5f, 0f, boneCount - 1);
        int a = Mathf.Min(Mathf.FloorToInt(f), boneCount - 2);
        float w = f - a;
        if (w <= 0.001f) return new BoneWeight { boneIndex0 = a, weight0 = 1f };
        if (w >= 0.999f) return new BoneWeight { boneIndex0 = a + 1, weight0 = 1f };
        return new BoneWeight { boneIndex0 = a, weight0 = 1f - w, boneIndex1 = a + 1, weight1 = w };
    }

    // --- 3. Rig hierarchy ------------------------------------------------------

    private sealed class ViewRig
    {
        public string name;
        public GameObject go;
        public Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    }

    private static GameObject BuildRig(Dictionary<string, Sprite> sprites, out List<ViewRig> viewRigs)
    {
        var root = new GameObject(C.RootName);
        root.AddComponent<SortingGroup>();
        root.AddComponent<Animator>();
        viewRigs = new List<ViewRig>();

        foreach (var (viewName, _, parts) in C.Views)
        {
            var rig = new ViewRig { name = viewName, go = new GameObject("View_" + viewName) };
            rig.go.transform.SetParent(root.transform, false);
            var bonesRoot = new GameObject("Bones").transform;
            bonesRoot.SetParent(rig.go.transform, false);
            var spritesRoot = new GameObject("Sprites").transform;
            spritesRoot.SetParent(rig.go.transform, false);

            foreach (var part in parts)
            {
                Sprite sprite = sprites[part.sprite];
                Vector3 pivotWorld = PivotWorld(part);

                Transform parent = part.parent == null ? bonesRoot : rig.bones[part.parent];
                var bone = new GameObject(part.bone).transform;
                bone.SetParent(parent, false);
                bone.position = pivotWorld;

                var chain = new List<Transform> { bone };
                if (IsTail(part.sprite))
                {
                    var r = C.PartRects[part.sprite];
                    float angle = C.TailAngle(part.sprite);
                    float segLen = TailLengthPx(C.PivotOf(part.sprite), new Vector2(r.width, r.height), angle)
                                   / TailBoneCount / C.PixelsPerUnit;
                    bone.rotation = Quaternion.Euler(0f, 0f, angle);
                    for (int i = 1; i < TailBoneCount; i++)
                    {
                        var seg = new GameObject($"{part.bone}_{i + 1}").transform;
                        seg.SetParent(chain[i - 1], false);
                        seg.localPosition = new Vector3(segLen, 0f, 0f);
                        chain.Add(seg);
                        rig.bones[seg.name] = seg;
                    }
                }
                rig.bones[part.bone] = bone;

                var spriteGo = new GameObject(part.bone + "_Sprite");
                spriteGo.transform.SetParent(spritesRoot, false);
                spriteGo.transform.position = pivotWorld;
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = part.order;
                var skin = spriteGo.AddComponent<SpriteSkin>();
                skin.SetRootBone(bone);
                var state = skin.SetBoneTransforms(chain.ToArray());
                if (state != SpriteSkinState.Ready)
                    Debug.LogWarning($"{C.LogTag} {viewName}/{part.bone} SpriteSkin state: {state}");
            }

            rig.go.SetActive(viewRigs.Count == 0);
            viewRigs.Add(rig);
        }
        return root;
    }

    private static Vector3 PivotWorld(Part part)
    {
        float ppu = C.PixelsPerUnit;
        float x = (part.posPx.x - C.LayoutCenterX) / ppu;
        float y = (C.LayoutFeetY - part.posPx.y) / ppu;
        if (part.atPivot) return new Vector3(x, y, 0f);

        var r = C.PartRects[part.sprite];
        Vector2 pivot = C.PivotOf(part.sprite);
        return new Vector3(x + (pivot.x - 0.5f) * r.width / ppu,
                           y + (pivot.y - 0.5f) * r.height / ppu, 0f);
    }

    // --- 4. Clips + controller -------------------------------------------------

    private const float WalkPeriod = 0.6f;
    private const float IdlePeriod = 2f;
    private const int SamplesPerClip = 16;

    private static AnimatorController BuildAnimations(List<ViewRig> viewRigs)
    {
        AssetDatabase.DeleteAsset(C.ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(C.ControllerPath);
        controller.AddParameter(DirParam, AnimatorControllerParameterType.Int);
        controller.AddParameter(MovingParam, AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;

        for (int v = 0; v < viewRigs.Count; v++)
        {
            var rig = viewRigs[v];
            foreach (bool walking in new[] { false, true })
            {
                string clipName = $"{C.ClipPrefix}{(walking ? "Walk" : "Idle")}_{rig.name}";
                var clip = BuildClip(clipName, rig, viewRigs, walking);
                var state = sm.AddState(clipName);
                state.motion = clip;
                if (v == 0 && !walking) sm.defaultState = state;

                // A view can stand in for several directions (config B shows the
                // front view while walking sideways).
                foreach (int dir in C.Views[v].dirs)
                {
                    var t = sm.AddAnyStateTransition(state);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    t.canTransitionToSelf = false;
                    t.AddCondition(AnimatorConditionMode.Equals, dir, DirParam);
                    t.AddCondition(walking ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, MovingParam);
                }
            }
        }
        return controller;
    }

    private static AnimationClip BuildClip(string name, ViewRig rig, List<ViewRig> allRigs, bool walk)
    {
        string path = $"{C.AnimDir}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { name = name, frameRate = 30f };
        float period = walk ? WalkPeriod : IdlePeriod;

        // Each clip shows its own view and hides the others, so the Animator alone picks the facing.
        foreach (var other in allRigs)
        {
            float v = other == rig ? 1f : 0f;
            clip.SetCurve("View_" + other.name, typeof(GameObject), "m_IsActive",
                new AnimationCurve(new Keyframe(0f, v), new Keyframe(period, v)));
        }

        bool side = rig.name == "Right" || rig.name == "Left";
        // Mirror rotation direction on the left view so both sides swing "forward" alike.
        float mirror = rig.name == "Left" ? -1f : 1f;
        const float Pi = Mathf.PI;

        if (walk)
        {
            Wave(clip, rig, "Head", Prop.Rot, 1.5f * mirror, period, Pi / 2f, 2);
            if (side)
            {
                // Legs swing front/back around the hip. Positive rotation swings the
                // foot toward the facing side (mirror flips it for Left), so a leg is
                // moving forward while cos > 0 -- it lifts only then, peaking as it
                // passes under the body.
                Wave(clip, rig, "Leg_Near", Prop.Rot, 28f * mirror, period, 0f);
                Wave(clip, rig, "Leg_Far", Prop.Rot, 28f * mirror, period, Pi);
                Wave(clip, rig, "Leg_Near", Prop.PosY, 0.05f, period, Pi / 2f, liftOnly: true);
                Wave(clip, rig, "Leg_Far", Prop.PosY, 0.05f, period, -Pi / 2f, liftOnly: true);
                // Each arm swings with the opposite leg.
                Wave(clip, rig, "Arm_Near", Prop.Rot, 30f * mirror, period, Pi);
                Wave(clip, rig, "Arm_Far", Prop.Rot, 30f * mirror, period, 0f);
                // Lowest when the legs are spread (contact), highest as they pass.
                Wave(clip, rig, "Hips", Prop.PosY, 0.02f, period, Pi / 2f, 2, absolute: true);
                TailWave(clip, rig, 10f, period, 1);
            }
            else
            {
                // Front/back: rotating the legs barely reads, so the feet step instead --
                // one foot up while the other is planted, hips leaning onto the planted one.
                Wave(clip, rig, "Leg_L", Prop.PosY, 0.09f, period, 0f, liftOnly: true);
                Wave(clip, rig, "Leg_R", Prop.PosY, 0.09f, period, Pi, liftOnly: true);
                Wave(clip, rig, "Hips", Prop.Rot, 2.5f, period, 0f);
                Wave(clip, rig, "Hips", Prop.PosY, 0.02f, period, 0f, 2, absolute: true);
                Wave(clip, rig, "Arm_L", Prop.Rot, C.FrontArmSwing, period, 0f);
                Wave(clip, rig, "Arm_R", Prop.Rot, C.FrontArmSwing, period, Pi);
                if (rig.bones.ContainsKey("Tail")) TailWave(clip, rig, 8f, period, 1);
            }
        }
        else
        {
            Wave(clip, rig, "Hips", Prop.PosY, 0.012f, period, 0f);
            Wave(clip, rig, "Head", Prop.Rot, 2f * mirror, period, Pi / 2f);
            foreach (var arm in new[] { "Arm_L", "Arm_R", "Arm_Near", "Arm_Far" })
                if (rig.bones.ContainsKey(arm)) Wave(clip, rig, arm, Prop.Rot, 3f, period, arm.EndsWith("R") ? Pi : 0f);
            if (rig.bones.ContainsKey("Tail")) TailWave(clip, rig, 6f, period, 1);
        }
        C.ExtraCurves?.Invoke(clip, rig, walk, period);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private enum Prop { Rot, PosY }

    // Sine wave around the bone's rest value. harmonic = cycles per clip.
    // absolute: |sin| (always up, e.g. step bob). liftOnly: only the positive half (foot lift).
    private static void Wave(AnimationClip clip, ViewRig rig, string bone, Prop prop, float amp, float period,
        float phase, int harmonic = 1, bool absolute = false, bool liftOnly = false)
    {
        if (!rig.bones.TryGetValue(bone, out var t)) return;
        string path = AnimationUtility.CalculateTransformPath(t, rig.go.transform.parent);

        var keys = new Keyframe[SamplesPerClip + 1];
        for (int i = 0; i <= SamplesPerClip; i++)
        {
            float time = period * i / SamplesPerClip;
            float x = (float)i / SamplesPerClip;
            float s = absolute
                ? Mathf.Abs(Mathf.Sin(Mathf.PI * harmonic * x + phase))
                : Mathf.Sin(2f * Mathf.PI * harmonic * x + phase);
            if (liftOnly) s = Mathf.Max(0f, s);
            keys[i] = new Keyframe(time, amp * s);
        }

        if (prop == Prop.Rot)
        {
            Vector3 e = t.localEulerAngles;
            SetCurves(clip, path, "localEulerAnglesRaw", new Vector3(e.x, e.y, e.z), keys, 2);
        }
        else
        {
            SetCurves(clip, path, "m_LocalPosition", t.localPosition, keys, 1);
        }
    }

    private static void SetCurves(AnimationClip clip, string path, string prop, Vector3 rest, Keyframe[] offsets, int axis)
    {
        string[] comps = { "x", "y", "z" };
        for (int c = 0; c < 3; c++)
        {
            AnimationCurve curve;
            if (c == axis)
            {
                curve = new AnimationCurve(offsets.Select(k => new Keyframe(k.time, rest[c] + k.value)).ToArray());
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            }
            else
            {
                curve = new AnimationCurve(new Keyframe(0f, rest[c]), new Keyframe(offsets[^1].time, rest[c]));
            }
            clip.SetCurve(path, typeof(Transform), $"{prop}.{comps[c]}", curve);
        }
    }

    // Each tail segment swings a bit later and wider than the one before -> whip.
    private static void TailWave(AnimationClip clip, ViewRig rig, float amp, float period, int harmonic)
    {
        Wave(clip, rig, "Tail", Prop.Rot, amp * 0.6f, period, 0f, harmonic);
        for (int i = 2; i <= TailBoneCount; i++)
            Wave(clip, rig, $"Tail_{i}", Prop.Rot, amp * (0.6f + 0.3f * i), period, -0.6f * (i - 1), harmonic);
    }

    // --- 5. Scene ----------------------------------------------------------------

    private static void BuildScene(GameObject prefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 2.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.55f);
        camGo.transform.position = new Vector3(0f, 1f, -10f);

        var otter = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        otter.transform.position = Vector3.zero;
        otter.AddComponent<OtterRigTestController>();

        EditorSceneManager.SaveScene(scene, C.ScenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
