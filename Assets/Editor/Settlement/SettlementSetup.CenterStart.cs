using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 구역·이사 2단계: 광장의 시작 묶음을 지도 가운데 공터로 옮긴다 (Docs/구역이사_작업기록.md).
/// 광장 배치 도구(Setup Plaza · P2 · P3)는 지금도 옛 남동쪽 광장의 바닥 픽셀로 만들고, 끝에서 이 단계가 묶음별로 옮긴다
/// (묶음마다 정해진 만큼 = 목표 − 기준의 옛 자리. 아직 옛 광장에 있는 것만 옮기므로 여러 번 해도, 도구 하나만 다시 돌려도 같음).
/// 건물 자리(ApplyPlotAnchors)보다 먼저.
/// - 게시판 묶음: 게시판 · 마을회관 · 길드 · 바위 · 흔드는 나무 · 나뭇가지 · 모임 자리 · 건설 해달 · 요정 · 해달 나오는 자리 · 처음 카메라를
///   한 덩어리로 (옛 광장에서의 거리 그대로 = 칸 (−38, +31), CenterStartMigration.StartShift)
/// - 집 · 의자 · 가로등 · 비축 상자 · 밭 가는 길: 공터 빈 곳에 따로 (건물은 건설 모드의 처음 자리, 플레이어가 옮김)
/// - 나무 · 덤불 · 바위 장식, 피크닉 · 울타리: 옛 광장(지금은 바닷가)에 그대로
/// </summary>
public static partial class SettlementSetup
{
    // 게시판 묶음: 옛 게시판 (400, 525) → 공터 가운데 조금 위. 차이 (−1520, −1240) px = 칸 (−38, +31)
    private static readonly Vector2 OldBoardPixel = new Vector2(400, 525);
    private static readonly Vector2 CenterBoardPixel = new Vector2(-1120, -715);

    // 옛 광장 (바닥 픽셀, 넉넉히): 이 안에 있는 것만 옮김 (공터와 겹치지 않음)
    private static readonly Rect OldPlazaPixels = Rect.MinMaxRect(-300, -400, 1400, 1800);

    // 게시판과 같이 움직이는 것 (PlazaRoot 기준 경로, "/"로 시작하면 씬 맨 위)
    private static readonly string[] CenterCoreGroup =
    {
        "Settlement/Board", "Settlement/Gather_branch_01", "Settlement/Gather_branch_02",
        "Settlement/Rock_rock_01", "Settlement/Rock_rock_02", "Settlement/Tree_tree_01", "Settlement/Tree_tree_02",
        "Settlement/BuilderOtter", "Settlement/GatherPoint", "Settlement/ArrivalPoint",
        "Settlement/P2/Site_BoardUpgrade", "Settlement/P2/BoardStation", "Settlement/P2/BoardLeaves", "Settlement/P2/TaskSite_BoardArea",
        "Settlement/P2/CommonSpace_Clutter", "Settlement/P2/CommonSpace_Seats", "Settlement/P2/TaskSite_CommonSpace",
        "Settlement/P2/GuildOffice", "Settlement/P2/TownHall", "Settlement/P2/Site_GuildOffice", "Settlement/P2/Site_TownHall",
        "Settlement/P2/DecorBlock", "Settlement/P2/RestCorner_Bench", "Settlement/P2/Site_RestCorner",
        "Settlement/P3/CommunityTable", "Settlement/P3/Site_CommunityTable", "Settlement/P3/Gathering",
        "Settlement/P3/HallDecor", "Settlement/P3/TaskSite_HallTidy",
        "SpawnPoints", "InitialCameraFocus", "/FairyNpc",
    };

    // 따로 옮기는 묶음: (기준, 기준의 옛 바닥 픽셀, 목표 바닥 픽셀, 같이 움직이는 것)
    private static readonly (string anchor, Vector2 old, Vector2 pixel, string[] followers)[] CenterSpotGroups =
    {
        ("Settlement/Site_House1", new Vector2(965, 400), new Vector2(-740, -810), new[] { "Props/House_Blue_01", "Settlement/NightLights/NightGlow_House_Blue_01" }),
        ("Settlement/Site_House2", new Vector2(905, 240), new Vector2(-1470, -820), new[] { "Props/House_Red_01", "Settlement/NightLights/NightGlow_House_Red_01" }),
        ("Settlement/Site_Chair", new Vector2(240, 320), new Vector2(-1250, -900), new[] { "Props/Bench_01" }),
        ("Settlement/Site_Lamp", new Vector2(990, 520), new Vector2(-650, -690), new[] { "Props/Lamp_01", "Settlement/NightLights/NightGlow_Lamp_01" }),
        ("Settlement/P3/Site_SupplyBox", new Vector2(905, 795), new Vector2(-1480, -300), new[] { "Settlement/P3/SupplyBox" }),
        // 밭 가는 길: 공터 서쪽 가장자리 (5단계에서 농업 구역으로)
        ("Settlement/Site_Farmland", new Vector2(545, 150), new Vector2(-1600, -560), new[] { "Settlement/FarmPathBlocker", "Settlement/FarmSign" }),
    };

