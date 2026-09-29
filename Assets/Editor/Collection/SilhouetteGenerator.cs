using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 그림(스프라이트)으로 도감용 실루엣 PNG를 만든다: 투명한 가장자리를 잘라내고 256×256 안에 맞춘 뒤 짙은 갈색 한 색으로 칠함.
/// (Tools/UIGen/export_collection.py의 silhouette()와 같은 결과)
/// - Build Global UI: 실루엣이 비어 있는 도감 항목은 자동 생성
/// - 도감 항목 인스펙터 ⋮ → 실루엣 자동 생성
/// - Project 창에서 이미지 우클릭 → Create → Collection Silhouette
/// </summary>
public static class SilhouetteGenerator
{
    public const string OutputFolder = "Assets/Art/UI/Collection/Silhouettes";

    private static readonly Color SilhouetteColor = new Color32(91, 58, 44, 255); // 문서: "실루엣: 짙은 갈색 단색"
    private const int Size = 256;
    private const int Margin = 8;
    private const float AlphaThreshold = 8f / 255f; // 흐린 부스러기 픽셀은 모양에서 제외

    #region 메뉴

    [MenuItem("CONTEXT/CollectionEntry/실루엣 자동 생성")]
    private static void CreateForEntry(MenuCommand command)
    {
        var entry = (CollectionEntry)command.context;
        if (entry.Portrait == null)
        {
            Debug.LogWarning($"[SilhouetteGenerator] {entry.name}: 그림(Portrait)도 연결된 아이템 아이콘도 없어 만들 수 없습니다.", entry);
            return;
        }

        AssignTo(entry, Create(entry.Portrait));
    }

    [MenuItem("Assets/Create/Collection Silhouette", false, 300)]
    private static void CreateForSelection()
    {
        foreach (var obj in Selection.objects)
        {
            var sprite = ToSprite(obj);
            if (sprite != null)
                Selection.activeObject = Create(sprite);
        }
    }

    [MenuItem("Assets/Create/Collection Silhouette", true)]
    private static bool CreateForSelectionValidate()
    {
        foreach (var obj in Selection.objects)
        {
            if (ToSprite(obj) != null)
                return true;
        }
        return false;
    }

