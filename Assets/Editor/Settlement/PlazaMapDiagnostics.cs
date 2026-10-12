using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlazaMapDiagnostics
{
    public static void ApplyAndCapture()
    {
        PlazaWorldMapHD.Apply();
        Dump();
        var controller = Object.FindAnyObjectByType<PlazaCameraController>();
        var camera = controller.GetComponent<Camera>();
        controller.enabled = false;
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.sprite == null) continue;
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            material.mainTexture = r.sprite.texture;
            r.sharedMaterial = material;
        }
        camera.orthographic = true;
        camera.orthographicSize = 19.2f;
        camera.aspect = 1024f / 1536f;
        camera.transform.position = new Vector3(0f, 0f, -50f);
        var target = new RenderTexture(1024, 1536, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1024, 1536, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1024, 1536), 0, 0);
        pixels.Apply();
        File.WriteAllBytes("C:/Users/xxxd1/.codex/generated_images/01a12f83-5b2f-7a21-a007-328160682426/aligned-style/unity-preview.png", pixels.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(pixels);
        Object.DestroyImmediate(target);
    }

    public static void Dump()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Plaza.unity");
        var report = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name != "WorldMap" && r.name != "Background" && !r.name.StartsWith("HD_") && !r.name.Contains("House")) continue;
            report.AppendLine($"{r.name}: active={r.gameObject.activeInHierarchy}, enabled={r.enabled}, position={r.transform.position}, scale={r.transform.lossyScale}, bounds={r.bounds}");
            if (r.sprite != null)
                report.AppendLine($"  asset={AssetDatabase.GetAssetPath(r.sprite)}, rect={r.sprite.rect}, pivot={r.sprite.pivot}, ppu={r.sprite.pixelsPerUnit}, texture={r.sprite.texture.name} {r.sprite.texture.width}x{r.sprite.texture.height}, packed={r.sprite.packed}");
        }
        File.WriteAllText("C:/Users/xxxd1/.codex/generated_images/01a12f83-5b2f-7a21-a007-328160682426/map-tiles/diagnostics.txt", report.ToString());
    }
}