    [MenuItem("Tools/Settlement/Apply Center Start")]
    public static void ApplyCenterStartMenu()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        ApplyBoardExpansion(plazaRoot);
        ApplyCenterStart(plazaRoot);
        ApplyPlotAnchors(plazaRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementSetup] 중앙 공터 시작 적용 완료");
    }

    /// <summary>게시판 묶음 · 집 · 밭 길 등을 공터로 (여러 번 해도 같음)</summary>
    private static void ApplyCenterStart(Transform plazaRoot)
    {
        var bounds = GroundBounds(plazaRoot.Find("Background").GetComponent<SpriteRenderer>());
        Vector2 ToPixel(Vector3 world) => new Vector2((world.x - bounds.min.x) * GroundPixelsPerUnit, (bounds.max.y - world.y) * GroundPixelsPerUnit);

        MoveGroup(plazaRoot, CenterBoardPixel - OldBoardPixel, CenterCoreGroup, ToPixel);
        foreach (var group in CenterSpotGroups)
        {
            var members = new List<string> { group.anchor };
            members.AddRange(group.followers);
            MoveGroup(plazaRoot, group.pixel - group.old, members, ToPixel);
        }

        // 카메라도 처음 자리로 (에디터에서 열었을 때)
        var focus = plazaRoot.Find("InitialCameraFocus");
        var camera = Object.FindAnyObjectByType<PlazaCameraController>();
        if (focus != null && camera != null)
        {
            var p = camera.transform.position;
            camera.transform.position = new Vector3(focus.position.x, focus.position.y, p.z);
        }
    }

    // 묶음에서 아직 옛 광장에 있는 것을 정해진 만큼(바닥 픽셀) 옮김. 앞뒤 정렬 · 공사 현장 깊이를 다시 맞춤
    private static void MoveGroup(Transform plazaRoot, Vector2 deltaPixels, IEnumerable<string> members, System.Func<Vector3, Vector2> toPixel)
    {
        var delta = new Vector3(deltaPixels.x / GroundPixelsPerUnit, -deltaPixels.y / GroundPixelsPerUnit, 0f);
        var moved = new HashSet<Transform>();
        foreach (var path in members)
        {
            var t = FindCenterPath(plazaRoot, path);
            if (t == null)
            {
                Debug.LogWarning($"[SettlementSetup] 중앙 공터로 같이 옮길 '{path}'이(가) 없습니다.");
                continue;
            }
            if (!moved.Add(t) || !OldPlazaPixels.Contains(toPixel(t.position)))
                continue;
            t.position += delta;
            foreach (var prop in t.GetComponentsInChildren<PlazaProp>(true))
            {
                prop.SetDepthOffset(prop.DepthOffset);
                var renderer = prop.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    EditorUtility.SetDirty(renderer);
            }
            foreach (var site in t.GetComponentsInChildren<ConstructionSiteView>(true))
                site.RefreshDepth();
            EditorUtility.SetDirty(t);
        }
    }

    private static Transform FindCenterPath(Transform plazaRoot, string path)
    {
        if (path.StartsWith("/"))
        {
            var top = GameObject.Find(path);
            return top != null ? top.transform : null;
        }
        return plazaRoot.Find(path);
    }

    /// <summary>옛 광장 픽셀의 소품이 공터로 옮겨졌을 때의 픽셀 (발전 조건을 붙일 때 옛 자리 · 옮긴 자리 둘 다 알아봄)</summary>
    private static Vector2? CenterStartPixel(string propKind, Vector2 oldPixel)
    {
        foreach (var group in CenterSpotGroups)
        {
            foreach (var follower in group.followers)
            {
                if (follower.StartsWith("Props/" + propKind + "_"))
                    return oldPixel + (group.pixel - group.old);
            }
        }
        return null;
    }
}