    // 텍스처를 골랐으면 그 안의 첫 스프라이트(가장 큰 조각)를 사용
    private static Sprite ToSprite(Object obj)
    {
        if (obj is Sprite sprite)
            return sprite;

        if (obj is Texture2D texture)
        {
            Sprite best = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)))
            {
                if (asset is Sprite s && (best == null || s.rect.width * s.rect.height > best.rect.width * best.rect.height))
                    best = s;
            }
            return best;
        }

        return null;
    }

    #endregion

    /// <summary>실루엣이 비어 있고 그림이 있는 항목에 실루엣을 만들어 채운다.</summary>
    /// <returns>새로 만든 개수</returns>
    public static int FillMissing(System.Collections.Generic.IEnumerable<CollectionEntry> entries)
    {
        int count = 0;
        foreach (var entry in entries)
        {
            if (entry == null || entry.Silhouette != null || entry.Portrait == null)
                continue;

            AssignTo(entry, Create(entry.Portrait));
            count++;
        }
        return count;
    }

    private static void AssignTo(CollectionEntry entry, Sprite silhouette)
    {
        var so = new SerializedObject(entry);
        so.FindProperty("_silhouette").objectReferenceValue = silhouette;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entry);
        AssetDatabase.SaveAssetIfDirty(entry);
        Debug.Log($"[SilhouetteGenerator] {entry.name} ← {AssetDatabase.GetAssetPath(silhouette)}", entry);
    }

    /// <summary>
    /// 스프라이트 모양으로 실루엣 PNG를 만들고 스프라이트로 가져온다. 이미 같은 이름이 있으면 덮어쓴다.
    /// 파일 이름: SIL_ + 원본 이름 (앞의 ICON_은 뺌). 예: ICON_Tomato → SIL_Tomato
    /// </summary>
    public static Sprite Create(Sprite source)
    {
        var shape = ReadSpritePixels(source);
        var output = FitAndPaint(shape);
        Object.DestroyImmediate(shape);

        string path = $"{OutputFolder}/{OutputName(source)}.png";
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllBytes(path, output.EncodeToPNG());
        Object.DestroyImmediate(output);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static string OutputName(Sprite source)
    {
        string name = source.texture.name;
        if (name.StartsWith("ICON_"))
            name = name.Substring("ICON_".Length);
        return "SIL_" + name;
    }

    // 원본 PNG를 파일에서 직접 읽는다 (텍스처의 Read/Write·최대 크기 설정과 상관없이 원본 해상도로)
    private static Texture2D ReadSpritePixels(Sprite source)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source.texture);
        var full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        full.LoadImage(File.ReadAllBytes(sourcePath));

        // 가져올 때 축소됐을 수 있으므로 스프라이트 영역을 원본 해상도로 환산
        float k = (float)full.width / source.texture.width;
        var rect = source.rect;
        int x = Mathf.RoundToInt(rect.x * k);
        int y = Mathf.RoundToInt(rect.y * k);
        int w = Mathf.Min(Mathf.RoundToInt(rect.width * k), full.width - x);
        int h = Mathf.Min(Mathf.RoundToInt(rect.height * k), full.height - y);

        var cropped = new Texture2D(w, h, TextureFormat.RGBA32, false);
        cropped.SetPixels(full.GetPixels(x, y, w, h));
        cropped.Apply();
        Object.DestroyImmediate(full);
        return cropped;
    }

    // 투명한 가장자리를 잘라내고 256×256 가운데에 맞춘 뒤, 모양(알파)만 남기고 한 색으로 칠함
    private static Texture2D FitAndPaint(Texture2D shape)
    {
        var pixels = shape.GetPixels();
        int minX = shape.width, minY = shape.height, maxX = -1, maxY = -1;
        for (int y = 0; y < shape.height; y++)
        {
            for (int x = 0; x < shape.width; x++)
            {
                if (pixels[y * shape.width + x].a <= AlphaThreshold)
                    continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        var output = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var clear = new Color[Size * Size];
        output.SetPixels(clear);

        if (maxX < 0)
        {
            Debug.LogWarning($"[SilhouetteGenerator] {shape.name}: 보이는 픽셀이 없습니다.");
            output.Apply();
            return output;
        }

        int bw = maxX - minX + 1;
        int bh = maxY - minY + 1;
        float scale = (Size - 2f * Margin) / Mathf.Max(bw, bh);
        int tw = Mathf.Max(1, Mathf.RoundToInt(bw * scale));
        int th = Mathf.Max(1, Mathf.RoundToInt(bh * scale));
        int ox = (Size - tw) / 2;
        int oy = (Size - th) / 2;

        // 큰 원본을 줄일 때 계단 현상이 없도록, 대상 픽셀 하나가 덮는 원본 영역의 알파를 평균
        float step = 1f / scale;
        int samples = Mathf.Clamp(Mathf.CeilToInt(step), 1, 8);
        for (int ty = 0; ty < th; ty++)
        {
            for (int tx = 0; tx < tw; tx++)
            {
                float alpha = 0f;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        int px = Mathf.Min(minX + (int)((tx + (sx + 0.5f) / samples) * step), maxX);
                        int py = Mathf.Min(minY + (int)((ty + (sy + 0.5f) / samples) * step), maxY);
                        alpha += pixels[py * shape.width + px].a;
                    }
                }
                alpha /= samples * samples;
                output.SetPixel(ox + tx, oy + ty, new Color(SilhouetteColor.r, SilhouetteColor.g, SilhouetteColor.b, alpha));
            }
        }

        output.Apply();
        return output;
    }
}
