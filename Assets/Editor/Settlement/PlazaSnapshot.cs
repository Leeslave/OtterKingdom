using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 광장 씬 전체를 한 장의 그림으로 찍는다 (배치 확인용, 씬은 저장하지 않음).
/// 발전 조건(DevelopmentGate)을 주어진 발전 목록으로 맞추고, 걷기 영역(초록)·막힌 발자국(빨강)을 선으로 겹쳐 그린다.
/// 배치 모드: -executeMethod PlazaSnapshot.CaptureFromCommandLine -snapshotOut 경로.png -snapshotDevs a,b,c [-snapshotAll]
/// </summary>
public static class PlazaSnapshot
{
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";
    private const float PixelsPerUnit = 40f;

    [MenuItem("Tools/Settlement/Plaza Snapshot (all unlocked)")]
    public static void CaptureAllUnlocked()
    {
        Capture(Path.Combine(Application.dataPath, "../Temp/plaza_snapshot.png"), null, true);
    }

    public static void CaptureFromCommandLine()
    {
        string output = Arg("-snapshotOut") ?? Path.Combine(Application.dataPath, "../Temp/plaza_snapshot.png");
        string devs = Arg("-snapshotDevs");
        bool all = Array.IndexOf(Environment.GetCommandLineArgs(), "-snapshotAll") >= 0;
        var set = new HashSet<string>(string.IsNullOrEmpty(devs) ? Array.Empty<string>() : devs.Split(','));
        Capture(output, set, all);
    }

    /// <summary>바닥 픽셀 x가 minPixelX보다 큰 렌더러의 이름·위치를 로그로 (배치 확인용)</summary>
    public static void DumpEast()
    {
        EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var background = GameObject.Find("PlazaRoot").transform.Find("Background").GetComponent<SpriteRenderer>();
        var b = SettlementSetup.GroundBounds(background);
        var lines = new List<string>();
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var p = r.transform.position;
            float px = (p.x - b.min.x) * PixelsPerUnit;
            float py = (b.max.y - p.y) * PixelsPerUnit;
            if (px < 700f)
                continue;
            lines.Add($"{HierarchyPath(r.transform)}  px=({px:0},{py:0}) size=({r.bounds.size.x * PixelsPerUnit:0},{r.bounds.size.y * PixelsPerUnit:0}) active={r.gameObject.activeInHierarchy}");
        }
        lines.Sort();
        File.WriteAllLines(Arg("-snapshotOut") ?? "east.txt", lines);
    }

    private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    public static void Capture(string outputPath, HashSet<string> developments, bool allUnlocked)
    {
        // 배치 모드에서 스프라이트 아틀라스가 아직 안 묶여 그림이 깨지므로 찍는 동안만 아틀라스를 끔 (끝나면 되돌림)
        var packer = EditorSettings.spritePackerMode;
        EditorSettings.spritePackerMode = SpritePackerMode.Disabled;
        try
        {
            CaptureScene(outputPath, developments, allUnlocked);
        }
        finally
        {
            EditorSettings.spritePackerMode = packer;
        }
    }

    private static void CaptureScene(string outputPath, HashSet<string> developments, bool allUnlocked)
    {
        EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        foreach (var gate in UnityEngine.Object.FindObjectsByType<DevelopmentGate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            gate.Apply(id => allUnlocked || string.IsNullOrEmpty(id) || developments != null && developments.Contains(id), false);

        var background = GameObject.Find("PlazaRoot").transform.Find("Background").GetComponent<SpriteRenderer>();
        var bounds = SettlementSetup.GroundBounds(background);
        // 영토 전체 지도까지. -snapshotRect x0,y0,x1,y1 (바닥 픽셀)이면 그 부분만, -snapshotScale로 배율 (기본 0.75)
        var worldMap = GameObject.Find("PlazaRoot").transform.Find("Settlement/Territory/WorldMap");
        if (worldMap != null)
            bounds.Encapsulate(worldMap.GetComponent<SpriteRenderer>().bounds);
        string rectArg = Arg("-snapshotRect");
        if (!string.IsNullOrEmpty(rectArg))
        {
            var ground = SettlementSetup.GroundBounds(background);
            var v = Array.ConvertAll(rectArg.Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            var a = new Vector3(ground.min.x + v[0] / PixelsPerUnit, ground.max.y - v[3] / PixelsPerUnit, 0f);
            var c = new Vector3(ground.min.x + v[2] / PixelsPerUnit, ground.max.y - v[1] / PixelsPerUnit, 0f);
            bounds = new Bounds((a + c) * 0.5f, c - a);
        }
        float scale = float.TryParse(Arg("-snapshotScale"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : 0.75f;
        // 확장 바닥 조각까지 (켜진 것만)
        var camera = UnityEngine.Object.FindAnyObjectByType<PlazaCameraController>();
        if (camera != null)
        {
            var so = new SerializedObject(camera);
            var extras = so.FindProperty("extraBoundsSources");
            for (int i = 0; i < extras.arraySize; i++)
            {
                if (extras.GetArrayElementAtIndex(i).objectReferenceValue is Renderer r && r.enabled && r.gameObject.activeInHierarchy)
                    bounds.Encapsulate(r.bounds);
            }
        }

        DrawPolygons();
        // 배치 모드에서는 2D 조명이 없어 조명 받는 스프라이트가 검게 나옴 → 찍을 때만 조명 없는 재질로 (씬은 저장하지 않음)
        var unlit = new Material(Shader.Find("Sprites/Default"));
        foreach (var sprite in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            sprite.sharedMaterial = unlit;

        int width = Mathf.RoundToInt(bounds.size.x * PixelsPerUnit * scale);
        int height = Mathf.RoundToInt(bounds.size.y * PixelsPerUnit * scale);
        // 씬의 광장 카메라를 씀 (URP 2D 렌더러·조명 설정이 그대로라 스프라이트가 제대로 그려짐)
        var cam = camera != null ? camera.GetComponent<Camera>() : Camera.main;
        if (cam == null)
            cam = new GameObject("SnapshotCamera").AddComponent<Camera>();
        if (camera != null)
            camera.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = bounds.size.y * 0.5f;
        cam.aspect = bounds.size.x / bounds.size.y;
        cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -50f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var texture = new RenderTexture(width, height, 24);
        cam.targetTexture = texture;
        cam.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        Debug.Log($"[PlazaSnapshot] {outputPath} ({width}x{height}), 바닥 범위 {bounds.min} ~ {bounds.max}");
    }

    // 걷기 영역(초록)·막힌 곳(빨강) 선 (켜진 다각형만)
    private static void DrawPolygons()
    {
        var shader = Shader.Find("Sprites/Default");
        var material = new Material(shader);
        foreach (var polygon in UnityEngine.Object.FindObjectsByType<PlazaAreaPolygon>(FindObjectsSortMode.None))
        {
            var points = new List<Vector2>();
            polygon.GetWorldPoints(points);
            if (points.Count < 3)
                continue;
            var line = new GameObject("Outline").AddComponent<LineRenderer>();
            line.material = material;
            line.loop = true;
            line.widthMultiplier = 0.08f;
            line.positionCount = points.Count;
            var color = polygon.Kind == PlazaAreaKind.Walkable ? new Color(0.1f, 0.9f, 0.2f) : new Color(0.95f, 0.15f, 0.15f);
            line.startColor = line.endColor = color;
            line.sortingOrder = 32700;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, -1f));
        }
    }
}
